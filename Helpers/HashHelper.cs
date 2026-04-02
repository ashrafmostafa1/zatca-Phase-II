using System.Security.Cryptography;
using System.Text;

namespace Zatca_Phase_II.Helpers;

public static class HashHelper
{
    public static string ComputeSha256(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hashBytes);
        }
    }
}
