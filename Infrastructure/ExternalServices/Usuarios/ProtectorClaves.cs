using System.Security.Cryptography;
using Application.Interfaces;
namespace Infrastructure.ExternalServices;

public sealed class ProtectorClaves : IProtectorClaves
{
    private const int Iteraciones = 210000;
    public string Proteger(string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA512, 32);
        return $"{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }
    public bool Verificar(string hash, string clave)
    {
        try
        {
            var partes = hash.Split('.');
            if (partes.Length != 3 || !int.TryParse(partes[0], out var iter) || iter is < 100000 or > 1000000) return false;
            var esperado = Convert.FromBase64String(partes[2]);
            var obtenido = Rfc2898DeriveBytes.Pbkdf2(clave, Convert.FromBase64String(partes[1]), iter, HashAlgorithmName.SHA512, 32);
            return CryptographicOperations.FixedTimeEquals(esperado, obtenido);
        }
        catch (FormatException) { return false; }
    }
}
