using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class ExpenseManager : IExpenseService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationService _push;

    public ExpenseManager(AppDbContext db, IPushNotificationService push)
    {
        _db = db;
        _push = push;
    }

    public async Task<List<Expense>> GetAllAsync(string? status = null, ExpenseCategory? category = null, DateTime? from = null, DateTime? to = null, int? customerId = null)
    {
        var q = _db.Expenses.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(e => e.Status == status);
        if (category.HasValue) q = q.Where(e => e.Category == category.Value);
        if (from.HasValue) q = q.Where(e => e.CreatedDate >= from.Value);
        if (to.HasValue) q = q.Where(e => e.CreatedDate <= to.Value);
        if (customerId.HasValue) q = q.Where(e => e.CustomerId == customerId.Value);
        return await q.OrderByDescending(e => e.CreatedDate).ToListAsync();
    }

    public async Task<Expense?> GetByIdAsync(int id) => await _db.Expenses.FindAsync(id);

    public async Task<Expense> CreateAsync(Expense expense)
    {
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        if (expense.Status == "Pending")
            _ = _push.SendToMasterAdminsAsync("Yeni Gider Talebi", expense.Description ?? $"{expense.Amount} {expense.Currency}", "/approvals", "expense");
        return expense;
    }

    public async Task ApproveAsync(int id, string reviewedBy)
    {
        var e = await _db.Expenses.FindAsync(id) ?? throw new KeyNotFoundException("Gider bulunamadı.");
        e.Status = "Approved";
        e.ReviewedBy = reviewedBy;
        e.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int id, string reviewedBy, string? note)
    {
        var e = await _db.Expenses.FindAsync(id) ?? throw new KeyNotFoundException("Gider bulunamadı.");
        e.Status = "Rejected";
        e.ReviewedBy = reviewedBy;
        e.AdminNote = note;
        e.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalApprovedAsync() =>
        await _db.Expenses.Where(e => e.Status == "Approved").SumAsync(e => (decimal?)e.Amount) ?? 0m;

    public async Task<decimal> GetTotalApprovedAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        return await _db.Expenses
            .Where(e => e.Status == "Approved" && e.CreatedDate >= start && e.CreatedDate < end)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;
    }

    public async Task<List<Expense>> GetRecentAsync(int count = 10) =>
        await _db.Expenses.OrderByDescending(e => e.CreatedDate).Take(count).ToListAsync();
}
