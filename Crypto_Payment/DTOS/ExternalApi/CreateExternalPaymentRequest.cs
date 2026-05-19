using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS.ExternalApi;

public class CreateExternalPaymentRequest
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    /// <summary>FIAT kaynak para birimi (USD, EUR, TRY).</summary>
    [Required]
    public string SourceCurrency { get; set; } = "USD";

    /// <summary>Plisio kripto para birimi (USDT_TRX, BTC, ETH, …).</summary>
    [Required]
    public string CryptoCurrency { get; set; } = "USDT_TRX";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    /// <summary>Benzersiz sipariş numarası (sizin sisteminizde).</summary>
    [Required]
    public string OrderNumber { get; set; } = "";

    public string OrderName { get; set; } = "";

    /// <summary>Harici sitenizde ödeme sonrası yönlendirme (opsiyonel).</summary>
    public string? SuccessUrl { get; set; }

    /// <summary>Ödeme durumu değişince POST (isteğe bağlı; ApiClient varsayılanı kullanılır).</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Harici referans ID (opsiyonel).</summary>
    public string? ExternalReference { get; set; }

    public int? CustomerId { get; set; }

    public List<ExternalPaymentItemRequest>? Items { get; set; }
}

public class ExternalPaymentItemRequest
{
    [Required]
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}
