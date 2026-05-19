using Crypto_Payment.Data;
using Crypto_Payment.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

public class InvoiceReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceReminderWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public InvoiceReminderWorker(IServiceScopeFactory scopeFactory, ILogger<InvoiceReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InvoiceReminderWorker başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InvoiceReminderWorker döngüsünde hata");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
        var plisioService = scope.ServiceProvider.GetRequiredService<IPlisioService>();
        var pushService = scope.ServiceProvider.GetService<IPushNotificationService>();

        await SyncPendingPaymentLinksAsync(db, plisioService);

        var now = DateTime.UtcNow;
        var today = now.Day;

        var recurringInvoices = await db.Invoices
            .Where(i => i.RegistrationStatus
                     && i.IsRecurring
                     && i.RecurringDay.HasValue)
            .Include(i => i.Customer)
            .ToListAsync();

        if (!recurringInvoices.Any()) return;

        // Bu ay ödenmiş yinelenen faturaları al (hatırlatma göndermemek için)
        var paidThisMonthIds = await GetPaidRecurringForMonthAsync(db, now.Year, now.Month);

        var remindersToSend = new List<(string CustomerName, string OrderName, decimal Amount, string Currency, int DayOfMonth, int DaysLeft)>();

        foreach (var invoice in recurringInvoices)
        {
            if (paidThisMonthIds.Contains(invoice.Id)) continue; // Bu ay ödendiyse hatırlatma gönderme

            var targetDay = invoice.RecurringDay!.Value;

            var alreadyRemindedThisMonth = invoice.LastReminderDate.HasValue
                && invoice.LastReminderDate.Value.Year == now.Year
                && invoice.LastReminderDate.Value.Month == now.Month;

            if (alreadyRemindedThisMonth) continue;

            int daysLeft;
            if (targetDay >= today)
            {
                daysLeft = targetDay - today;
            }
            else
            {
                var nextMonth = now.AddMonths(1);
                var maxDay = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                var effectiveDay = Math.Min(targetDay, maxDay);
                var nextDate = new DateTime(nextMonth.Year, nextMonth.Month, effectiveDay, 0, 0, 0, DateTimeKind.Utc);
                daysLeft = (int)(nextDate - now.Date).TotalDays;
            }

            if (daysLeft == 0)
            {
                var customerName = invoice.Customer != null
                    ? $"{invoice.Customer.FirstName} {invoice.Customer.LastName}"
                    : (invoice.Email ?? "-");

                remindersToSend.Add((customerName, invoice.OrderName, invoice.SourceAmount, invoice.SourceCurrency, targetDay, 0));

                invoice.LastReminderDate = now;
            }
            else if (daysLeft > 0 && daysLeft <= 3)
            {
                var customerName = invoice.Customer != null
                    ? $"{invoice.Customer.FirstName} {invoice.Customer.LastName}"
                    : (invoice.Email ?? "-");

                remindersToSend.Add((customerName, invoice.OrderName, invoice.SourceAmount, invoice.SourceCurrency, targetDay, daysLeft));

                invoice.LastReminderDate = now;
            }
        }

        if (remindersToSend.Any())
        {
            await db.SaveChangesAsync();

            var todayReminders = remindersToSend.Where(r => r.DaysLeft == 0).ToList();
            var upcomingReminders = remindersToSend.Where(r => r.DaysLeft > 0).ToList();

            if (todayReminders.Any())
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"<b>Bugün Vadesi Gelen Yinelenen Faturalar</b> ({now:dd.MM.yyyy})");
                sb.AppendLine();

                foreach (var r in todayReminders)
                {
                    sb.AppendLine($"<b>{r.OrderName}</b> — {r.Amount} {r.Currency}");
                    sb.AppendLine($"  {r.CustomerName}");
                    sb.AppendLine($"  Her ayın {r.DayOfMonth}. günü");
                    sb.AppendLine();
                }

                await telegramService.SendMessageAsync(sb.ToString());
                if (pushService != null)
                {
                    var msg = $"{todayReminders.Count} fatura bugün vadesinde: " + string.Join(", ", todayReminders.Take(3).Select(r => r.OrderName));
                    _ = pushService.SendToMasterAdminsAsync("Vadesi Gelen Faturalar", msg, "/invoices", "invoice-due");
                }
                _logger.LogInformation("{Count} yinelenen fatura hatırlatması gönderildi (bugün).", todayReminders.Count);
            }

            if (upcomingReminders.Any())
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"<b>Yaklaşan Yinelenen Faturalar</b> ({now:dd.MM.yyyy})");
                sb.AppendLine();

                foreach (var r in upcomingReminders)
                {
                    sb.AppendLine($"<b>{r.OrderName}</b> — {r.Amount} {r.Currency}");
                    sb.AppendLine($"  {r.CustomerName}");
                    sb.AppendLine($"  {r.DaysLeft} gün kaldı (Ayın {r.DayOfMonth}. günü)");
                    sb.AppendLine();
                }

                await telegramService.SendMessageAsync(sb.ToString());
                if (pushService != null)
                {
                    var msg = $"{upcomingReminders.Count} fatura yaklaşıyor: " + string.Join(", ", upcomingReminders.Take(3).Select(r => $"{r.OrderName} ({r.DaysLeft}g)"));
                    _ = pushService.SendToMasterAdminsAsync("Yaklaşan Faturalar", msg, "/invoices", "invoice-upcoming");
                }
                _logger.LogInformation("{Count} yinelenen fatura hatırlatması gönderildi (yaklaşan).", upcomingReminders.Count);
            }
        }

        if (now.Hour == 9 && now.Minute < 60)
        {
            await SendDailySummaryAsync(db, telegramService, now);
        }
    }

    private async Task SendDailySummaryAsync(AppDbContext db, ITelegramBotService telegram, DateTime now)
    {
        var today = now.Date;
        var m = now.Month;
        var y = now.Year;
        var monthStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var isCurrentMonth = true;
        var isPastMonth = false;

        var recurringInvoices = await db.Invoices
            .Where(i => i.RegistrationStatus && i.IsRecurring && i.RecurringDay.HasValue && i.CreatedDate < monthEnd)
            .Select(i => new { i.Id, i.RecurringDay, i.SourceAmount, i.SourceCurrency, i.Status, i.CreatedDate })
            .ToListAsync();

        var recurringIds = recurringInvoices.Select(r => r.Id).ToList();
        var paidThisMonth = await db.PaymentLinks
            .Where(p => recurringIds.Contains(p.InvoiceId)
                && (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && ((p.PaidForMonth == m && p.PaidForYear == y)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        var cancelledForMonth = await db.PaymentLinks
            .Where(p => recurringIds.Contains(p.InvoiceId)
                && p.Status == "cancelled"
                && ((p.PaidForMonth == m && p.PaidForYear == y)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        var paidViaInvoice = recurringInvoices
            .Where(r => !paidThisMonth.Contains(r.Id)
                && !cancelledForMonth.Contains(r.Id)
                && (r.Status == "completed" || r.Status == "mismatch")
                && r.CreatedDate >= monthStart && r.CreatedDate < monthEnd)
            .Select(r => r.Id)
            .ToList();
        var allPaidIds = paidThisMonth.Union(paidViaInvoice).Distinct().ToList();

        var unpaid = recurringInvoices.Where(r => !allPaidIds.Contains(r.Id)).ToList();
        var overdueInvoices = unpaid
            .Where(r => isPastMonth || (isCurrentMonth && (r.RecurringDay ?? 1) < now.Day))
            .ToList();

        var todayDue = unpaid.Count(r => (r.RecurringDay ?? 1) == now.Day);
        var paidCount = allPaidIds.Count;
        var totalRecurring = recurringInvoices.Count;
        var overdueAmount = overdueInvoices.Sum(r => r.SourceAmount);

        var msg = $"<b>Günlük Fatura Özeti</b> — {today:dd.MM.yyyy}\n\n" +
                  $"Yinelenen Faturalar: <b>{totalRecurring}</b>\n" +
                  $"Bu Ay Ödenen: <b>{paidCount}</b> ✅\n" +
                  $"Gecikmiş: <b>{overdueInvoices.Count}</b> ({overdueAmount:N2}) ⚠️\n" +
                  $"Bugün Vadesi Gelen: <b>{todayDue}</b>\n" +
                  $"Tahsilat Oranı: <b>{(totalRecurring > 0 ? Math.Round((double)paidCount / totalRecurring * 100, 1) : 0)}%</b>";

        await telegram.SendMessageAsync(msg);
    }

    private async Task SyncPendingPaymentLinksAsync(AppDbContext db, IPlisioService plisioService)
    {
        await CleanupBadBackfillLinksAsync(db);

        var pendingLinks = await db.PaymentLinks
            .Where(p => p.Status == "new" || p.Status == "pending")
            .Where(p => p.TxnId != null)
            .OrderBy(p => p.CreatedDate)
            .Take(50)
            .ToListAsync();

        if (!pendingLinks.Any()) return;

        int updated = 0;
        foreach (var link in pendingLinks)
        {
            try
            {
                var details = await plisioService.GetInvoiceDetailsAsync(link.TxnId!);
                if (details == null || string.IsNullOrEmpty(details.Status)) continue;

                var newStatus = StatusMapper.MapPlisioStatus(details.Status);
                if (newStatus == link.Status) continue;

                link.Status = newStatus;
                if (newStatus == "expired") link.ExpiredDate = DateTime.UtcNow;
                if (newStatus == "completed" || newStatus == "mismatch") link.PaidDate = DateTime.UtcNow;
                if (details.TxIds != null && details.TxIds.Count > 0 && string.IsNullOrEmpty(link.TransactionId))
                    link.TransactionId = details.TxIds[0];

                updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync PaymentLink {PlId} status", link.Id);
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Synced {Count} PaymentLink statuses from Plisio", updated);
        }
    }

    private async Task CleanupBadBackfillLinksAsync(AppDbContext db)
    {
        // Önceki hatalı backfill'in oluşturduğu PaymentLink kayıtlarını temizle:
        // Invoice.TxnId == PaymentLink.TxnId olan kayıtlar (faturanın kendi linki)
        // Bunlar gereksiz çünkü monthly-history artık Invoice.Status'ü doğrudan kontrol ediyor
        var paidRecurring = await db.Invoices
            .Where(i => i.RegistrationStatus && i.IsRecurring
                && (i.Status == "completed" || i.Status == "mismatch")
                && i.TxnId != null)
            .Select(i => new { i.Id, i.TxnId })
            .ToListAsync();

        if (!paidRecurring.Any()) return;

        var ids = paidRecurring.Select(r => r.Id).ToList();
        var backfillLinks = await db.PaymentLinks
            .Where(p => ids.Contains(p.InvoiceId))
            .ToListAsync();

        var toRemove = new List<Models.PaymentLink>();
        foreach (var inv in paidRecurring)
        {
            var dupeLink = backfillLinks.FirstOrDefault(p => p.InvoiceId == inv.Id && p.TxnId == inv.TxnId);
            if (dupeLink != null)
                toRemove.Add(dupeLink);
        }

        if (toRemove.Any())
        {
            db.PaymentLinks.RemoveRange(toRemove);
            await db.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} backfill PaymentLink records (Invoice.TxnId duplicates)", toRemove.Count);
        }
    }

    /// <summary>Bu ay ödenmiş yinelenen fatura ID'lerini döner (PaidForMonth/Year, PaidDate, cancelledForMonth, paidViaInvoice ile tutarlı)</summary>
    private static async Task<HashSet<int>> GetPaidRecurringForMonthAsync(AppDbContext db, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var recurring = await db.Invoices
            .Where(i => i.RegistrationStatus && i.IsRecurring && i.RecurringDay.HasValue && i.CreatedDate < monthEnd)
            .Select(i => new { i.Id, i.Status, i.CreatedDate })
            .ToListAsync();
        var ids = recurring.Select(r => r.Id).ToList();

        var paidViaLink = await db.PaymentLinks
            .Where(p => ids.Contains(p.InvoiceId)
                && (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && ((p.PaidForMonth == month && p.PaidForYear == year)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        var cancelled = await db.PaymentLinks
            .Where(p => ids.Contains(p.InvoiceId) && p.Status == "cancelled"
                && ((p.PaidForMonth == month && p.PaidForYear == year)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        var paidViaInv = recurring
            .Where(r => !paidViaLink.Contains(r.Id) && !cancelled.Contains(r.Id)
                && (r.Status == "completed" || r.Status == "mismatch")
                && r.CreatedDate >= monthStart && r.CreatedDate < monthEnd)
            .Select(r => r.Id)
            .ToList();

        return paidViaLink.Union(paidViaInv).ToHashSet();
    }
}
