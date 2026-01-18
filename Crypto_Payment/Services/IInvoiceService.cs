using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface IInvoiceService
{
    public Task<List<InvoiceDto>> GetAllAsync();

    public Task<InvoiceDto> GetByIdAsync(int id);

    public Task<InvoiceDto> CreateAsync(InvoiceDto dto);

    public Task<InvoiceDto> UpdateAsync(int id, InvoiceDto dto);

    public Task DeleteAsync(int id);
    
    public Task UpdateStatusAsync(int id, string status);
    
    public Task<InvoiceDto?> GetByTxnIdAsync(string txnId);
}
