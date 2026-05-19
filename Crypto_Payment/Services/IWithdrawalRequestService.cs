using Crypto_Payment.Models;

namespace Crypto_Payment.Services;

public interface IWithdrawalRequestService
{
    Task<WithdrawalRequest> CreateLinkAsync(string adminId, string adminName, WithdrawalCategory category);
    Task<WithdrawalRequest?> GetByTokenAsync(Guid token);
    Task<WithdrawalRequest?> GetByIdAsync(int id);
    Task<List<WithdrawalRequest>> GetAllAsync(WithdrawalStatus? status = null);
    Task<List<WithdrawalRequest>> GetPendingForApprovalAsync();
    Task FillByCustomerAsync(Guid token, string customerName, string? companyName, decimal amount,
        WdPaymentMethod method, CryptoNetwork? network, string walletOrIban, string? ip, string? customerNote, string? currency = null);
    Task ApproveAsync(int id, string reviewedBy);
    Task RejectAsync(int id, string reviewedBy, string? note);
    Task<int> GetPendingCountAsync();
}
