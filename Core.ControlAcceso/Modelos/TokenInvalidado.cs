namespace Core.ControlAcceso.Modelos;

public class TokenInvalidado
{
    public int Id { get; set; }
    public string Jti { get; set; } = string.Empty;
    public DateTime FechaExpiracionToken { get; set; }
}