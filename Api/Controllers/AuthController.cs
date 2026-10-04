using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    public record LoginRequest(string Correo, string Contrasena);
    public record RecuperacionRequest(string Correo);
    public record RestablecerRequest(string Codigo, string NuevaContrasena);
    public record CambiarContrasenaRequest(string ContrasenaActual, string ContrasenaNueva);

    private int ObtenerIdDesdeToken() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
        return Ok(new { mensaje = "Si el correo esta registrado, se envio un nuevo enlace." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var resultado = await _authService.LoginAsync(request.Correo, request.Contrasena);

        if (!resultado.Exitoso)
            return BadRequest(new { error = resultado.Error });

        return Ok(new { token = resultado.Token });
    }

    [Authorize]
    [HttpGet("yo")]
    public IActionResult Yo()
    {
        var correo = User.FindFirstValue(ClaimTypes.Email);
        var rol = User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { correo, rol });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);

        if (jti is null || expClaim is null || !long.TryParse(expClaim, out var expUnix))
            return BadRequest(new { error = "Token invalido." });

        var expiracion = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        await _authService.CerrarSesionAsync(jti, expiracion);

        return Ok(new { mensaje = "Sesion cerrada correctamente." });
    }

    [HttpPost("recuperar")]
    public async Task<IActionResult> Recuperar([FromBody] RecuperacionRequest request)
    {
        await _authService.SolicitarRecuperacionAsync(request.Correo);
        return Ok(new { mensaje = "Si el correo esta registrado, se enviaron instrucciones." });
    }

    [HttpPost("restablecer")]
    public async Task<IActionResult> Restablecer([FromBody] RestablecerRequest request)
    {
        var (exitoso, error) = await _authService.RestablecerConCodigoAsync(
            request.Codigo, request.NuevaContrasena);

        if (!exitoso)
            return BadRequest(new { error });

        return Ok(new { mensaje = "Contrasena actualizada correctamente." });
    }

    [Authorize]
    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> CambiarContrasena([FromBody] CambiarContrasenaRequest request)
    {
        var usuarioId = ObtenerIdDesdeToken();
        var (exitoso, error) = await _authService.CambiarContrasenaConSesionAsync(
            usuarioId, request.ContrasenaActual, request.ContrasenaNueva);

        if (!exitoso)
            return BadRequest(new { error });

        return Ok(new { mensaje = "Contrasena cambiada correctamente." });
    }
}