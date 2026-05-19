namespace Crypto_Payment.Models;

public class Expense
{
    public int Id { get; set; }
    /// <summary>Müşteriye atfedilen masraf için (cari hesap borç hareketi).</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Diger;
    public string Description { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string? RequesterCompany { get; set; }
    public string Method { get; set; } = "Banka";       // Banka, Kripto
    public string? PaymentNetwork { get; set; }          // TRC20, ERC20 vb.
    public string? WalletAddress { get; set; }
    public string? BankInfo { get; set; }
    public string Status { get; set; } = "Pending";     // Pending, Approved, Rejected
    public string? AdminNote { get; set; }
    public string? ReviewedBy { get; set; }
    public string? RequestedByIp { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedDate { get; set; }
}

public enum ExpenseCategory
{
    Maas = 1,
    Avans = 2,
    SunucuGideri = 3,
    OfisGideri = 4,
    KarCekimi = 5,
    Diger = 6
}
