namespace Crypto_Payment.Models;

/// <summary>Personel maaş bilgisi (Personel rolündeki kullanıcı ile 1:1).</summary>
public class StaffProfile
{
    public string UserId { get; set; } = "";
    public User? User { get; set; }

    /// <summary>Aylık maaş tutarı (maaş girişinde varsayılan öneri için).</summary>
    public decimal? MonthlySalary { get; set; }

    /// <summary>Ayın kaçında ödeme (1–31).</summary>
    public int? SalaryDayOfMonth { get; set; }
}
