namespace Crypto_Payment.Models;

public class SystemLog
{
    public int Id { get; set; }
    public string Action { get; set; } = "";            // "InvoiceStatusChange", "Login" vb.
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? TargetEntity { get; set; }           // "Invoice", "Customer" vb.
    public string? TargetId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
