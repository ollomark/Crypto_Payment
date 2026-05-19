using System.Security.Cryptography;
using System.Text;

namespace Crypto_Payment.Helpers;

public static class ApiKeyHelper
{
    public const string KeyPrefix = "cp_live_";

    public static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return KeyPrefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string GenerateWebhookSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashApiKey(string apiKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GetPrefix(string apiKey)
    {
        if (apiKey.Length <= 16) return apiKey;
        return apiKey[..16] + "…";
    }
}
