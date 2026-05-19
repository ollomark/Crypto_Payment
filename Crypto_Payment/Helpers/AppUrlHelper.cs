namespace Crypto_Payment.Helpers;

public static class AppUrlHelper
{
    public static string GetBaseUrl()
    {
        return Environment.GetEnvironmentVariable("APP_BASE_URL")
            ?? Environment.GetEnvironmentVariable("APP_URL")
            ?? "http://localhost:5156";
    }

    public static string GetPlisioCallbackUrl()
    {
        return $"{GetBaseUrl().TrimEnd('/')}/api/callback";
    }

    public static string BuildPaymentUrl(int invoiceId, string? txnId)
    {
        var baseUrl = GetBaseUrl().TrimEnd('/');
        if (string.IsNullOrEmpty(txnId))
            return $"{baseUrl}/pay/{invoiceId}";
        return $"{baseUrl}/pay/{invoiceId}?txnId={Uri.EscapeDataString(txnId)}";
    }
}
