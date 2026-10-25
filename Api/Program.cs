using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.ControlAcceso;
using Core.ControlAcceso.Modelos;
using Core.Negocio;
using Core.Permisos;

var builder = WebApplication.CreateBuilder(args);

// --- Bases de datos (SQLite, persisten fuera del proceso: RD-09) ---
builder.Services.AddDbContext<InventarioDbContext>(options =>
    options.UseSqlite("Data Source=inventario.db"));
builder.Services.AddDbContext<NegocioDbContext>(options =>
    options.UseSqlite("Data Source=inventario.db"));
builder.Services.AddDbContext<PermisosDbContext>(options =>
    options.UseSqlite("Data Source=inventario.db"));

// --- Secretos y configuracion: todo desde variables de entorno (RD-10) ---
var claveJwt = builder.Configuration["Jwt:Clave"]
    ?? throw new InvalidOperationException("Falta la variable de entorno Jwt__Clave");

var smtp = new OpcionesSmtp(
    Host: builder.Configuration["Smtp:Host"] ?? "smtp.gmail.com",
    Puerto: int.TryParse(builder.Configuration["Smtp:Puerto"], out var puerto) ? puerto : 587,
    Usuario: builder.Configuration["Smtp:Usuario"]
        ?? throw new InvalidOperationException("Falta la variable de entorno Smtp__Usuario"),
    Contrasena: builder.Configuration["Smtp:ContrasenaApp"]
        ?? throw new InvalidOperationException("Falta la variable de entorno Smtp__ContrasenaApp"),
    Remitente: builder.Configuration["Smtp:Remitente"]
        ?? builder.Configuration["Smtp:Usuario"]!);

var urlBase = (builder.Configuration["App:UrlBase"] ?? "http://localhost:5118").TrimEnd('/');

// --- JWT ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveJwt))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<InventarioDbContext>();

                var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (jti is not null)
                {
                    var invalidado = await db.TokensInvalidados.AnyAsync(t => t.Jti == jti);
                    if (invalidado)
                    {
                        context.Fail("Token invalidado.");
                        return;
                    }
                }

                var correo = context.Principal?.FindFirstValue(ClaimTypes.Email);
                var emitidoTicksTexto = context.Principal?.FindFirstValue("emitido");

                if (correo is not null && emitidoTicksTexto is not null
                    && long.TryParse(emitidoTicksTexto, out var emitidoTicks))
                {
                    var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);
                    if (usuario is null || !usuario.Activo)
                    {
                        context.Fail("Usuario inactivo o inexistente.");
                        return;
                    }

                    var emitido = new DateTime(emitidoTicks, DateTimeKind.Utc);
                    if (emitido < usuario.SesionesValidasDesde)
                    {
                        context.Fail("Token emitido antes del ultimo cambio de contrasena o desactivacion.");
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();

// --- Servicios propios del Core ---
builder.Services.AddSingleton(new ServicioJwt(claveJwt));
builder.Services.AddSingleton(new OpcionesApp(urlBase));
builder.Services.AddSingleton(smtp);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ServicioEnvioCorreo>();
builder.Services.AddScoped<SolicitudPermisoService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Al arrancar: crear las tablas y, si se pidio, el Administrador inicial ---
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var usuariosDb = sp.GetRequiredService<InventarioDbContext>();
    usuariosDb.Database.Migrate();
    sp.GetRequiredService<NegocioDbContext>().Database.Migrate();
    sp.GetRequiredService<PermisosDbContext>().Database.Migrate();

    // Administrador inicial: credenciales solo por variables de entorno (RD-10)
    var adminCorreo = builder.Configuration["Admin:Correo"];
    var adminClave = builder.Configuration["Admin:Contrasena"];
    if (!string.IsNullOrWhiteSpace(adminCorreo) && !string.IsNullOrWhiteSpace(adminClave))
    {
        var correo = adminCorreo.Trim().ToLowerInvariant();
        if (!usuariosDb.Usuarios.Any(u => u.Correo == correo))
        {
            if (!ServicioContrasena.CumplePolitica(adminClave))
                throw new InvalidOperationException(
                    "Admin__Contrasena debe tener al menos 8 caracteres, con letras y numeros.");

            usuariosDb.Usuarios.Add(new Usuario
            {
                Nombre = "Administrador",
                Correo = correo,
                HashContrasena = ServicioContrasena.Hashear(adminClave),
                Rol = Rol.Administrador,
                Activo = true
            });
            usuariosDb.SaveChanges();
        }
    }
}

// --- Comando independiente: dotnet run --no-build --project Api -- enviar-correos ---
// Toma los correos pendientes de la cola, los envia por SMTP y termina (RF-NOT-09, RF-NOT-12).
if (args.Contains("enviar-correos"))
{
    using var scope = app.Services.CreateScope();
    var enviados = await scope.ServiceProvider.GetRequiredService<ServicioEnvioCorreo>().ProcesarPendientesAsync();
    Console.WriteLine($"Correos enviados: {enviados}");
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();