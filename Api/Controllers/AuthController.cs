using Microsoft.AspNetCore.Mvc;
using Core.ControlAcceso;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    public record RegistroRequest(string Nombre, string Correo, string Contrasena);
    public record ReenvioRequest(string Correo);

    [HttpPost("registro")]
    public async Task<IActionResult> Registrar([FromBody] RegistroRequest request)
    {
        var resultado = await _authService.RegistrarAsync(
            request.Nombre, request.Correo, request.Contrasena);

        if (!resultado.Exitoso)
            return BadRequest(new { error = resultado.Error });

        return Ok(new { mensaje = "Registro exitoso. Revisa tu correo para activar la cuenta." });
    }

    [HttpGet("activar")]
    public async Task<IActionResult> Activar([FromQuery] string codigo)
    {
        var exitoso = await _authService.ActivarAsync(codigo);

        if (!exitoso)
            return BadRequest(new { error = "El enlace de activacion es invalido o ya vencio." });

        return Ok(new { mensaje = "Cuenta activada correctamente. Ya puedes iniciar sesion." });
    }

    [HttpPost("reenviar-activacion")]
    public async Task<IActionResult> ReenviarActivacion([FromBody] ReenvioRequest request)
    {
        await _authService.ReenviarActivacionAsync(request.Correo);
        // RF-CA-17: respuesta identica exista o no el correo
        return Ok(new { mensaje = "Si el correo esta registrado, se envio un nuevo enlace." });
    }
}