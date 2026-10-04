using Microsoft.EntityFrameworkCore;
using Core.ControlAcceso.Modelos;

namespace Core.ControlAcceso;

public class AuthService : IAuthService
{
    private readonly InventarioDbContext _db;
    private readonly ServicioJwt _jwt;

    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public AuthService(InventarioDbContext db, ServicioJwt jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<ResultadoRegistro> RegistrarAsync(string nombre, string correo, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(correo))
            return new ResultadoRegistro(false, "Nombre y correo son obligatorios.");

        if (!correo.Contains('@') || !correo.Contains('.'))
            return new ResultadoRegistro(false, "El correo no tiene un formato valido.");

        if (!ServicioContrasena.CumplePolitica(contrasena))
            return new ResultadoRegistro(false, "La contrasena debe tener al menos 8 caracteres, con letras y numeros.");

        var correoNormalizado = correo.Trim().ToLowerInvariant();
        var yaExiste = await _db.Usuarios.AnyAsync(u => u.Correo == correoNormalizado);
        if (yaExiste)
            return new ResultadoRegistro(false, "Ya existe una cuenta con ese correo.");

        var usuario = new Usuario
        {
            Nombre = nombre.Trim(),
            Correo = correoNormalizado,
            HashContrasena = ServicioContrasena.Hashear(contrasena),
            Rol = Rol.Estandar,
            Activo = false
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        await CrearYEncolarTokenActivacionAsync(usuario);
        await _db.SaveChangesAsync();

        return new ResultadoRegistro(true, null);
    }

    public async Task<bool> ActivarAsync(string codigo)
    {
        var token = await _db.TokensActivacion
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (token is null || token.Usado || token.FechaVencimiento < DateTime.UtcNow)
            return false;

        token.Usado = true;
        token.Usuario.Activo = true;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task ReenviarActivacionAsync(string correo)
    {
        var correoNormalizado = correo.Trim().ToLowerInvariant();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correoNormalizado);

        if (usuario is null || usuario.Activo)
            return;

        var tokensAnteriores = await _db.TokensActivacion
            .Where(t => t.UsuarioId == usuario.Id && !t.Usado)
            .ToListAsync();
        foreach (var t in tokensAnteriores)
            t.Usado = true;

        await CrearYEncolarTokenActivacionAsync(usuario);
        await _db.SaveChangesAsync();
    }

    public async Task<ResultadoLogin> LoginAsync(string correo, string contrasena)
    {
        var correoNormalizado = correo.Trim().ToLowerInvariant();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correoNormalizado);

        const string mensajeGenerico = "Correo o contrasena incorrectos.";

        if (usuario is null)
            return new ResultadoLogin(false, null, mensajeGenerico);

        if (!usuario.Activo)
            return new ResultadoLogin(false, null, "La cuenta no esta activa. Revisa tu correo.");

        if (usuario.BloqueadoHasta is not null && usuario.BloqueadoHasta > DateTime.UtcNow)
            return new ResultadoLogin(false, null, mensajeGenerico);

        if (!ServicioContrasena.Verificar(contrasena, usuario.HashContrasena))
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= MaxIntentosFallidos)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.Add(DuracionBloqueo);
            }
            await _db.SaveChangesAsync();
            return new ResultadoLogin(false, null, mensajeGenerico);
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await _db.SaveChangesAsync();

        var token = _jwt.GenerarToken(usuario);
        return new ResultadoLogin(true, token, null);
    }

    public async Task CerrarSesionAsync(string jti, DateTime expiracionToken)
    {
        var yaInvalidado = await _db.TokensInvalidados.AnyAsync(t => t.Jti == jti);
        if (yaInvalidado) return;

        _db.TokensInvalidados.Add(new TokenInvalidado
        {
            Jti = jti,
            FechaExpiracionToken = expiracionToken
        });
        await _db.SaveChangesAsync();
    }

    // RF-CA-21: listado sin datos sensibles (sin hash ni tokens)
    public async Task<List<UsuarioResumen>> ListarUsuariosAsync()
    {
        return await _db.Usuarios
            .Select(u => new UsuarioResumen(u.Id, u.Nombre, u.Correo, u.Rol.ToString(), u.Activo))
            .ToListAsync();
    }

    // RF-CA-08: cambio de rol reservado al Administrador (verificado tambien en el controlador via [Authorize])
    public async Task<(bool Exitoso, string? Error)> CambiarRolAsync(int adminId, int usuarioId, string nuevoRol)
    {
        if (adminId == usuarioId)
            return (false, "No puedes cambiar tu propio rol.");

        if (!Enum.TryParse<Rol>(nuevoRol, ignoreCase: true, out var rolParseado))
            return (false, "Rol invalido. Usa 'Estandar' o 'Administrador'.");

        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
            return (false, "Usuario no encontrado.");

        usuario.Rol = rolParseado;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    // RF-CA-20: desactivar invalida sesiones abiertas (via SesionesValidasDesde)
    public async Task<(bool Exitoso, string? Error)> DesactivarUsuarioAsync(int adminId, int usuarioId)
    {
        if (adminId == usuarioId)
            return (false, "No puedes desactivarte a ti mismo.");

        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
            return (false, "Usuario no encontrado.");

        usuario.Activo = false;
        usuario.SesionesValidasDesde = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Exitoso, string? Error)> ReactivarUsuarioAsync(int usuarioId)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
            return (false, "Usuario no encontrado.");

        usuario.Activo = true;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    private async Task CrearYEncolarTokenActivacionAsync(Usuario usuario)
    {
        var token = new TokenActivacion
        {
            UsuarioId = usuario.Id,
            Codigo = ServicioCodigos.GenerarCodigo(),
            FechaVencimiento = DateTime.UtcNow.AddHours(24)
        };
        _db.TokensActivacion.Add(token);

        var enlace = $"https://localhost:5118/api/auth/activar?codigo={token.Codigo}";
        _db.CorreosEnCola.Add(new CorreoEnCola
        {
            Destinatario = usuario.Correo,
            Asunto = "Activa tu cuenta - Inventario Pyme",
            Cuerpo = $"Hola {usuario.Nombre}, activa tu cuenta haciendo clic aqui: {enlace}"
        });

        await Task.CompletedTask;
    }
}