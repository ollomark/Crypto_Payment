namespace Crypto_Payment.Models;

public class WithdrawalRequest
{
    public int Id { get; set; }
    public Guid Token { get; set; } = Guid.NewGuid();
    public string CreatedByAdminId { get; set; } = "";
    public string CreatedByAdminName { get; set; } = "";
    public WithdrawalCategory Category { get; set; } = WithdrawalCategory.Diger;
    public string RequestCode { get; set; } = "";

    // Müşteri dolduracak
    public string? CustomerName { get; set; }
    public string? CompanyName { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public WdPaymentMethod? PaymentMethod { get; set; }
    public CryptoNetwork? CryptoNetwork { get; set; }
    public string? WalletAddressOrIban { get; set; }

    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.LinkCreated;
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? FilledDate { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewedBy { get; set; }
    public string? AdminNote { get; set; }
    public string? CustomerNote { get; set; }
    public string? FilledByIp { get; set; }
}

public enum WdPaymentMethod
{
    Banka = 1,
    Kripto = 2
}

public enum CryptoNetwork
{
    TRC20 = 1,
    ERC20 = 2,
    BTC = 3
}

public enum WithdrawalCategory
{
    Maas = 1,
    SunucuOdemesi = 2,
    Avans = 3,
    BayiOdemesi = 4,
    Diger = 5
}

public enum WithdrawalStatus
{
    LinkCreated = 0,
    CustomerFilled = 1,
    Approved = 2,
    Rejected = 3
}
