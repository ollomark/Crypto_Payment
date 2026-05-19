namespace Crypto_Payment.DTOS;

public class InvoiceDashboardDto
{
    public string Status { get; set; } = "";
    public string Currency { get; set; } = "";  
    public int Count { get; set; }         
    public decimal Total { get; set; }         
}

public class MonthlyStatsDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalInvoices { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PaidCount { get; set; }
    public decimal PendingAmount { get; set; }
    public int PendingCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OverdueCount { get; set; }
    public decimal RecurringAmount { get; set; }
    public int RecurringCount { get; set; }
    public int OnetimeCount { get; set; }
    public double CollectionRate { get; set; }
    public int UpcomingCount { get; set; }
    public decimal UpcomingAmount { get; set; }
}

