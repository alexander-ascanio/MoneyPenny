using System.Security.Cryptography;
using System.Text;

namespace MoneyPenny.Helpers;

public static class CommentImageSourceKey
{
    public const int MaxLength = 2048;

    public static string ForCache(string sanitizedSource)
    {
        if (string.IsNullOrWhiteSpace(sanitizedSource))
        {
            return string.Empty;
        }

        if (sanitizedSource.Length <= MaxLength)
        {
            return sanitizedSource;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sanitizedSource)));
        return $"hash:{hash}";
    }
}
