using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Core.Permisos;
using Core.Permisos.Modelos;

namespace Api.Controllers;

[ApiController]
[Route("api/permisos")]
[Authorize] // RD-06: toda operacion exige sesion valida
public class PermisosController : ControllerBase
{
    private readonly SolicitudPermisoService _servicio;

    public PermisosController(SolicitudPermisoService servicio)
    {
        _servicio = servicio;
    }

    public record CrearSolicitudRequest(string Permiso);
    public record RechazarRequest(string Motivo);

    private int UsuarioActualId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static object Resumen(SolicitudPermiso s) => new
    {
        s.Id,
        s.SolicitanteId,
        s.PermisoSolicitado,
        Estado = s.Estado.ToString(),
        s.AprobadorId,
        s.FechaCreacion,
        s.FechaResolucion,
        s.MotivoRechazo
    };

    // RF-GP-01
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSolicitudRequest request)
    {
        var r = await _servicio.CrearAsync(UsuarioActualId(), request.Permiso);
        if (!r.Exitoso) return BadRequest(new { error = r.Error });
        return Ok(Resumen(r.Solicitud!));
    }

    // RF-GP-02: cada usuario solo ve las suyas
    [HttpGet("mias")]
    public async Task<IActionResult> Mias()
    {
        var lista = await _servicio.ListarDeUsuarioAsync(UsuarioActualId());
        return Ok(lista.Select(Resumen));
    }

    // RF-GP-02: solo el Administrador ve las pendientes de todos
    [Authorize(Roles = "Administrador")]
    [HttpGet("pendientes")]
    public async Task<IActionResult> Pendientes()
    {
        var lista = await _servicio.ListarPendientesAsync();
        return Ok(lista.Select(Resumen));
    }

    // RF-GP-03, 05, 06, 07
    [Authorize(Roles = "Administrador")]
    [HttpPost("{id}/aprobar")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var r = await _servicio.AprobarAsync(id, UsuarioActualId());
        if (!r.Exitoso) return BadRequest(new { error = r.Error });
        return Ok(Resumen(r.Solicitud!));
    }

    // RF-GP-04, 06, 07
    [Authorize(Roles = "Administrador")]
    [HttpPost("{id}/rechazar")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarRequest request)
    {
        var r = await _servicio.RechazarAsync(id, UsuarioActualId(), request.Motivo);
        if (!r.Exitoso) return BadRequest(new { error = r.Error });
        return Ok(Resumen(r.Solicitud!));
    }

    // RF-GP-05: el solicitante comprueba que ya tiene el permiso aplicado
    [HttpGet("tengo")]
    public async Task<IActionResult> Tengo([FromQuery] string permiso)
    {
        var tiene = await _servicio.TienePermisoAsync(UsuarioActualId(), permiso ?? string.Empty);
        return Ok(new { permiso, tiene });
    }
}