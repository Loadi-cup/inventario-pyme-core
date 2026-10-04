namespace Core.ControlAcceso;

public record ResultadoRegistro(bool Exitoso, string? Error);
public record ResultadoLogin(bool Exitoso, string? Token, string? Error);

public interface IAuthService
{
    Task<ResultadoRegistro> RegistrarAsync(string nombre, string correo, string contrasena);
    Task<bool> ActivarAsync(string codigo);
    Task ReenviarActivacionAsync(string correo);
    Task<ResultadoLogin> LoginAsync(string correo, string contrasena);
    Task CerrarSesionAsync(string jti, DateTime expiracionToken);
}