using Crypto_Payment.Models;

namespace Crypto_Payment.Helpers;

public static class StatusMapper
{
    public static string MapPlisioStatus(string? plisioStatus)
    {
        if (string.IsNullOrEmpty(plisioStatus))
            return InvoiceStatus.Pending;

        return plisioStatus.ToLowerInvariant() switch
        {
            "completed" or "confirmed" => InvoiceStatus.Completed,
            "mismatch" => InvoiceStatus.Mismatch,
            "expired" => InvoiceStatus.Expired,
            "cancelled" => InvoiceStatus.Cancelled,
            "error" => InvoiceStatus.Error,
            "new" => InvoiceStatus.New,
            "pending" => InvoiceStatus.Pending,
            _ => InvoiceStatus.Pending
        };
    }
}
