using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface ICustomerService
{
    public virtual Task<List<CustomerDto>> GetAllAsync()
        => throw new NotImplementedException();

    public virtual Task<CustomerDto> GetByIdAsync(int id)
        => throw new NotImplementedException();

    public virtual Task<CustomerDto> CreateAsync(CustomerDto dto)
        => throw new NotImplementedException();

    public virtual Task<CustomerDto> UpdateAsync(int id, CustomerDto dto)
        => throw new NotImplementedException();

    public virtual Task DeleteAsync(int id)
        => throw new NotImplementedException();
}

public interface IMerchantService
{
    public virtual Task<List<CustomerDto>> GetAllAsync()
        => throw new NotImplementedException();

    public virtual Task<CustomerDto> GetByIdAsync(int id)
        => throw new NotImplementedException();

    public virtual Task<CustomerDto> CreateAsync(CustomerDto dto)
        => throw new NotImplementedException();

    public virtual Task<CustomerDto> UpdateAsync(int id, CustomerDto dto)
        => throw new NotImplementedException();

    public virtual Task DeleteAsync(int id)
        => throw new NotImplementedException();
}