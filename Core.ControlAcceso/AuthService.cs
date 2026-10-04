using Microsoft.EntityFrameworkCore;
using Core.ControlAcceso.Modelos;

namespace Core.ControlAcceso;

public class AuthService : IAuthService
{
    private readonly InventarioDbContext _db;

    public AuthService(InventarioDbContext db)
    {
        _db = db;
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

        // RF-CA-17: no revelar si el correo existe; si no existe, simplemente no hacemos nada
        if (usuario is null || usuario.Activo)
            return;

        // Invalidar tokens anteriores no usados
        var tokensAnteriores = await _db.TokensActivacion
            .Where(t => t.UsuarioId == usuario.Id && !t.Usado)
            .ToListAsync();
        foreach (var t in tokensAnteriores)
            t.Usado = true;

        await CrearYEncolarTokenActivacionAsync(usuario);
        await _db.SaveChangesAsync();
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