using Crypto_Payment.Data;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

public class CryptoPaymentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CryptoPaymentWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    public CryptoPaymentWorker(IServiceScopeFactory scopeFactory, ILogger<CryptoPaymentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CryptoPaymentWorker started - checking every {Sec}s", CheckInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingPaymentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CryptoPaymentWorker error");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckPendingPaymentsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tronService = scope.ServiceProvider.GetRequiredService<ITronService>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();

        var pendingLinks = await db.PaymentLinks
            .Include(p => p.Invoice).ThenInclude(i => i!.Customer)
            .Include(p => p.PaymentMethod)
            .Where(p => (p.Status == "new" || p.Status == "pending")
                && p.CryptoWalletAddress != null
                && p.ExpectedAmount != null
                && p.CryptoCurrency != null
                && p.PaymentMethod != null && p.PaymentMethod.Type == "fast_crypto")
            .OrderBy(p => p.CreatedDate)
            .Take(20)
            .ToListAsync();

        if (!pendingLinks.Any()) return;

        // Bu cüzdana atanmış, tamamlanmış ödemelerin TxHash'leri (aynı transfer iki linke atanmasın)
        var usedTxHashes = await db.PaymentLinks
            .Where(p => p.CryptoWalletAddress != null
                && (p.Status == "completed" || p.Status == "mismatch")
                && p.ConfirmedTxHash != null
                && pendingLinks.Select(x => x.CryptoWalletAddress).Contains(p.CryptoWalletAddress))
            .Select(p => p.ConfirmedTxHash!)
            .Distinct()
            .ToListAsync();
        var excludeTxSet = new HashSet<string>(usedTxHashes, StringComparer.OrdinalIgnoreCase);

        foreach (var link in pendingLinks)
        {
            if (link.ExpiredDate.HasValue && link.ExpiredDate.Value < DateTime.UtcNow)
            {
                link.Status = "expired";
                _logger.LogInformation("PaymentLink {Id} expired", link.Id);
                continue;
            }

            try
            {
                var result = await tronService.CheckPaymentAsync(
                    link.CryptoWalletAddress!,
                    link.ExpectedAmount!.Value,
                    link.CryptoCurrency!,
                    link.CreatedDate.AddSeconds(-10), // small buffer
                    excludeTxSet
                );

                // Bu oturumda tamamlanan linklerin TxHash'ini ekle (aynı batch'te çakışma önlenir)
                if (result is { Found: true } && !string.IsNullOrEmpty(result.TxHash))
                    excludeTxSet.Add(result.TxHash);

                if (result is { Found: true, Confirmed: true })
                {
                    link.Status = result.Amount > link.ExpectedAmount!.Value ? "mismatch" : "completed"; // Fazla = mismatch
                    link.PaidDate = DateTime.UtcNow;
                    link.ConfirmedTxHash = result.TxHash;
                    link.ReceivedAmount = result.Amount;
                    link.TransactionId = result.TxHash;

                    if (link.Invoice != null && link.Invoice.Status != "completed" && link.Invoice.Status != "mismatch")
                    {
                        link.Invoice.Status = link.Status;
                        link.Invoice.TransactionId = result.TxHash;
                    }

                    _logger.LogInformation(
                        "Payment confirmed! Link={Id}, Invoice={InvId}, TxHash={Tx}, Amount={Amt} {Cur}",
                        link.Id, link.InvoiceId, result.TxHash, result.Amount, link.CryptoCurrency);

                    // Telegram bildirimi
                    try
                    {
                        var customerName = link.Invoice?.Customer != null
                            ? $"{link.Invoice.Customer.FirstName} {link.Invoice.Customer.LastName}"
                            : link.Invoice?.Email ?? "Bilinmeyen";
                        var invoiceName = link.Invoice?.OrderName ?? $"Fatura #{link.InvoiceId}";
                        var cur = link.CryptoCurrency == "USDT_TRC20" ? "USDT" : link.CryptoCurrency;

                        var msg = $"<b>Kripto Ödeme Onaylandı</b>\n\n" +
                                  $"<b>Fatura:</b> {invoiceName}\n" +
                                  $"<b>Müşteri:</b> {customerName}\n" +
                                  $"<b>Tutar:</b> {result.Amount:N2} {cur}\n" +
                                  $"<b>TX:</b> <a href=\"https://tronscan.org/#/transaction/{result.TxHash}\">{result.TxHash?[..12]}...</a>\n" +
                                  $"<b>Zaman:</b> {result.Timestamp:dd.MM.yyyy HH:mm}";

                        await telegramService.SendMessageAsync(msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Telegram notification failed for payment link {Id}", link.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking payment for link {Id}", link.Id);
            }
        }

        await db.SaveChangesAsync();
    }
}
