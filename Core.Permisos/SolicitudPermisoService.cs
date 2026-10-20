using Microsoft.EntityFrameworkCore;
using Core.Permisos.Modelos;

namespace Core.Permisos;

public record ResultadoPermiso(bool Exitoso, string? Error, SolicitudPermiso? Solicitud = null);

public class SolicitudPermisoService
{
    private readonly PermisosDbContext _db;

    // RD-04: unico lugar donde se declaran las transiciones validas.
    // Agregar una transicion nueva implica tocar solo este diccionario.
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

    // Unico metodo que cambia el estado de una solicitud (RD-04, RF-GP-06)
    private static (bool Exitoso, string? Error) Transicionar(SolicitudPermiso solicitud, EstadoSolicitud nuevo)
    {
        if (!PuedeTransicionar(solicitud.Estado, nuevo))
            return (false, $"No se puede pasar de {solicitud.Estado} a {nuevo}.");

        solicitud.Estado = nuevo;
        return (true, null);
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

    // RF-GP-03, RF-GP-05, RF-GP-06, RF-GP-07
    public async Task<ResultadoPermiso> AprobarAsync(int solicitudId, int aprobadorId)
    {
        var solicitud = await _db.Solicitudes.FindAsync(solicitudId);
        if (solicitud is null)
            return new(false, "Solicitud no encontrada.");

        // RF-GP-07: nadie resuelve su propia solicitud
        if (solicitud.SolicitanteId == aprobadorId)
            return new(false, "No puedes resolver tu propia solicitud.", solicitud);

        var aprobada = Transicionar(solicitud, EstadoSolicitud.Aprobada);
        if (!aprobada.Exitoso)
            return new(false, aprobada.Error, solicitud);

        // RF-GP-03: queda registrado quien aprobo y cuando
        solicitud.AprobadorId = aprobadorId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        // RF-GP-05: el cambio de permiso se aplica automaticamente, en la misma operacion
        _db.PermisosOtorgados.Add(new PermisoOtorgado
        {
            UsuarioId = solicitud.SolicitanteId,
            Permiso = solicitud.PermisoSolicitado,
            SolicitudId = solicitud.Id
        });
        Transicionar(solicitud, EstadoSolicitud.Aplicada);

        await _db.SaveChangesAsync();
        return new(true, null, solicitud);
    }

    // RF-GP-04, RF-GP-06, RF-GP-07
    public async Task<ResultadoPermiso> RechazarAsync(int solicitudId, int aprobadorId, string motivo)
    {
        var solicitud = await _db.Solicitudes.FindAsync(solicitudId);
        if (solicitud is null)
            return new(false, "Solicitud no encontrada.");

        if (solicitud.SolicitanteId == aprobadorId)
            return new(false, "No puedes resolver tu propia solicitud.", solicitud);

        if (!PuedeTransicionar(solicitud.Estado, EstadoSolicitud.Rechazada))
            return new(false, $"No se puede pasar de {solicitud.Estado} a {EstadoSolicitud.Rechazada}.", solicitud);

        // RF-GP-04: sin motivo escrito el rechazo no se completa
        if (string.IsNullOrWhiteSpace(motivo))
            return new(false, "El motivo del rechazo es obligatorio.", solicitud);

        Transicionar(solicitud, EstadoSolicitud.Rechazada);
        solicitud.AprobadorId = aprobadorId;
        solicitud.FechaResolucion = DateTime.UtcNow;
        solicitud.MotivoRechazo = motivo.Trim();

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