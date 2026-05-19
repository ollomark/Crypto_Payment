using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class PermissionManager : IPermissionService
{
    private readonly AppDbContext _db;

    public PermissionManager(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PermissionDto> CreateAsync(PermissionDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));
        
        var permission = new Permission()
        {
            Name = dto.Name.Trim(),
            NormalizedName = dto.Name.Trim().ToUpperInvariant(),
            TopPermissionId = dto.TopPermissionId
        };

        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();

        dto.Id = permission.Id;
        return dto;
    }

    public async Task<PermissionDto> UpdateAsync(int id, PermissionDto dto)
    {
        var permission = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == id);
        if (permission == null) throw new KeyNotFoundException("Yetki bulunamadı.");

        permission.Name = dto.Name.Trim();
        permission.NormalizedName = dto.Name.Trim().ToUpperInvariant();
        permission.TopPermissionId = dto.TopPermissionId;

        await _db.SaveChangesAsync();

        dto.Id = permission.Id;
        return dto;
    }

    public async Task<List<PermissionDto>> GetAllAsync()
    {
        return await _db.Permissions
            .Include(x => x.TopPermission)
            .Select(x => new PermissionDto()
            { 
                Id = x.Id,
                Name = x.Name,
                TopPermissionId = x.TopPermissionId,
                TopPermissionName = x.TopPermission != null ? x.TopPermission.Name : null
            })
            .ToListAsync();
    }
    
    public async Task<int> GetTotalCountAsync()
    {
        return await _db.Permissions.CountAsync();
    }

    public async Task<PermissionDto> GetByIdAsync(int id)
    {
        var x = await _db.Permissions
            .Include(p => p.TopPermission)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (x == null) throw new KeyNotFoundException("Yetki bulunamadı.");

        return new PermissionDto()
        {
            Id = x.Id,
            Name = x.Name,
            TopPermissionId = x.TopPermissionId,
            TopPermissionName = x.TopPermission?.Name
        };
    }

    public async Task DeleteAsync(int id)
    {
        var permission = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == id);
        if (permission == null) throw new KeyNotFoundException("Yetki bulunamadı.");

        _db.Permissions.Remove(permission);
        await _db.SaveChangesAsync();
    }
}