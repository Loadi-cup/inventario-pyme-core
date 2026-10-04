using System.Security.Cryptography;

namespace Core.ControlAcceso;

public static class ServicioContrasena
{
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;
    private const int Iteraciones = 100_000;

    public static string Hashear(string contrasenaTextoPlano)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            contrasenaTextoPlano, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);

        return $"{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string contrasenaTextoPlano, string hashAlmacenado)
    {
        var partes = hashAlmacenado.Split('.');
        if (partes.Length != 2) return false;

        var sal = Convert.FromBase64String(partes[0]);
        var hashGuardado = Convert.FromBase64String(partes[1]);

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
            contrasenaTextoPlano, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);

        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashGuardado);
    }

    public static bool CumplePolitica(string contrasena)
    {
        if (contrasena.Length < 8) return false;
        if (!contrasena.Any(char.IsLetter)) return false;
        if (!contrasena.Any(char.IsDigit)) return false;
        return true;
    }
}