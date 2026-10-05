using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using Core.ControlAcceso.Modelos;

namespace Core.ControlAcceso;

public class ServicioEnvioCorreo
{
    private readonly InventarioDbContext _db;
    private readonly string _usuarioSmtp;
    private readonly string _contrasenaSmtp;

    private const int MaxIntentos = 3;

    public ServicioEnvioCorreo(InventarioDbContext db, string usuarioSmtp, string contrasenaSmtp)
    {
        _db = db;
        _usuarioSmtp = usuarioSmtp;
        _contrasenaSmtp = contrasenaSmtp;
    }

    public async Task<int> ProcesarPendientesAsync()
    {
        var pendientes = await _db.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente)
            .ToListAsync();

        var enviados = 0;

        foreach (var correo in pendientes)
        {
            try
            {
                await EnviarPorSmtpAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo);
                correo.Estado = EstadoCorreo.Enviado;
                correo.FechaEnvio = DateTime.UtcNow;
                enviados++;
            }
            catch (Exception ex)
            {
                correo.Intentos++;
                var detalle = ex.InnerException?.Message ?? ex.Message;
                correo.UltimoError = $"{ex.Message} | Detalle: {detalle}";

                if (correo.Intentos >= MaxIntentos)
                    correo.Estado = EstadoCorreo.Fallido;
            }

            await _db.SaveChangesAsync();
        }

        return enviados;
    }

    private async Task EnviarPorSmtpAsync(string destinatario, string asunto, string cuerpo)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(_usuarioSmtp));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpo };

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await cliente.AuthenticateAsync(_usuarioSmtp, _contrasenaSmtp);
        await cliente.SendAsync(mensaje);
        await cliente.DisconnectAsync(true);
    }
}