using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

/// <summary>Periyodik olarak MasterAdmin'lere bekleyen onay/talep özeti push bildirimi gönderir.</summary>
public class PushNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PushNotificationWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);

    public PushNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<PushNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendPendingSummaryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PushNotificationWorker özet gönderirken hata");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task SendPendingSummaryAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetService<IPushNotificationService>();
        if (push == null) return;

        var approvalCount = await db.ApprovalRequests.CountAsync(a => a.Status == "Pending");
        var withdrawalCount = await db.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.CustomerFilled);
        var expenseCount = await db.Expenses.CountAsync(e => e.Status == "Pending");

        var total = approvalCount + withdrawalCount + expenseCount;
        if (total == 0) return;

        var parts = new List<string>();
        if (approvalCount > 0) parts.Add($"{approvalCount} onay");
        if (withdrawalCount > 0) parts.Add($"{withdrawalCount} çekim");
        if (expenseCount > 0) parts.Add($"{expenseCount} gider");
        var msg = "Bekleyen: " + string.Join(", ", parts);
        await push.SendToMasterAdminsAsync("Bekleyen Talepler", msg, "/approvals", "pending-summary");
    }
}
