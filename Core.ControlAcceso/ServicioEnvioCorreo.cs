using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using Core.ControlAcceso.Modelos;

namespace Core.ControlAcceso;

public record OpcionesSmtp(string Host, int Puerto, string Usuario, string Contrasena, string Remitente);

public class ServicioEnvioCorreo
{
    private readonly InventarioDbContext _db;
    private readonly OpcionesSmtp _smtp;

    private const int MaxIntentos = 3;

    public ServicioEnvioCorreo(InventarioDbContext db, OpcionesSmtp smtp)
    {
        _db = db;
        _smtp = smtp;
    }

    // RF-NOT-09 y RF-NOT-12: toma los pendientes y los envia. Un correo ya enviado
    // cambia a Enviado y no se vuelve a tomar, asi que ejecutarlo dos veces no duplica.
    public async Task<int> ProcesarPendientesAsync()
    {
        var pendientes = await _db.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var enviados = 0;

        foreach (var correo in pendientes)
        {
            try
            {
                await EnviarPorSmtpAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo);
                correo.Estado = EstadoCorreo.Enviado;
                correo.FechaEnvio = DateTime.UtcNow;
                correo.UltimoError = null;
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

            // Se guarda despues de cada correo para no duplicar si el proceso se interrumpe
            await _db.SaveChangesAsync();
        }

        return enviados;
    }

    private async Task EnviarPorSmtpAsync(string destinatario, string asunto, string cuerpo)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(_smtp.Remitente));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpo };

        using var cliente = new SmtpClient();
        // Auto elige SSL directo en el 465 y STARTTLS en el 587
        await cliente.ConnectAsync(_smtp.Host, _smtp.Puerto, SecureSocketOptions.Auto);
        await cliente.AuthenticateAsync(_smtp.Usuario, _smtp.Contrasena);
        await cliente.SendAsync(mensaje);
        await cliente.DisconnectAsync(true);
    }
}