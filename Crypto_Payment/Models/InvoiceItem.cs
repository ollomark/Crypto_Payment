using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.Models;

public class InvoiceItem
{
    public int Id { get; set; }

    [Required]
    public int InvoiceId { get; set; }

    [Required, MaxLength(100)]
    public string ServiceName { get; set; } = "";
    
    [Required, MaxLength(200)]
    public string ServiceDescription { get; set; } = "";
    
    [Required]
    public int Quantity { get; set; }

    [Required]
    public decimal Price { get; set; }
    
    [Required]
    public decimal Total { get; set; }

    // Durum: true / false (zorunlu değil)
    public bool Status { get; set; }

    public DateTime CreatedDate { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = default!;
}