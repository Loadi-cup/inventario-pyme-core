namespace Core.Permisos.Modelos;

public class SolicitudPermiso
{
    public int Id { get; set; }
    public int SolicitanteId { get; set; }
    public string PermisoSolicitado { get; set; } = string.Empty;
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    // Se llenan al resolver (RF-GP-03 y RF-GP-04)
    public int? AprobadorId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaResolucion { get; set; }
    public string? MotivoRechazo { get; set; }
}