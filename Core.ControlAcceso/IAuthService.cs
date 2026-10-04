namespace Core.ControlAcceso;

public record ResultadoRegistro(bool Exitoso, string? Error);
public record ResultadoLogin(bool Exitoso, string? Token, string? Error);
public record UsuarioResumen(int Id, string Nombre, string Correo, string Rol, bool Activo);

public interface IAuthService
{
    Task<ResultadoRegistro> RegistrarAsync(string nombre, string correo, string contrasena);
    Task<bool> ActivarAsync(string codigo);
    Task ReenviarActivacionAsync(string correo);
    Task<ResultadoLogin> LoginAsync(string correo, string contrasena);
    Task CerrarSesionAsync(string jti, DateTime expiracionToken);
    Task<List<UsuarioResumen>> ListarUsuariosAsync();
    Task<(bool Exitoso, string? Error)> CambiarRolAsync(int adminId, int usuarioId, string nuevoRol);
    Task<(bool Exitoso, string? Error)> DesactivarUsuarioAsync(int adminId, int usuarioId);
    Task<(bool Exitoso, string? Error)> ReactivarUsuarioAsync(int usuarioId);
    Task SolicitarRecuperacionAsync(string correo);
    Task<(bool Exitoso, string? Error)> RestablecerConCodigoAsync(string codigo, string nuevaContrasena);
    Task<(bool Exitoso, string? Error)> ForzarRestablecimientoAsync(int usuarioId);
    Task<(bool Exitoso, string? Error)> CambiarContrasenaConSesionAsync(int usuarioId, string contrasenaActual, string contrasenaNueva);
}