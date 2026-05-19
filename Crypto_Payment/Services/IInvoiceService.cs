using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface IInvoiceService
{
    public Task<List<InvoiceDto>> GetAllAsync();
    
    public Task<int> GetTotalCountAsync();
    
    public Task<List<InvoiceDto>> GetRecentInvoicesAsync();

    public Task<IEnumerable<InvoiceDashboardDto>> GetTotalInvoiceByStatusAsync();

    public Task<InvoiceDto?> GetByIdAsync(int id);

    public Task<InvoiceDto> CreateAsync(InvoiceDto dto);

    public Task<InvoiceDto> UpdateAsync(int id, InvoiceDto dto);

    public Task DeleteAsync(int id);
    
    public Task UpdateStatusAsync(int id, string status);
    
    public Task UpdateRegistrationStatusAsync(int id, bool registrationStatus);
    
    public Task<InvoiceDto?> GetByTxnIdAsync(string txnId);
    
    public Task<decimal> GetTotalAmountAsync();
    
    public Task<decimal> GetPendingAmountAsync();
    
    public Task<decimal> GetPaidAmountAsync();
    
    public Task<int> GetPendingCountAsync();
    
    public Task UpdateTxnAsync(int id, string txnId, string? invoiceUrl, string status);
    
    public Task UpdateTransactionIdAsync(int id, string transactionId);

    Task<MonthlyStatsDto> GetMonthlyStatsAsync(int year, int month);
}
