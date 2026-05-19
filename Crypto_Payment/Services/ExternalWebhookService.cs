using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crypto_Payment.Data;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

public class ExternalWebhookService : IExternalWebhookService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ExternalWebhookService> _logger;

    public ExternalWebhookService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        ILogger<ExternalWebhookService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public Task NotifyInvoiceStatusChangedAsync(int invoiceId, string? previousStatus, string newStatus, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                await SendWebhookAsync(invoiceId, previousStatus, newStatus, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External webhook failed for invoice {InvoiceId}", invoiceId);
            }
        }, ct);
    }

    private async Task SendWebhookAsync(int invoiceId, string? previousStatus, string newStatus, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.ApiClient)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice?.ApiClientId == null) return;

        var webhookUrl = invoice.MerchantWebhookUrl;
        if (string.IsNullOrWhiteSpace(webhookUrl))
            webhookUrl = invoice.ApiClient?.DefaultWebhookUrl;

        if (string.IsNullOrWhiteSpace(webhookUrl)) return;

        var client = invoice.ApiClient;
        if (client == null || !client.IsActive) return;

        var payload = new
        {
            @event = MapEventName(newStatus),
            payment_id = invoice.Id,
            order_number = invoice.OrderNumber,
            external_reference = invoice.ExternalReference,
            txn_id = invoice.TxnId,
            previous_status = previousStatus,
            status = newStatus,
            amount = invoice.SourceAmount,
            source_currency = invoice.SourceCurrency,
            crypto_currency = invoice.Currency,
            transaction_id = invoice.TransactionId,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload);
        var signature = ComputeSignature(json, client.WebhookSecret);

        var http = _httpFactory.CreateClient("ExternalWebhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("X-Signature-SHA256", signature);
        request.Headers.TryAddWithoutValidation("X-Event-Type", payload.@event);
        request.Headers.TryAddWithoutValidation("User-Agent", "CryptoPayment-Webhook/1.0");

        var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Merchant webhook {Url} returned {Status}: {Body}",
                webhookUrl, (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
        }
        else
        {
            _logger.LogInformation(
                "Merchant webhook sent for invoice {InvoiceId} → {Status}",
                invoiceId, newStatus);
        }
    }

    private static string MapEventName(string status) => status.ToLowerInvariant() switch
    {
        "completed" or "mismatch" => "payment.completed",
        "expired" => "payment.expired",
        "cancelled" => "payment.cancelled",
        "pending" or "new" => "payment.pending",
        _ => "payment.updated"
    };

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
