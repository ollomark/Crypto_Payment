namespace Crypto_Payment.Models;

public static class InvoiceStatus
{
    public const string New = "new";
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
    public const string Preparing = "preparing";
    public const string Error = "error";

    public const string Mismatch = "mismatch";

    private static readonly HashSet<string> TerminalStatuses = new()
    {
        Completed, Mismatch, Expired, Cancelled, Error
    };

    public static bool IsTerminal(string? status) =>
        status != null && TerminalStatuses.Contains(status);
}
