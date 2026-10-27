using Microsoft.EntityFrameworkCore;
using Core.Permisos.Modelos;

namespace Core.Permisos;

public record ResultadoPermiso(bool Exitoso, string? Error, SolicitudPermiso? Solicitud = null);

public class SolicitudPermisoService
{
    private readonly PermisosDbContext _db;

    // RD-04: unico lugar donde se declaran las transiciones validas.
    private static readonly Dictionary<EstadoSolicitud, EstadoSolicitud[]> TransicionesPermitidas = new()
    {
        [EstadoSolicitud.Pendiente] = new[] { EstadoSolicitud.Aprobada, EstadoSolicitud.Rechazada },
        [EstadoSolicitud.Aprobada] = new[] { EstadoSolicitud.Aplicada },
        [EstadoSolicitud.Aplicada] = Array.Empty<EstadoSolicitud>(),
        [EstadoSolicitud.Rechazada] = Array.Empty<EstadoSolicitud>()
    };

    public SolicitudPermisoService(PermisosDbContext db)
    {
        _db = db;
    }

    private static bool PuedeTransicionar(EstadoSolicitud desde, EstadoSolicitud hacia) =>
        TransicionesPermitidas.TryGetValue(desde, out var permitidas) && permitidas.Contains(hacia);

    // RF-GP-06: operacion generica que cambia el estado solo si la tabla lo permite.
    // La API no la expone; existe para probar la maquina de estados directamente (RD-12).
    public async Task<ResultadoPermiso> TransicionarAsync(int solicitudId, EstadoSolicitud nuevoEstado)
    {
        var solicitud = await _db.Solicitudes.FindAsync(solicitudId);
        if (solicitud is null)
            return new(false, "Solicitud no encontrada.");

        if (!PuedeTransicionar(solicitud.Estado, nuevoEstado))
            return new(false, $"No se puede pasar de {solicitud.Estado} a {nuevoEstado}.", solicitud);

        solicitud.Estado = nuevoEstado;
        await _db.SaveChangesAsync();
        return new(true, null, solicitud);
    }

    // RF-GP-01
    public async Task<ResultadoPermiso> CrearAsync(int solicitanteId, string permiso)
    {
        if (solicitanteId <= 0)
            return new(false, "Solicitante invalido.");

        if (string.IsNullOrWhiteSpace(permiso))
            return new(false, "El permiso solicitado es obligatorio.");

        if (permiso.Trim().Length > 100)
            return new(false, "El permiso solicitado no puede superar 100 caracteres.");

        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitanteId,
            PermisoSolicitado = permiso.Trim()
        };

        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();

        return new(true, null, solicitud);
    }

    // RF-GP-02: el Administrador ve las pendientes de todos
    public async Task<List<SolicitudPermiso>> ListarPendientesAsync()
    {
        return await _db.Solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    // RF-GP-02: un usuario solo ve las suyas
    public async Task<List<SolicitudPermiso>> ListarDeUsuarioAsync(int usuarioId)
    {
        return await _db.Solicitudes
            .Where(s => s.SolicitanteId == usuarioId)
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    // Carga la solicitud y aplica RF-GP-07 en un unico lugar para aprobar y rechazar
    private async Task<(SolicitudPermiso? Solicitud, string? Error)> CargarParaResolverAsync(int solicitudId, int resolutorId)
    {
        var solicitud = await _db.Solicitudes.FindAsync(solicitudId);
        if (solicitud is null) return (null, "Solicitud no encontrada.");
        if (solicitud.SolicitanteId == resolutorId) return (solicitud, "No puedes resolver tu propia solicitud.");
        return (solicitud, null);
    }

    // RF-GP-03, RF-GP-05, RF-GP-06, RF-GP-07
    public async Task<ResultadoPermiso> AprobarAsync(int solicitudId, int aprobadorId)
    {
        var (solicitud, error) = await CargarParaResolverAsync(solicitudId, aprobadorId);
        if (error is not null)
            return new(false, error, solicitud);

        if (!PuedeTransicionar(solicitud!.Estado, EstadoSolicitud.Aprobada))
            return new(false, $"No se puede pasar de {solicitud.Estado} a {EstadoSolicitud.Aprobada}.", solicitud);

        // RF-GP-03: queda registrado quien aprobo y cuando
        solicitud.Estado = EstadoSolicitud.Aprobada;
        solicitud.AprobadorId = aprobadorId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        // RF-GP-05: el permiso se aplica en la misma operacion
        _db.PermisosOtorgados.Add(new PermisoOtorgado
        {
            UsuarioId = solicitud.SolicitanteId,
            Permiso = solicitud.PermisoSolicitado,
            SolicitudId = solicitud.Id
        });

        // Aprobada -> Aplicada: automatica e inmediata
        solicitud.Estado = EstadoSolicitud.Aplicada;

        await _db.SaveChangesAsync();
        return new(true, null, solicitud);
    }

    // RF-GP-04, RF-GP-06, RF-GP-07
    public async Task<ResultadoPermiso> RechazarAsync(int solicitudId, int aprobadorId, string motivo)
    {
        var (solicitud, error) = await CargarParaResolverAsync(solicitudId, aprobadorId);
        if (error is not null)
            return new(false, error, solicitud);

        if (!PuedeTransicionar(solicitud!.Estado, EstadoSolicitud.Rechazada))
            return new(false, $"No se puede pasar de {solicitud.Estado} a {EstadoSolicitud.Rechazada}.", solicitud);

        // RF-GP-04: sin motivo escrito el rechazo no se completa
        if (string.IsNullOrWhiteSpace(motivo))
            return new(false, "El motivo del rechazo es obligatorio.", solicitud);

        solicitud.Estado = EstadoSolicitud.Rechazada;
        solicitud.AprobadorId = aprobadorId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new(true, null, solicitud);
    }

    // RF-GP-05: comprobar si el usuario ya tiene el permiso aplicado
    public async Task<bool> TienePermisoAsync(int usuarioId, string permiso)
    {
        return await _db.PermisosOtorgados
            .AnyAsync(p => p.UsuarioId == usuarioId && p.Permiso == permiso.Trim());
    }
}