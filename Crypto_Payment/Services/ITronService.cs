namespace Crypto_Payment.Services;

public interface ITronService
{
    /// <param name="excludeTxHashes">Bu TxHash'ler eşleşmede hariç tutulur (aynı cüzdana başka ödemeye atanmış olanlar)</param>
    Task<TronTransactionResult?> CheckPaymentAsync(string walletAddress, decimal expectedAmount, string currency, DateTime sinceUtc, HashSet<string>? excludeTxHashes = null);
}

public class TronTransactionResult
{
    public bool Found { get; set; }
    public string? TxHash { get; set; }
    public decimal Amount { get; set; }
    public string? FromAddress { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Confirmed { get; set; }
}
