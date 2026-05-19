using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class ApprovalManager : IApprovalService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationService _push;

    public ApprovalManager(AppDbContext db, IPushNotificationService push)
    {
        _db = db;
        _push = push;
    }

    public async Task<List<ApprovalRequest>> GetPendingAsync() =>
        await _db.ApprovalRequests
            .Where(a => a.Status == "Pending")
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();

    public async Task<List<ApprovalRequest>> GetAllAsync(int page = 1, int pageSize = 20) =>
        await _db.ApprovalRequests
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<int> GetPendingCountAsync() =>
        await _db.ApprovalRequests.CountAsync(a => a.Status == "Pending");

    public async Task<ApprovalRequest?> GetByIdAsync(int id) =>
        await _db.ApprovalRequests.FindAsync(id);

    public async Task<List<ApprovalRequest>> GetByUserAsync(string userId) =>
        await _db.ApprovalRequests
            .Where(a => a.RequestedBy == userId)
            .OrderByDescending(a => a.CreatedDate)
            .Take(50)
            .ToListAsync();

    public async Task<ApprovalRequest> CreateAsync(ApprovalRequest request)
    {
        _db.ApprovalRequests.Add(request);
        await _db.SaveChangesAsync();
        _ = _push.SendToMasterAdminsAsync(
            "Yeni Onay Talebi",
            request.Description ?? request.RequestType,
            "/approvals",
            "approval");
        return request;
    }

    public async Task ApproveAsync(int id, string reviewedBy)
    {
        var req = await _db.ApprovalRequests.FindAsync(id)
            ?? throw new KeyNotFoundException("Onay talebi bulunamadı.");
        req.Status = "Approved";
        req.ReviewedBy = reviewedBy;
        req.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int id, string reviewedBy, string? note)
    {
        var req = await _db.ApprovalRequests.FindAsync(id)
            ?? throw new KeyNotFoundException("Onay talebi bulunamadı.");
        req.Status = "Rejected";
        req.ReviewedBy = reviewedBy;
        req.AdminNote = note;
        req.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
