using System.Security.Cryptography;
using System.Text;

namespace SalaoBeleza.Services;

// Senhas guardadas com PBKDF2 + salt aleatorio.
// Contas antigas (SHA256 simples) continuam funcionando e sao atualizadas no primeiro login.
public static class SenhaHasher
{
    private const int Iteracoes = 100_000;
    private const string Prefixo = "pbkdf2";

    public static string Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, 32);

        return Prefixo + "$" + Iteracoes + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    public static bool Verificar(string senha, string armazenado)
    {
        if (string.IsNullOrEmpty(armazenado))
            return false;

        if (armazenado.StartsWith(Prefixo + "$"))
        {
            var partes = armazenado.Split('$');

            if (partes.Length != 4 || !int.TryParse(partes[1], out var iteracoes))
                return false;

            try
            {
                var salt = Convert.FromBase64String(partes[2]);
                var esperado = Convert.FromBase64String(partes[3]);
                var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);

                return CryptographicOperations.FixedTimeEquals(calculado, esperado);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Formato antigo: SHA256 em hexadecimal.
        var legado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(senha)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legado),
            Encoding.UTF8.GetBytes(armazenado.ToUpperInvariant()));
    }

    public static bool PrecisaAtualizar(string armazenado)
    {
        return !armazenado.StartsWith(Prefixo + "$");
    }
}
