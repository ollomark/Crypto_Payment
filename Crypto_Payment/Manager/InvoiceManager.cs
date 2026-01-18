using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

public class InvoiceManager : IInvoiceService
{
    private readonly AppDbContext _db;
    private readonly IPlisioService _plisioService;

    public InvoiceManager(AppDbContext db, IPlisioService plisioService)
    {
        _db = db;
        _plisioService = plisioService;
    }

    public async Task<List<InvoiceDto>> GetAllAsync()
    {
        return await _db.Invoices.Select(x => new InvoiceDto
        {
            Id = x.Id,
            SourceCurrency = x.SourceCurrency,
            SourceAmount = x.SourceAmount,
            OrderNumber = x.OrderNumber,
            Currency = x.Currency,
            Email = x.Email,
            OrderName = x.OrderName,
            CallbackUrl = x.CallbackUrl,
            CustomerId = x.CustomerId,
            InvoiceUrl = x.InvoiceUrl,
            TxnId = x.TxnId,
            Status = x.Status
        }).ToListAsync();
    }

    public async Task<InvoiceDto> GetByIdAsync(int id)
    {
        var x = await _db.Invoices.FirstOrDefaultAsync(c => c.Id == id);
        if (x == null) throw new KeyNotFoundException("Fatura bulunamadı.");
        
        return new InvoiceDto
        {
            Id = x.Id,
            SourceCurrency = x.SourceCurrency,
            SourceAmount = x.SourceAmount,
            OrderNumber = x.OrderNumber,
            Currency = x.Currency,
            Email = x.Email,
            OrderName = x.OrderName,
            CallbackUrl = x.CallbackUrl,
            CustomerId = x.CustomerId,
            InvoiceUrl = x.InvoiceUrl,
            TxnId = x.TxnId,
            Status = x.Status
        };
    }
    
    public async Task<InvoiceDto?> GetByTxnIdAsync(string txnId)
    {
        var x = await _db.Invoices.FirstOrDefaultAsync(c => c.TxnId == txnId);
        if (x == null) return null;
        
        return new InvoiceDto
        {
            Id = x.Id,
            SourceCurrency = x.SourceCurrency,
            SourceAmount = x.SourceAmount,
            OrderNumber = x.OrderNumber,
            Currency = x.Currency,
            Email = x.Email,
            OrderName = x.OrderName,
            CallbackUrl = x.CallbackUrl,
            CustomerId = x.CustomerId,
            InvoiceUrl = x.InvoiceUrl,
            TxnId = x.TxnId,
            Status = x.Status
        };
    }

    public async Task<InvoiceDto> CreateAsync(InvoiceDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));
        
        var plisio = await _plisioService.CreateInvoiceAsync(dto);
        if (!plisio.IsSuccess)
            throw new Exception(plisio.ErrorMessage);
        
        var invoice = new Invoice
        {
            SourceCurrency = dto.SourceCurrency,
            SourceAmount = dto.SourceAmount,
            OrderNumber = dto.OrderNumber,
            Currency = dto.Currency,
            Email = dto.Email,
            OrderName = dto.OrderName,
            CallbackUrl = dto.CallbackUrl,
            CustomerId = dto.CustomerId,
            InvoiceUrl = plisio.InvoiceUrl,
            TxnId = plisio.TxnId,
            Status = "pending"
        };
        
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        dto.Id = invoice.Id;
        dto.InvoiceUrl = invoice.InvoiceUrl;
        dto.TxnId = invoice.TxnId;
        dto.Status = invoice.Status;
        return dto;
    }

    public async Task<InvoiceDto> UpdateAsync(int id, InvoiceDto dto)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");

        invoice.SourceCurrency = dto.SourceCurrency;
        invoice.SourceAmount = dto.SourceAmount;
        invoice.OrderNumber = dto.OrderNumber;
        invoice.Currency = dto.Currency;
        invoice.Email = dto.Email;
        invoice.OrderName = dto.OrderName;
        invoice.CallbackUrl = dto.CallbackUrl;
        invoice.CustomerId = dto.CustomerId;

        await _db.SaveChangesAsync();

        dto.Id = invoice.Id;
        dto.InvoiceUrl = invoice.InvoiceUrl;
        dto.TxnId = invoice.TxnId;
        dto.Status = invoice.Status;
        return dto;
    }
    
    public async Task UpdateStatusAsync(int id, string status)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");
        
        invoice.Status = status;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");

        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();
    }
}
