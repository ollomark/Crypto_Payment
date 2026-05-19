using Crypto_Payment.DTOS;

namespace Crypto_Payment.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalInvoices { get; set; }
    public int PendingCount { get; set; }
    public int TotalCustomers { get; set; }
    public int CurrentMonth { get; set; }
    public int CurrentYear { get; set; }
    public List<InvoiceDto> RecentInvoices { get; set; } = new();
    public int MyPendingRequests { get; set; }
}
