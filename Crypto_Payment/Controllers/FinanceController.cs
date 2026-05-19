using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[Authorize(Roles = "MasterAdmin")]
[Route("finance")]
public class FinanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInvoiceService _invoiceService;

    public FinanceController(AppDbContext db, IInvoiceService invoiceService)
    {
        _db = db;
        _invoiceService = invoiceService;
    }

    // Gelir/Gider Raporu sayfası
    [HttpGet("report")]
    public IActionResult Report() => View();

    // Personel Performans sayfası
    [HttpGet("performance")]
    public IActionResult Performance() => View();

    /// <summary>Ay Sonu Karlılık için — Finance Report ile aynı toplamlar, çift sayım yok.</summary>
    [HttpGet("api/report-totals")]
    public async Task<IActionResult> ReportTotals([FromQuery] int year, [FromQuery] int month)
    {
        var (tahsil, gider) = await ReportTotalsHelper.GetTotalsAsync(_db, _invoiceService, year, month);
        return Ok(new { tahsil, gider, netKasa = tahsil - gider });
    }

    // API: Gelir/Gider detay listesi (DataTables için)
    [HttpGet("api/report-data")]
    public async Task<IActionResult> ReportData(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? type)
    {
        var start = string.IsNullOrEmpty(startDate)
            ? DateTime.UtcNow.Date.AddMonths(-3)
            : DateTime.SpecifyKind(DateTime.Parse(startDate), DateTimeKind.Utc);
        var end = string.IsNullOrEmpty(endDate)
            ? DateTime.UtcNow.Date.AddDays(1)
            : DateTime.SpecifyKind(DateTime.Parse(endDate), DateTimeKind.Utc).AddDays(1);

        // Tek seferlik faturalar için manuel ödeme InvoiceId'leri — bunları crypto gelirden çıkarıyoruz (çift sayım önleme)
        var manualOneTimeInvoiceIds = await _db.PaymentLinks
            .Where(p => p.IsManual
                     && (p.Status == "completed" || p.Status == "manual" || p.Status == "mismatch")
                     && p.CreatedDate >= start && p.CreatedDate < end)
            .Join(_db.Invoices.Where(i => !i.IsRecurring), pl => pl.InvoiceId, i => i.Id, (pl, i) => pl.InvoiceId)
            .Distinct()
            .ToListAsync();

        // Gelir 1: Tek seferlik faturalar (crypto) — CreatedDate aralıkta, manuel olmayanlar
        var oneTimeIncomes = await _db.Invoices
            .Include(i => i.Customer)
            .Where(i => i.RegistrationStatus && !i.IsRecurring
                     && (i.Status == "completed" || i.Status == "mismatch")
                     && i.CreatedDate >= start && i.CreatedDate < end
                     && !manualOneTimeInvoiceIds.Contains(i.Id))
            .Select(i => new
            {
                Date        = i.CreatedDate,
                Type        = "Gelir",
                Category    = "Fatura",
                Description = i.OrderName,
                Amount      = i.SourceAmount,
                Currency    = i.SourceCurrency,
                Actor       = i.Customer != null ? (i.Customer.CompanyName ?? i.Customer.FirstName + " " + i.Customer.LastName) : i.Email,
                Status      = i.Status
            })
            .ToListAsync();

        // Gelir 2: Recurring faturaların bu dönemdeki ödemeleri (PaymentLink — PaidForMonth/PaidDate)
        var recurringIds = await _db.Invoices
            .Where(i => i.RegistrationStatus && i.IsRecurring)
            .Select(i => i.Id)
            .ToListAsync();

        var recurringPaymentLinks = recurringIds.Count > 0
            ? await _db.PaymentLinks
                .Include(p => p.Invoice!)
                .ThenInclude(i => i.Customer)
                .Where(p => recurringIds.Contains(p.InvoiceId)
                        && (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                        && ((p.PaidForMonth.HasValue && p.PaidForYear.HasValue
                             && p.PaidForYear.Value * 12 + p.PaidForMonth.Value >= start.Year * 12 + start.Month
                             && p.PaidForYear.Value * 12 + p.PaidForMonth.Value < end.Year * 12 + end.Month)
                            || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= start && (p.PaidDate ?? p.CreatedDate) < end)))
                .ToListAsync()
            : new List<PaymentLink>();

        var recurringIncomes = recurringPaymentLinks.Select(p =>
        {
            var inv = p.Invoice;
            var payDate = p.PaidDate ?? p.CreatedDate;
            return (object)new
            {
                Date        = payDate,
                Type        = "Gelir",
                Category    = p.IsManual ? "Manuel Ödeme" : "Fatura",
                Description = inv?.OrderName ?? "Fatura",
                Amount      = inv?.SourceAmount ?? 0m,
                Currency    = inv?.SourceCurrency ?? "EUR",
                Actor       = inv?.Customer != null ? (inv.Customer.CompanyName ?? inv.Customer.FirstName + " " + inv.Customer.LastName) : (inv?.Email ?? ""),
                Status      = "completed"
            };
        }).ToList();

        // Gelir 3: Manuel ödemeler (sadece tek seferlik faturalar — recurring olanlar zaten recurringIncomes'ta)
        var manualLinks = await _db.PaymentLinks
            .Include(p => p.Invoice!)
            .ThenInclude(i => i.Customer)
            .Where(p => p.IsManual
                     && (p.Status == "completed" || p.Status == "manual" || p.Status == "mismatch")
                     && p.CreatedDate >= start && p.CreatedDate < end
                     && p.Invoice != null && !p.Invoice.IsRecurring)
            .Select(p => new { p.InvoiceId, p.CreatedDate, p.Note, p.Invoice })
            .ToListAsync();

        var manualPayments = manualLinks.Select(m => new
        {
            Date        = m.CreatedDate,
            Type        = "Gelir",
            Category    = "Manuel Ödeme",
            Description = (m.Invoice?.OrderName ?? "Fatura") + (m.Note != null ? " — " + m.Note : ""),
            Amount      = m.Invoice?.SourceAmount ?? 0m,
            Currency    = m.Invoice?.SourceCurrency ?? "EUR",
            Actor       = m.Invoice?.Customer != null
                ? (m.Invoice.Customer.CompanyName ?? m.Invoice.Customer.FirstName + " " + m.Invoice.Customer.LastName)
                : (m.Invoice?.Email ?? ""),
            Status      = "completed"
        }).ToList();

        // Müşteri sayfasından eklenen manuel tahsilat (Gelir)
        var collections = await _db.CustomerCollections
            .Include(c => c.Customer)
            .Where(c => c.CreatedDate >= start && c.CreatedDate < end)
            .Select(c => new
            {
                Date        = c.CreatedDate,
                Type        = "Gelir",
                Category    = "Manuel Tahsilat",
                Description = string.IsNullOrWhiteSpace(c.Description) ? "Manuel tahsilat" : c.Description,
                Amount      = c.Amount,
                Currency    = c.Currency ?? "EUR",
                Actor       = c.Customer != null
                    ? (c.Customer.CompanyName ?? c.Customer.FirstName + " " + c.Customer.LastName)
                    : "",
                Status      = "completed"
            })
            .ToListAsync();

        // Müşteri sayfasından eklenen geri ödemeler (Gider)
        var refunds = await _db.CustomerRefunds
            .Include(r => r.Customer)
            .Where(r => r.CreatedDate >= start && r.CreatedDate < end)
            .Select(r => new
            {
                Date        = r.CreatedDate,
                Type        = "Gider",
                Category    = "Geri Ödeme",
                Description = string.IsNullOrWhiteSpace(r.Description) ? "Geri ödeme" : r.Description,
                Amount      = r.Amount,
                Currency    = r.Currency ?? "EUR",
                Actor       = r.Customer != null
                    ? (r.Customer.CompanyName ?? r.Customer.FirstName + " " + r.Customer.LastName)
                    : "",
                Status      = "completed"
            })
            .ToListAsync();

        // Personel ödemeleri (yalnız gider kaydı OLMAYAN eski kayıtlar — yeni kayıtlar Expenses üzerinden)
        var staffPayments = await _db.StaffPayments
            .Include(p => p.User)
            .Where(p => p.ExpenseId == null && p.CreatedDate >= start && p.CreatedDate < end)
            .Select(p => new
            {
                Date        = p.CreatedDate,
                Type        = "Gider",
                Category    = p.Type == StaffPaymentType.Avans ? "Avans" : "Maaş",
                Description = p.Description,
                Amount      = p.Amount,
                Currency    = p.Currency ?? "EUR",
                Actor       = p.User != null ? (p.User.FullName ?? p.User.Email ?? "") : "",
                Status      = "completed"
            })
            .ToListAsync();

        // Giderler (onaylı masraflar)
        var expenses = await _db.Expenses
            .Where(e => e.Status == "Approved" && e.CreatedDate >= start && e.CreatedDate < end)
            .Select(e => new
            {
                Date        = e.CreatedDate,
                Type        = "Gider",
                Category    = e.Category.ToString(),
                Description = e.Description,
                Amount      = e.Amount,
                Currency    = e.Currency,
                Actor       = e.RequesterName,
                Status      = e.Status
            })
            .ToListAsync();

        var allIncomes = oneTimeIncomes.Cast<object>()
            .Concat(recurringIncomes)
            .Concat(manualPayments.Cast<object>())
            .Concat(collections.Cast<object>())
            .ToList();
        var allExpenses = expenses.Cast<object>().Concat(refunds.Cast<object>()).Concat(staffPayments.Cast<object>()).ToList();

        var result = allIncomes.Concat(allExpenses.Cast<object>())
            .OrderByDescending(x => ((dynamic)x).Date)
            .ToList();

        if (type == "income") result = allIncomes.OrderByDescending(x => ((dynamic)x).Date).ToList();
        if (type == "expense") result = allExpenses.OrderByDescending(x => ((dynamic)x).Date).ToList();

        return Ok(result);
    }

    // API: Personel performans verisi
    [HttpGet("api/performance-data")]
    public async Task<IActionResult> PerformanceData()
    {
        // Her kullanıcının oluşturduğu faturaların toplamı (SystemLog üzerinden)
        var logs = await _db.SystemLogs
            .Where(l => l.Action == "InvoiceCreated" || l.Action == "InvoiceDeleted")
            .GroupBy(l => new { l.UserId, l.UserName })
            .Select(g => new
            {
                UserId   = g.Key.UserId,
                UserName = g.Key.UserName,
                Created  = g.Count(l => l.Action == "InvoiceCreated"),
                Deleted  = g.Count(l => l.Action == "InvoiceDeleted")
            })
            .ToListAsync();

        // Fatura tutarlarını kullanıcı bazında hesapla (CreatedBy alanı yoksa log'dan çek)
        var invoiceSums = await _db.Invoices
            .Where(i => i.Status == "completed" || i.Status == "mismatch")
            .GroupBy(i => 1)
            .Select(g => new { Total = g.Sum(i => i.SourceAmount) })
            .FirstOrDefaultAsync();

        return Ok(new { logs, totalPaid = invoiceSums?.Total ?? 0 });
    }
}
