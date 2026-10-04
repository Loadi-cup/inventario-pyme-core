namespace Core.ControlAcceso;

public record ResultadoRegistro(bool Exitoso, string? Error);

public interface IAuthService
{
    Task<ResultadoRegistro> RegistrarAsync(string nombre, string correo, string contrasena);
    Task<bool> ActivarAsync(string codigo);
    Task ReenviarActivacionAsync(string correo);
}