namespace Crypto_Payment.Models;

public class Invoice
{
    public int Id { get; set; }
    public string SourceCurrency { get; set; } = "";   // USD
    public decimal SourceAmount { get; set; }    // 2
    public string OrderNumber { get; set; } = "";      // 1
    public string Currency { get; set; } = "";         // USDT_TRX
    public string Email { get; set; } = "";
    public string OrderName { get; set; } = "";        // usdt
    public string CallbackUrl { get; set; } = "";      // http://...
    public bool RegistrationStatus { get; set; } 
    public DateTime CreatedDate { get; set; }
    
    // Plisio entegrasyonu
    public string? InvoiceUrl { get; set; }      // Plisio ödeme linki
    public string? TxnId { get; set; }           // Plisio işlem ID'si
    public string? Status { get; set; }          // pending, completed, expired, cancelled
    public string? TransactionId { get; set; }   // Blockchain TX ID (Plisio callback'ten gelir)
    
    // Yinelenen fatura
    public bool IsRecurring { get; set; } = false;
    public int? RecurringDay { get; set; }          // Ayın kaçıncı günü (1-28)
    public DateTime? LastReminderDate { get; set; }  // Son hatırlatma tarihi

    // Harici API entegrasyonu
    public int? ApiClientId { get; set; }
    public ApiClient? ApiClient { get; set; }
    /// <summary>Harici sitenin sipariş / referans numarası.</summary>
    public string? ExternalReference { get; set; }
    /// <summary>Ödeme durumu değişince POST atılacak merchant webhook URL.</summary>
    public string? MerchantWebhookUrl { get; set; }

    // Müşteri ilişkisi
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    // Fatura kalemleri
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    // Ödeme linkleri
    public ICollection<PaymentLink> PaymentLinks { get; set; } = new List<PaymentLink>();
}