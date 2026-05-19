using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class InvoiceManager : IInvoiceService
{
    private readonly AppDbContext _db;
    private readonly IPlisioService _plisioService;
    private readonly IInvoiceItemService _invoiceItemService;
    private readonly IExternalWebhookService _webhookService;

    public InvoiceManager(
        AppDbContext db,
        IPlisioService plisioService,
        IInvoiceItemService invoiceItemService,
        IExternalWebhookService webhookService)
    {
        _db = db;
        _plisioService = plisioService;
        _invoiceItemService = invoiceItemService;
        _webhookService = webhookService;
    }

    public async Task<List<InvoiceDto>> GetAllAsync()
    {
        return await _db.Invoices.Where(x => x.RegistrationStatus)
            .Include(x => x.Customer)
            .Select(x => new InvoiceDto
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
            Status = x.Status,
            TransactionId = x.TransactionId,
            RegistrationStatus = x.RegistrationStatus,
            CreatedDate = x.CreatedDate,
            IsRecurring = x.IsRecurring,
            RecurringDay = x.RecurringDay,
            Customer = x.Customer != null ? new CustomerDto
            {
                Id = x.Customer.Id,
                FirstName = x.Customer.FirstName,
                LastName = x.Customer.LastName,
                CompanyName = x.Customer.CompanyName,
                Phone = x.Customer.Phone,
                Email = x.Customer.Email
            } : null
        }).ToListAsync();
    }
    
    public async Task<int> GetTotalCountAsync()
    {
        return await _db.Invoices.Where(x => x.RegistrationStatus).CountAsync();
    }
    
    public async Task<IEnumerable<InvoiceDashboardDto>> GetTotalInvoiceByStatusAsync()
    {
        return await _db.Invoices
            .AsNoTracking()
            .Where(x => x.RegistrationStatus)
            .Select(x => new
            {
                Status = x.Status == InvoiceStatus.Completed || x.Status == "mismatch"
                    ? "paid"
                    : x.Status ?? InvoiceStatus.Pending,

                Amount = x.SourceAmount
            })
            .GroupBy(x => x.Status)
            .Select(g => new InvoiceDashboardDto
            {
                Status = g.Key,
                Currency = "ALL",
                Count = g.Count(),
                Total = (decimal)g.Sum(x => (double)x.Amount)
            })
            .ToListAsync();
    }
    
    public async Task<List<InvoiceDto>> GetRecentInvoicesAsync()
    {
        return await _db.Invoices
            .Where(x => x.RegistrationStatus)
            .Include(x => x.Customer)
            .OrderByDescending(i => i.Id)
            .Take(5)
            .Select(x => new InvoiceDto
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
                Status = x.Status,
                TransactionId = x.TransactionId,
                RegistrationStatus = x.RegistrationStatus,
                CreatedDate = x.CreatedDate,
                Customer = x.Customer == null ? null : new CustomerDto
                {
                    Id = x.Customer.Id,
                    FirstName = x.Customer.FirstName,
                    LastName = x.Customer.LastName,
                    CompanyName = x.Customer.CompanyName,
                    Email = x.Customer.Email
                }
            }).ToListAsync();
    }
    
    public async Task<InvoiceDto?> GetByIdAsync(int id)
    {
        var x = await _db.Invoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (x == null) return null;
        
        var invoiceItems = await _invoiceItemService.GetByInvoiceIdAsync(id);
        
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
            Status = x.Status,
            TransactionId = x.TransactionId,
            RegistrationStatus = x.RegistrationStatus,
            CreatedDate = x.CreatedDate,
            IsRecurring = x.IsRecurring,
            RecurringDay = x.RecurringDay,
            Customer = x.Customer != null ? new CustomerDto
            {
                Id = x.Customer.Id,
                FirstName = x.Customer.FirstName,
                LastName = x.Customer.LastName,
                Phone = x.Customer.Phone,
                Email = x.Customer.Email,
                CompanyName = x.Customer.CompanyName,
                Telegram = x.Customer.Telegram,
                Skype = x.Customer.Skype
            } : null,
            InvoiceItemsDto = invoiceItems
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
            Status = x.Status,
            TransactionId = x.TransactionId,
            RegistrationStatus = x.RegistrationStatus,
            CreatedDate = x.CreatedDate,
        };
    }

    public async Task<InvoiceDto> CreateAsync(InvoiceDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));
        
        if (dto.InvoiceItemsDto != null && dto.InvoiceItemsDto.Any())
        {
            var sum = dto.InvoiceItemsDto.Sum(i => i.Quantity * i.Price);
            if (Math.Abs(dto.SourceAmount - sum) > 0.01m)
                throw new InvalidOperationException("Fatura tutarı ile kalem toplamları eşleşmiyor.");
        }
        
        var plisio = await _plisioService.CreateInvoiceAsync(dto);
        if (!plisio.IsSuccess)
            throw new InvalidOperationException(plisio.ErrorMessage ?? "Plisio fatura oluşturma başarısız.");
        
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
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
                Status = "new",
                RegistrationStatus = true,
                CreatedDate = DateTime.UtcNow,
                IsRecurring = dto.IsRecurring,
                RecurringDay = dto.IsRecurring ? dto.RecurringDay : null,
                ApiClientId = dto.ApiClientId,
                ExternalReference = dto.ExternalReference,
                MerchantWebhookUrl = dto.MerchantWebhookUrl
            };
            
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();
            
            // 👇 AYRI METHOD ama AYNI CONTEXT
            if (dto.InvoiceItemsDto != null && dto.InvoiceItemsDto.Any())
            {
                await _invoiceItemService.CreateAsync(invoice.Id, dto.InvoiceItemsDto);
            }
            
            await tx.CommitAsync();
            dto.Id = invoice.Id;
            dto.InvoiceUrl = invoice.InvoiceUrl;
            dto.TxnId = invoice.TxnId;
            dto.Status = invoice.Status;
            dto.CreatedDate = invoice.CreatedDate;
            return dto;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
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
        invoice.IsRecurring = dto.IsRecurring;
        invoice.RecurringDay = dto.IsRecurring ? dto.RecurringDay : null;

        await _db.SaveChangesAsync();

        dto.Id = invoice.Id;
        dto.InvoiceUrl = invoice.InvoiceUrl;
        dto.TxnId = invoice.TxnId;
        dto.Status = invoice.Status;
        dto.RegistrationStatus = invoice.RegistrationStatus;
        dto.CreatedDate = invoice.CreatedDate;
        return dto;
    }
    
    public async Task UpdateStatusAsync(int id, string status)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");

        var previousStatus = invoice.Status;
        if (previousStatus == status) return;

        invoice.Status = status;
        await _db.SaveChangesAsync();

        if (invoice.ApiClientId.HasValue)
            await _webhookService.NotifyInvoiceStatusChangedAsync(id, previousStatus, status);
    }
    
    public async Task UpdateRegistrationStatusAsync(int id, bool registrationStatus)
    {
        var invoice = await _db.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");
        
        invoice.RegistrationStatus = registrationStatus;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");

        // Soft delete: fiziksel silme yerine RegistrationStatus'u false yap
        invoice.RegistrationStatus = false;
        await _db.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalAmountAsync()
    {
        return await _db.Invoices
            .Where(x => x.RegistrationStatus)
            .SumAsync(x => x.SourceAmount);
    }

    public async Task<decimal> GetPendingAmountAsync()
    {
        return await _db.Invoices
            .Where(x => x.RegistrationStatus && (x.Status == InvoiceStatus.Pending || x.Status == InvoiceStatus.New))
            .SumAsync(x => (decimal?)x.SourceAmount) ?? 0m;
    }

    public async Task<decimal> GetPaidAmountAsync()
    {
        return await _db.Invoices
            .Where(x => x.RegistrationStatus && (x.Status == InvoiceStatus.Completed || x.Status == "mismatch"))
            .SumAsync(x => (decimal?)x.SourceAmount) ?? 0m;
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _db.Invoices
            .Where(x => x.RegistrationStatus && (x.Status == InvoiceStatus.Pending || x.Status == InvoiceStatus.New))
            .CountAsync();
    }

    public async Task UpdateTxnAsync(int id, string txnId, string? invoiceUrl, string status)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");

        invoice.TxnId = txnId;
        invoice.InvoiceUrl = invoiceUrl;
        invoice.Status = status;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateTransactionIdAsync(int id, string transactionId)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
        if (invoice == null) throw new KeyNotFoundException("Fatura bulunamadı.");

        invoice.TransactionId = transactionId;
        await _db.SaveChangesAsync();
    }

    public async Task<MonthlyStatsDto> GetMonthlyStatsAsync(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var now = DateTime.UtcNow;
        var today = now.Day;
        var isCurrentMonth = (year == now.Year && month == now.Month);
        var isPastMonth = (year < now.Year || (year == now.Year && month < now.Month));

        var onetimeInvoices = await _db.Invoices
            .Where(x => x.RegistrationStatus && !x.IsRecurring
                && x.CreatedDate >= monthStart && x.CreatedDate < monthEnd)
            .Select(x => new { x.SourceAmount, x.Status })
            .ToListAsync();

        var recurringInvoices = await _db.Invoices
            .Where(x => x.RegistrationStatus && x.IsRecurring && x.CreatedDate < monthEnd)
            .Select(x => new { x.Id, x.SourceAmount, x.Status, x.RecurringDay, x.CreatedDate })
            .ToListAsync();

        var recurringIds = recurringInvoices.Select(r => r.Id).ToList();
        var recurringPaidViaLink = await _db.PaymentLinks
            .Where(p => recurringIds.Contains(p.InvoiceId)
                && (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && ((p.PaidForMonth.HasValue && p.PaidForYear.HasValue && p.PaidForMonth == month && p.PaidForYear == year)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        // Bu ay için iptal edilmiş link olan faturalar — Invoice fallback uygulanmaz (OverdueInvoices ile aynı mantık)
        var cancelledForMonth = await _db.PaymentLinks
            .Where(p => recurringIds.Contains(p.InvoiceId)
                && p.Status == "cancelled"
                && ((p.PaidForMonth == month && p.PaidForYear == year)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        // Ayrıca: Invoice.Status completed/mismatch ve fatura bu ayda oluşturulmuşsa, ilk ay ödemesi sayılır
        // Ancak bu ay için iptal edilmiş link varsa sayma
        var recurringPaidViaInvoice = recurringInvoices
            .Where(r => !recurringPaidViaLink.Contains(r.Id)
                && !cancelledForMonth.Contains(r.Id)
                && (r.Status == "completed" || r.Status == "mismatch" || r.Status == "manual")
                && r.CreatedDate >= monthStart && r.CreatedDate < monthEnd)
            .Select(r => r.Id)
            .ToList();

        var recurringPaidIds = recurringPaidViaLink.Union(recurringPaidViaInvoice).Distinct().ToList();

        var onetimePaid = onetimeInvoices.Where(x => x.Status == "completed" || x.Status == "mismatch" || x.Status == "manual").ToList();
        var onetimePending = onetimeInvoices.Where(x => x.Status != "completed" && x.Status != "mismatch" && x.Status != "manual" && x.Status != "cancelled").ToList();

        // Tek seferlik: Bu ayda PaymentLink ile ödenenler (başka ayda oluşturulmuş olabilir) — çift sayım önleme
        var onetimeIds = await _db.Invoices.Where(i => i.RegistrationStatus && !i.IsRecurring).Select(i => i.Id).ToListAsync();
        var onetimePaidViaLinkIds = onetimeIds.Count > 0
            ? await _db.PaymentLinks
                .Where(p => onetimeIds.Contains(p.InvoiceId)
                    && (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                    && ((p.PaidForMonth.HasValue && p.PaidForYear.HasValue && p.PaidForMonth == month && p.PaidForYear == year)
                        || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
                .Select(p => p.InvoiceId)
                .Distinct()
                .ToListAsync()
            : new List<int>();
        var onetimeCreatedThisMonthPaidIds = await _db.Invoices
            .Where(x => x.RegistrationStatus && !x.IsRecurring
                && x.CreatedDate >= monthStart && x.CreatedDate < monthEnd
                && (x.Status == "completed" || x.Status == "mismatch" || x.Status == "manual"))
            .Select(x => x.Id)
            .ToListAsync();
        var onetimePaidViaLinkExclIds = onetimePaidViaLinkIds.Except(onetimeCreatedThisMonthPaidIds).ToList();
        decimal onetimePaidViaLinkExclAmount = onetimePaidViaLinkExclIds.Count > 0
            ? (await _db.Invoices.Where(i => onetimePaidViaLinkExclIds.Contains(i.Id)).SumAsync(i => (decimal?)i.SourceAmount)) ?? 0m
            : 0m;

        int recPaidCount = recurringPaidIds.Count;
        decimal recPaidAmount = recurringInvoices.Where(r => recurringPaidIds.Contains(r.Id)).Sum(r => r.SourceAmount);

        var recUnpaid = recurringInvoices.Where(r => !recurringPaidIds.Contains(r.Id)).ToList();

        int overdueCount = 0;
        decimal overdueAmount = 0;
        int pendingCount = 0;
        decimal pendingAmount = 0;
        int upcomingCount = 0;
        decimal upcomingAmount = 0;

        foreach (var r in recUnpaid)
        {
            var dueDay = r.RecurringDay ?? 1;
            if (isPastMonth)
            {
                overdueCount++;
                overdueAmount += r.SourceAmount;
            }
            else if (isCurrentMonth)
            {
                if (dueDay < today)
                {
                    overdueCount++;
                    overdueAmount += r.SourceAmount;
                }
                else if (dueDay <= today + 7)
                {
                    upcomingCount++;
                    upcomingAmount += r.SourceAmount;
                    pendingCount++;
                    pendingAmount += r.SourceAmount;
                }
                else
                {
                    pendingCount++;
                    pendingAmount += r.SourceAmount;
                }
            }
            else
            {
                pendingCount++;
                pendingAmount += r.SourceAmount;
            }
        }

        pendingCount += onetimePending.Count;
        pendingAmount += onetimePending.Sum(x => x.SourceAmount);

        int totalInvoices = onetimeInvoices.Count + recurringInvoices.Count;
        decimal totalAmount = onetimeInvoices.Sum(x => x.SourceAmount) + recurringInvoices.Sum(x => x.SourceAmount);
        int totalPaid = onetimePaid.Count + recPaidCount + (onetimePaidViaLinkExclIds.Count > 0 ? onetimePaidViaLinkExclIds.Count : 0);
        decimal totalPaidAmount = onetimePaid.Sum(x => x.SourceAmount) + recPaidAmount + onetimePaidViaLinkExclAmount;
        double collectionRate = totalInvoices > 0 ? Math.Round((double)totalPaid / totalInvoices * 100, 1) : 0;

        return new MonthlyStatsDto
        {
            Year = year,
            Month = month,
            TotalInvoices = totalInvoices,
            TotalAmount = totalAmount,
            PaidAmount = totalPaidAmount,
            PaidCount = totalPaid,
            PendingAmount = pendingAmount,
            PendingCount = pendingCount,
            OverdueAmount = overdueAmount,
            OverdueCount = overdueCount,
            RecurringAmount = recurringInvoices.Sum(x => x.SourceAmount),
            RecurringCount = recurringInvoices.Count,
            OnetimeCount = onetimeInvoices.Count,
            CollectionRate = collectionRate,
            UpcomingCount = upcomingCount,
            UpcomingAmount = upcomingAmount
        };
    }
}
