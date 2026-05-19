using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface ICustomerService
{
    public Task<List<CustomerDto>> GetAllAsync();
    
    public Task<int> GetTotalCountAsync();

    public Task<CustomerDto> GetByIdAsync(int id);

    public Task<CustomerDto> CreateAsync(CustomerDto dto);

    public Task<CustomerDto> UpdateAsync(int id, CustomerDto dto);

    public Task DeleteAsync(int id);
}