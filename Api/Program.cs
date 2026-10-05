using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.ControlAcceso;

var builder = WebApplication.CreateBuilder(args);

// --- Base de datos (SQLite) ---
builder.Services.AddDbContext<InventarioDbContext>(options =>
    options.UseSqlite("Data Source=inventario.db"));
    
builder.Services.AddDbContext<Core.Negocio.NegocioDbContext>(options =>
    options.UseSqlite("Data Source=inventario.db"));

// --- JWT ---
var claveJwt = builder.Configuration["Jwt:Clave"]
    ?? throw new InvalidOperationException("Falta la variable de entorno Jwt__Clave");

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

// --- SMTP (variables de entorno, RD-10) ---
var usuarioSmtp = builder.Configuration["Smtp:Usuario"]
    ?? throw new InvalidOperationException("Falta la variable de entorno Smtp__Usuario");
var contrasenaSmtp = builder.Configuration["Smtp:ContrasenaApp"]
    ?? throw new InvalidOperationException("Falta la variable de entorno Smtp__ContrasenaApp");

// --- Servicios propios del Core ---
builder.Services.AddSingleton(new ServicioJwt(claveJwt));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped(sp =>
    new ServicioEnvioCorreo(sp.GetRequiredService<InventarioDbContext>(), usuarioSmtp, contrasenaSmtp));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoint para disparar manualmente el envio de la cola (RF-NOT-09)
app.MapPost("/api/admin/enviar-correos-pendientes", async (ServicioEnvioCorreo servicio) =>
{
    var enviados = await servicio.ProcesarPendientesAsync();
    return Results.Ok(new { enviados });
});

app.MapGet("/api/admin/diagnostico-correos", async (InventarioDbContext db) =>
{
    var correos = await db.CorreosEnCola.ToListAsync();
    return Results.Ok(correos.Select(c => new {
        c.Destinatario, c.Estado, c.Intentos, c.UltimoError
    }));
});

app.Run();