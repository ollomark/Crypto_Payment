namespace Crypto_Payment.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public string SenderId { get; set; } = "";
    public string ReceiverId { get; set; } = "";
    public string? Content { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
