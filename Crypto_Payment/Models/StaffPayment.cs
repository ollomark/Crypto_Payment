using Microsoft.AspNetCore.Identity;

namespace Crypto_Payment.Models;

/// <summary>Personel avans veya maaş ödemesi.</summary>
public class StaffPayment
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public User? User { get; set; }

    /// <summary>Avans veya Maas</summary>
    public StaffPaymentType Type { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";

    /// <summary>Maaş için: hangi ay (1-12). Avans için: null.</summary>
    public int? PeriodMonth { get; set; }
    /// <summary>Maaş için: hangi yıl. Avans için: null.</summary>
    public int? PeriodYear { get; set; }

    public string Description { get; set; } = "";
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    /// <summary>Onaylı gider kaydı; doluysa raporda yalnızca Expenses toplanır (çift sayım yok).</summary>
    public int? ExpenseId { get; set; }
    public Expense? LinkedExpense { get; set; }
}

public enum StaffPaymentType
{
    Avans = 1,
    Maas = 2
}
