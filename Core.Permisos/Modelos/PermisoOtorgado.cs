namespace Core.Permisos.Modelos;

// RF-GP-05: el cambio de permiso que se aplica al aprobar una solicitud
public class PermisoOtorgado
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Permiso { get; set; } = string.Empty;
    public int SolicitudId { get; set; }
    public DateTime FechaOtorgado { get; set; } = DateTime.UtcNow;
}