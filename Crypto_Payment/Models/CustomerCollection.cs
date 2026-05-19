namespace Crypto_Payment.Models;

/// <summary>Müşteriden alınan tahsilat (manuel ödeme kaydı).</summary>
public class CustomerCollection
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Description { get; set; } = "";
    public string? Reference { get; set; }  // Fatura no, ödeme referansı vb.

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
