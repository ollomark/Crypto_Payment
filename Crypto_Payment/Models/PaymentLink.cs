namespace Crypto_Payment.Models;

public class PaymentLink
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string? TxnId { get; set; }
    public string? InvoiceUrl { get; set; }
    public string Status { get; set; } = "new";        // new, pending, completed, expired, cancelled, error, manual
    public string? TransactionId { get; set; }
    public string? Note { get; set; }
    public bool IsManual { get; set; }
    public int? PaidForMonth { get; set; }
    public int? PaidForYear { get; set; }

    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    // Hızlı Kripto ödeme alanları
    public string? CryptoWalletAddress { get; set; }
    public string? CryptoCurrency { get; set; }         // TRX, USDT_TRC20
    public decimal? ExpectedAmount { get; set; }
    public string? ConfirmedTxHash { get; set; }
    public decimal? ReceivedAmount { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiredDate { get; set; }
    /// <summary>Ödeme tamamlandığında (status completed/mismatch) set edilir. PaidForMonth null ise ay eşleştirmede kullanılır.</summary>
    public DateTime? PaidDate { get; set; }
}
