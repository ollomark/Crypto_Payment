using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class InvoiceDto
{
    public int? Id { get; set; } 
    
    [Required(ErrorMessage = "Source currency is required.")]
    public string SourceCurrency { get; set; }

    [Required(ErrorMessage = "Source amount is required.")]
    public decimal SourceAmount { get; set; }

    [Required(ErrorMessage = "Order number is required.")]
    public string OrderNumber { get; set; }

    [Required(ErrorMessage = "Payment currency is required.")]
    public string Currency { get; set; }

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Order name is required.")]
    public string OrderName { get; set; }

    [Required(ErrorMessage = "Callback URL is required.")]
    public string CallbackUrl { get; set; }

    // Customer selection
    public int? CustomerId { get; set; }

    // Plisio integration (read-only)
    public string? InvoiceUrl { get; set; }
    public string? TxnId { get; set; }
    public string? Status { get; set; }
}
