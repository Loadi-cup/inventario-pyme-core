namespace Core.ControlAcceso.Modelos;

public enum EstadoCorreo
{
    Pendiente,
    Enviado,
    Fallido
}

public class CorreoEnCola
{
    public int Id { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public EstadoCorreo Estado { get; set; } = EstadoCorreo.Pendiente;
    public int Intentos { get; set; } = 0;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEnvio { get; set; }
    public string? UltimoError { get; set; }
}