using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class WithdrawalRequestManager : IWithdrawalRequestService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationService _push;

    public WithdrawalRequestManager(AppDbContext db, IPushNotificationService push)
    {
        _db = db;
        _push = push;
    }

    public async Task<WithdrawalRequest> CreateLinkAsync(string adminId, string adminName, WithdrawalCategory category)
    {
        // Talep kodu: CT-YYYYMMDD-XXXX (rastgele 4 hane)
        var code = $"CT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var wr = new WithdrawalRequest
        {
            Token = Guid.NewGuid(),
            CreatedByAdminId = adminId,
            CreatedByAdminName = adminName,
            Category = category,
            RequestCode = code,
            Status = WithdrawalStatus.LinkCreated,
            RequestDate = DateTime.UtcNow
        };
        _db.WithdrawalRequests.Add(wr);
        await _db.SaveChangesAsync();
        return wr;
    }

    public async Task<WithdrawalRequest?> GetByTokenAsync(Guid token)
        => await _db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Token == token);

    public async Task<WithdrawalRequest?> GetByIdAsync(int id)
        => await _db.WithdrawalRequests.FindAsync(id);

    public async Task<List<WithdrawalRequest>> GetAllAsync(WithdrawalStatus? status = null)
    {
        var q = _db.WithdrawalRequests.AsQueryable();
        if (status.HasValue) q = q.Where(w => w.Status == status.Value);
        return await q.OrderByDescending(w => w.RequestDate).ToListAsync();
    }

    public async Task<List<WithdrawalRequest>> GetPendingForApprovalAsync()
        => await _db.WithdrawalRequests
            .Where(w => w.Status == WithdrawalStatus.CustomerFilled)
            .OrderByDescending(w => w.FilledDate)
            .ToListAsync();

    public async Task FillByCustomerAsync(Guid token, string customerName, string? companyName,
        decimal amount, WdPaymentMethod method, CryptoNetwork? network, string walletOrIban, string? ip, string? customerNote, string? currency = null)
    {
        var wr = await _db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Token == token)
            ?? throw new KeyNotFoundException("Talep bulunamadı.");

        if (wr.Status != WithdrawalStatus.LinkCreated)
            throw new InvalidOperationException("Bu link zaten kullanılmış.");

        wr.CustomerName = customerName;
        wr.CompanyName = companyName;
        wr.Amount = amount;
        wr.Currency = currency ?? "USD";
        wr.PaymentMethod = method;
        wr.CryptoNetwork = method == WdPaymentMethod.Kripto ? network : null;
        wr.WalletAddressOrIban = walletOrIban;
        wr.CustomerNote = customerNote;
        wr.Status = WithdrawalStatus.CustomerFilled;
        wr.FilledDate = DateTime.UtcNow;
        wr.FilledByIp = ip;
        await _db.SaveChangesAsync();
        _ = _push.SendToMasterAdminsAsync("Yeni Çekim Talebi", $"{customerName}: {amount} {wr.Currency}", "/withdrawal-requests", "withdrawal");
    }

    public async Task ApproveAsync(int id, string reviewedBy)
    {
        var wr = await _db.WithdrawalRequests.FindAsync(id)
            ?? throw new KeyNotFoundException("Talep bulunamadı.");
        wr.Status = WithdrawalStatus.Approved;
        wr.ReviewedBy = reviewedBy;
        wr.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int id, string reviewedBy, string? note)
    {
        var wr = await _db.WithdrawalRequests.FindAsync(id)
            ?? throw new KeyNotFoundException("Talep bulunamadı.");
        wr.Status = WithdrawalStatus.Rejected;
        wr.ReviewedBy = reviewedBy;
        wr.AdminNote = note;
        wr.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetPendingCountAsync()
        => await _db.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.CustomerFilled);
}
