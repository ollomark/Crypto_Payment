using Crypto_Payment.Models;

namespace Crypto_Payment.Services;

public interface IApprovalService
{
    Task<List<ApprovalRequest>> GetPendingAsync();
    Task<List<ApprovalRequest>> GetAllAsync(int page = 1, int pageSize = 20);
    Task<int> GetPendingCountAsync();
    Task<ApprovalRequest?> GetByIdAsync(int id);
    Task<List<ApprovalRequest>> GetByUserAsync(string userId);
    Task<ApprovalRequest> CreateAsync(ApprovalRequest request);
    Task ApproveAsync(int id, string reviewedBy);
    Task RejectAsync(int id, string reviewedBy, string? note);
}
