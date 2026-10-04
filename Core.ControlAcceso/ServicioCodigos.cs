using System.Security.Cryptography;

namespace Core.ControlAcceso;

public static class ServicioCodigos
{
    public static string GenerarCodigo()
    {
        // 32 bytes aleatorios, codificados en Base64 seguro para URL
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}