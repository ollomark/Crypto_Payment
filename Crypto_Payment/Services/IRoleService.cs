using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface IRoleService
{
    public Task<List<RoleDto>> GetAllAsync();
    
    public Task<int> GetTotalCountAsync();

    public Task<RoleDto> GetByIdAsync(string id);

    public Task<RoleDto> CreateAsync(RoleDto dto);

    public Task<RoleDto> UpdateAsync(string id, RoleDto dto);

    public Task DeleteAsync(string id);
}