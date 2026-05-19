using Crypto_Payment.Models;

namespace Crypto_Payment.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetAllAsync(string? status = null, ExpenseCategory? category = null, DateTime? from = null, DateTime? to = null, int? customerId = null);
    Task<Expense?> GetByIdAsync(int id);
    Task<Expense> CreateAsync(Expense expense);
    Task ApproveAsync(int id, string reviewedBy);
    Task RejectAsync(int id, string reviewedBy, string? note);
    Task<decimal> GetTotalApprovedAsync();
    Task<decimal> GetTotalApprovedAsync(int year, int month);
    Task<List<Expense>> GetRecentAsync(int count = 10);
}
