using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS.ExternalApi;

public class ExternalPaymentResponse
{
    public int PaymentId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string? ExternalReference { get; set; }
    public string? TxnId { get; set; }
    public string Status { get; set; } = "";
    public string PaymentUrl { get; set; } = "";
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = "";
    public string CryptoCurrency { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class ExternalPaymentStatusResponse
{
    public int PaymentId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string? ExternalReference { get; set; }
    public string? TxnId { get; set; }
    public string Status { get; set; } = "";
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = "";
    public string CryptoCurrency { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateApiClientRequest
{
    [Required]
    public string Name { get; set; } = "";
    public string? DefaultWebhookUrl { get; set; }
}

public class CreateApiClientResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string? DefaultWebhookUrl { get; set; }
    public string ApiKeyPrefix { get; set; } = "";
    public string Message { get; set; } = "API anahtarını güvenli bir yerde saklayın; tekrar gösterilmez.";
}

public class ApiClientListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ApiKeyPrefix { get; set; } = "";
    public string? DefaultWebhookUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
