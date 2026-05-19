using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class InvoiceItemManager : IInvoiceItemService
{
    private readonly AppDbContext _db;

    public InvoiceItemManager(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(int invoiceId, List<InvoiceItemDto> dtos)
    {
        var entities = dtos.Select(s => new InvoiceItem()
        {
            InvoiceId = invoiceId,
            ServiceName = s.ServiceName,
            ServiceDescription = s.ServiceDescription ?? "",
            Quantity = s.Quantity,
            Price = s.Price,
            Total = s.Quantity * s.Price,
            Status = true,
            CreatedDate = DateTime.UtcNow
        }).ToList();

        _db.InvoiceItems.AddRange(entities);
        await _db.SaveChangesAsync();
    }

    public async Task<List<InvoiceItemDto>> GetByInvoiceIdAsync(int invoiceId)
    {
        return await _db.InvoiceItems.Where(x => x.InvoiceId == invoiceId)
            .Select(x => new InvoiceItemDto
            {
                 Id = x.Id,
                 InvoiceId = x.InvoiceId,
                 ServiceName = x.ServiceName,
                 ServiceDescription = x.ServiceDescription,
                 Quantity = x.Quantity,
                 Price = x.Price,
                 Total = x.Total,
                 Status = x.Status
            })
            .ToListAsync();
    }
    
    public async Task<InvoiceItemDto> GetByIdAsync(int id)
    {
        var x = await _db.InvoiceItems.FirstOrDefaultAsync(c => c.Id == id);
        if (x == null) throw new KeyNotFoundException("Hizmet bulunamadı.");

        return new InvoiceItemDto
        {
            Id = x.Id,
            InvoiceId = x.InvoiceId,
            ServiceName = x.ServiceName,
            ServiceDescription = x.ServiceDescription,
            Quantity = x.Quantity,
            Price = x.Price,
            Total = x.Total,
            Status = x.Status
        };
    }
}