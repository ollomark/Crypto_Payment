using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class InvoiceDto
{
    public int? Id { get; set; } 
    
    [Required(ErrorMessage = "Kaynak para birimi zorunludur.")]
    public string SourceCurrency { get; set; } 

    [Required(ErrorMessage = "Kaynak tutar zorunludur.")]
    public decimal SourceAmount { get; set; } 

    [Required(ErrorMessage = "Sipariş numarası zorunludur.")]
    public string OrderNumber { get; set; } 

    [Required(ErrorMessage = "Ödeme para birimi zorunludur.")]
    public string Currency { get; set; } 

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Sipariş adı zorunludur.")]
    public string OrderName { get; set; }

    [Required(ErrorMessage = "Callback URL zorunludur.")]
    public string CallbackUrl { get; set; }

    // Müşteri seçimi
    public int? CustomerId { get; set; }
    
    // Plisio entegrasyonu (sadece okuma için)
    public string? InvoiceUrl { get; set; }
    public string? TxnId { get; set; }
    public string? Status { get; set; }
}
