using Crypto_Payment.DTOS;
using Crypto_Payment.Models;

namespace Crypto_Payment.ViewModels;

public class MasterDashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalInvoices { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PaidCount { get; set; }
    public decimal PendingAmount { get; set; }
    public int PendingCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OverdueCount { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetKasa { get; set; }
    /// <summary>Ay Sonu Karlılık kartı için — Finance Report ile aynı Tahsil (Gelir).</summary>
    public decimal NetKasaTahsil { get; set; }
    /// <summary>Ay Sonu Karlılık kartı için — Finance Report ile aynı Gider.</summary>
    public decimal NetKasaGider { get; set; }
    public int RecurringCount { get; set; }
    public decimal RecurringAmount { get; set; }
    public double CollectionRate { get; set; }
    public int UpcomingCount { get; set; }
    public decimal UpcomingAmount { get; set; }
    public int CurrentMonth { get; set; }
    public int CurrentYear { get; set; }
    public List<InvoiceDto> RecentInvoices { get; set; } = new();
    public List<TodayDueInvoice> TodayDueInvoices { get; set; } = new();
    public List<ApprovalRequest> PendingApprovals { get; set; } = new();
    public int PendingExpenseCount { get; set; }
    public decimal PendingExpenseAmount { get; set; }
}

public class TodayDueInvoice
{
    public int InvoiceId { get; set; }
    public string OrderName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public int RecurringDay { get; set; }
    public bool IsPaidThisMonth { get; set; }
}
