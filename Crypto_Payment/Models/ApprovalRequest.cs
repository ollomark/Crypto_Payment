namespace Crypto_Payment.Models;

public class ApprovalRequest
{
    public int Id { get; set; }
    public string RequestType { get; set; } = "";       // "InvoiceStatusChange", "InvoiceDelete" vb.
    public string RequestData { get; set; } = "";       // JSON: { invoiceId, newStatus, ... }
    public string RequestedBy { get; set; } = "";       // UserId
    public string RequestedByName { get; set; } = "";   // Görünen ad
    public string Status { get; set; } = "Pending";     // Pending, Approved, Rejected
    public string? AdminNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedDate { get; set; }
    public string Description { get; set; } = "";       // İnsan okunabilir açıklama
}
