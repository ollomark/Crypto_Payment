namespace Crypto_Payment.Models;

public class PaymentMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "manual";       // plisio, bank_transfer, credit_card, manual, crypto_wallet
    public string? Description { get; set; }
    public string? Icon { get; set; }                   // remixicon class
    public string? BankName { get; set; }
    public string? AccountHolder { get; set; }
    public string? Iban { get; set; }
    public string? WalletAddress { get; set; }
    public string? WalletNetwork { get; set; }
    public string? ApiKey { get; set; }
    public string? ExtraConfig { get; set; }            // JSON for flexible config
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
