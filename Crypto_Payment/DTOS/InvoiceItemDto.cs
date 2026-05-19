using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class InvoiceItemDto
{
    public int? Id { get; set; }
    
    public int? InvoiceId { get; set; }

    [Required(ErrorMessage = "Hizmet adı zorunludur.")]
    [StringLength(100, ErrorMessage = "Hizmet adı en fazla 100 karakter olabilir.")]
    public string ServiceName { get; set; } = "";
    
    [StringLength(200, ErrorMessage = "Hizmet açıklaması en fazla 200 karakter olabilir.")]
    public string? ServiceDescription { get; set; }

    [Required(ErrorMessage = "Adet zorunludur.")]
    [Range(1, int.MaxValue, ErrorMessage = "Adet en az 1 olmalıdır.")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Fiyat zorunludur.")]
    [Range(0.01, 999999999, ErrorMessage = "Fiyat 0'dan büyük olmalıdır.")]
    public decimal Price { get; set; }
    
    public decimal Total { get; set; }

    // Status isteğe bağlı
    public bool? Status { get; set; }
}