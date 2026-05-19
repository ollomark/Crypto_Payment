using Crypto_Payment.DTOS;
using Crypto_Payment.Models;

namespace Crypto_Payment.Services;

public interface IInvoiceItemService
{
    public Task<List<InvoiceItemDto>> GetByInvoiceIdAsync(int invoiceId);

    public Task<InvoiceItemDto> GetByIdAsync(int id);

    public Task CreateAsync(int invoiceId, List<InvoiceItemDto> dtos);
}