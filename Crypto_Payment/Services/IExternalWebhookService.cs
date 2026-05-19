namespace Crypto_Payment.Services;

public interface IExternalWebhookService
{
    Task NotifyInvoiceStatusChangedAsync(int invoiceId, string? previousStatus, string newStatus, CancellationToken ct = default);
}
