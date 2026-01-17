using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

public class CustomerManager : ICustomerService
{
    private readonly AppDbContext _db;

    public CustomerManager(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerDto> CreateAsync(CustomerDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));
        
        var customer = new Customer
        {
            FirstName   = dto.FirstName.Trim(),
            LastName    = dto.LastName.Trim(),
            Phone       = dto.Phone.Trim(),
            Email       = dto.Email?.Trim(),
            CompanyName = dto.CompanyName.Trim(),
            Telegram    = dto.Telegram.Trim(),
            Skype       = dto.Skype.Trim()
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        dto.Id = customer.Id;
        return dto;
    }

    public async Task<CustomerDto> UpdateAsync(int id, CustomerDto dto)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (customer == null) throw new KeyNotFoundException("Müşteri bulunamadı.");

        customer.FirstName   = dto.FirstName.Trim();
        customer.LastName    = dto.LastName.Trim();
        customer.Phone       = dto.Phone.Trim();
        customer.Email       = dto.Email?.Trim();
        customer.CompanyName = dto.CompanyName.Trim();
        customer.Telegram    = dto.Telegram.Trim();
        customer.Skype       = dto.Skype.Trim();

        await _db.SaveChangesAsync();

        dto.Id = customer.Id;
        return dto;
    }

    public async Task<List<CustomerDto>> GetAllAsync()
    {
        return await _db.Customers
            .Select(x => new CustomerDto
            {
                Id          = x.Id,
                FirstName   = x.FirstName,
                LastName    = x.LastName,
                Phone       = x.Phone,
                Email       = x.Email,
                CompanyName = x.CompanyName,
                Telegram    = x.Telegram,
                Skype       = x.Skype
            })
            .ToListAsync();
    }

    public async Task<CustomerDto> GetByIdAsync(int id)
    {
        var x = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (x == null) throw new KeyNotFoundException("Müşteri bulunamadı.");

        return new CustomerDto
        {
            Id          = x.Id,
            FirstName   = x.FirstName,
            LastName    = x.LastName,
            Phone       = x.Phone,
            Email       = x.Email,
            CompanyName = x.CompanyName,
            Telegram    = x.Telegram,
            Skype       = x.Skype
        };
    }

    public async Task DeleteAsync(int id)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (customer == null) throw new KeyNotFoundException("Müşteri bulunamadı.");

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
    }
}
