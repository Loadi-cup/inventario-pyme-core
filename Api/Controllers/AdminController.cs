using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Core.ControlAcceso;

namespace Api.Controllers;

[ApiController]
[Route("api/admin/usuarios")]
[Authorize(Roles = "Administrador")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;

    public AdminController(IAuthService authService)
    {
        _authService = authService;
    }

    public record CambiarRolRequest(string NuevoRol);

    private int ObtenerIdDesdeToken() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarios = await _authService.ListarUsuariosAsync();
        return Ok(usuarios);
    }

    [HttpPut("{id}/rol")]
    public async Task<IActionResult> CambiarRol(int id, [FromBody] CambiarRolRequest request)
    {
        var adminId = ObtenerIdDesdeToken();
        var (exitoso, error) = await _authService.CambiarRolAsync(adminId, id, request.NuevoRol);

        if (!exitoso)
            return BadRequest(new { error });

        return Ok(new { mensaje = "Rol actualizado correctamente." });
    }

    [HttpPost("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var adminId = ObtenerIdDesdeToken();
        var (exitoso, error) = await _authService.DesactivarUsuarioAsync(adminId, id);

        if (!exitoso)
            return BadRequest(new { error });

        return Ok(new { mensaje = "Usuario desactivado correctamente." });
    }

    [HttpPost("{id}/reactivar")]
    public async Task<IActionResult> Reactivar(int id)
    {
        var (exitoso, error) = await _authService.ReactivarUsuarioAsync(id);

        if (!exitoso)
            return BadRequest(new { error });

        return Ok(new { mensaje = "Usuario reactivado correctamente." });
    }

    [HttpPost("{id}/forzar-restablecimiento")]
    public async Task<IActionResult> ForzarRestablecimiento(int id)
    {
        var (exitoso, error) = await _authService.ForzarRestablecimientoAsync(id);

        if (!exitoso)
            return BadRequest(new { error });

        return Ok(new { mensaje = "Restablecimiento forzado. El usuario recibira un correo." });
    }
}