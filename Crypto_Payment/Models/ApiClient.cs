namespace Crypto_Payment.Models;

/// <summary>
/// Harici site entegrasyonu — API key ile kimlik doğrulama.
/// </summary>
public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>SHA-256 hash of full API key (hex).</summary>
    public string ApiKeyHash { get; set; } = "";
    /// <summary>İlk 12 karakter (cp_live_xxxx) — listede gösterim için.</summary>
    public string ApiKeyPrefix { get; set; } = "";
    /// <summary>Webhook imzası için gizli anahtar.</summary>
    public string WebhookSecret { get; set; } = "";
    /// <summary>Varsayılan merchant webhook URL (isteğe bağlı override).</summary>
    public string? DefaultWebhookUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
}
