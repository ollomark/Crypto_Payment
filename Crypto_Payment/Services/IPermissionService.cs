using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface IPermissionService
{
    public Task<List<PermissionDto>> GetAllAsync();
    public Task<int> GetTotalCountAsync();

    public Task<PermissionDto> GetByIdAsync(int id);

    public Task<PermissionDto> CreateAsync(PermissionDto dto);

    public Task<PermissionDto> UpdateAsync(int id, PermissionDto dto);

    public Task DeleteAsync(int id);
}