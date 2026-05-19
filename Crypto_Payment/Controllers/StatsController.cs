using Crypto_Payment.Data;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[Authorize]
[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IInvoiceService _invoiceService;
    private readonly IExpenseService _expenseService;

    public StatsController(AppDbContext db, IInvoiceService invoiceService, IExpenseService expenseService)
    {
        _db = db;
        _invoiceService = invoiceService;
        _expenseService = expenseService;
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> MonthlyStats([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        if (m < 1 || m > 12) return BadRequest("Geçersiz ay.");

        var stats = await _invoiceService.GetMonthlyStatsAsync(y, m);
        var monthlyExpenses = await _expenseService.GetTotalApprovedAsync(y, m);

        // Ay Sonu Karlılık: ReportTotalsHelper (Finance Report ile aynı, çift sayım yok)
        var (netKasaTahsil, netKasaGider) = await ReportTotalsHelper.GetTotalsAsync(_db, _invoiceService, y, m);
        var netKasa = netKasaTahsil - netKasaGider;

        return Ok(new
        {
            stats.Year,
            stats.Month,
            stats.TotalInvoices,
            stats.TotalAmount,
            stats.PaidAmount,
            stats.PaidCount,
            stats.PendingAmount,
            stats.PendingCount,
            stats.OverdueAmount,
            stats.OverdueCount,
            stats.RecurringAmount,
            stats.RecurringCount,
            stats.OnetimeCount,
            stats.CollectionRate,
            stats.UpcomingCount,
            stats.UpcomingAmount,
            TotalExpenses = monthlyExpenses,
            NetKasa = netKasa,
            netKasaTahsil,
            netKasaGider
        });
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpGet("financial-chart")]
    public async Task<IActionResult> FinancialChart([FromQuery] string period = "12months")
    {
        var now = DateTime.UtcNow;
        int months = period == "30days" ? 1 : 12;
        var startDate = period == "30days"
            ? now.AddDays(-29).Date
            : new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));

        if (period == "30days")
        {
            // Günlük bazda son 30 gün
            var days = Enumerable.Range(0, 30)
                .Select(i => now.AddDays(-29 + i).Date)
                .ToList();

            var incomeRaw = await _db.Invoices
                .Where(i => i.RegistrationStatus
                    && (i.Status == "completed" || i.Status == "mismatch")
                    && i.CreatedDate >= startDate)
                .GroupBy(i => i.CreatedDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(x => (decimal?)x.SourceAmount) ?? 0m })
                .ToListAsync();

            var expenseRaw = await _db.Expenses
                .Where(e => e.Status == "Approved" && e.CreatedDate >= startDate)
                .GroupBy(e => e.CreatedDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(x => (decimal?)x.Amount) ?? 0m })
                .ToListAsync();

            var labels = days.Select(d => d.ToString("dd MMM")).ToList();
            var income = days.Select(d => incomeRaw.FirstOrDefault(x => x.Date == d)?.Total ?? 0m).ToList();
            var expense = days.Select(d => expenseRaw.FirstOrDefault(x => x.Date == d)?.Total ?? 0m).ToList();

            return Ok(new { labels, income, expense });
        }
        else
        {
            // Aylık bazda son 12 ay — ReportTotalsHelper ile tutarlı (TotalAmount+Collections, Gider)
            var monthList = Enumerable.Range(0, 12)
                .Select(i => startDate.AddMonths(i))
                .ToList();

            var income = new List<decimal>();
            var expense = new List<decimal>();
            foreach (var m in monthList)
            {
                var (tahsil, gider) = await ReportTotalsHelper.GetTotalsAsync(_db, _invoiceService, m.Year, m.Month);
                income.Add(tahsil);
                expense.Add(gider);
            }

            var labels = monthList.Select(m => m.ToString("MMM yyyy")).ToList();
            return Ok(new { labels, income, expense });
        }
    }

    /// <summary>
    /// Seçili ay için gecikmiş faturaların listesi (MasterDashboard için).
    /// </summary>
    [Authorize(Roles = "MasterAdmin")]
    [HttpGet("overdue-invoices")]
    public async Task<IActionResult> OverdueInvoices([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        if (m < 1 || m > 12) return BadRequest("Geçersiz ay.");

        var monthStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var today = now.Day;
        var isCurrentMonth = (y == now.Year && m == now.Month);
        var isPastMonth = (y < now.Year || (y == now.Year && m < now.Month));

        var recurringInvoices = await _db.Invoices
            .Include(i => i.Customer)
            .Where(x => x.RegistrationStatus && x.IsRecurring && x.CreatedDate < monthEnd)
            .Select(x => new { x.Id, x.OrderName, x.SourceAmount, x.SourceCurrency, x.RecurringDay, x.CreatedDate, x.Status, x.Customer })
            .ToListAsync();

        var paidThisMonth = await _db.PaymentLinks
            .Where(p => (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && ((p.PaidForMonth == m && p.PaidForYear == y)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        // Bu ay için iptal edilmiş PaymentLink olan faturalar — Invoice fallback uygulanmaz (GetMonthlyHistory ile aynı mantık)
        var cancelledForMonth = await _db.PaymentLinks
            .Where(p => p.Status == "cancelled"
                && ((p.PaidForMonth == m && p.PaidForYear == y)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        // Yinelenen faturanın ilk ayı Invoice.Status ile ödenmiş olabilir (Plisio ana link, PaymentLink yok)
        // Ancak bu ay için iptal edilmiş link varsa sayma (fatura detayda gecikmiş gösteriliyorsa listede de göster)
        var paidViaInvoice = recurringInvoices
            .Where(r => !paidThisMonth.Contains(r.Id)
                && !cancelledForMonth.Contains(r.Id)
                && (r.Status == "completed" || r.Status == "mismatch")
                && r.CreatedDate >= monthStart && r.CreatedDate < monthEnd)
            .Select(r => r.Id)
            .ToList();
        var allPaidIds = paidThisMonth.Union(paidViaInvoice).Distinct().ToList();

        var unpaid = recurringInvoices.Where(r => !allPaidIds.Contains(r.Id)).ToList();
        var overdue = new List<object>();

        foreach (var r in unpaid)
        {
            var dueDay = r.RecurringDay ?? 1;
            var isOverdue = isPastMonth || (isCurrentMonth && dueDay < today);
            if (isOverdue)
            {
                var cName = r.Customer != null
                    ? (r.Customer.CompanyName ?? $"{r.Customer.FirstName} {r.Customer.LastName}".Trim())
                    : "";
                overdue.Add(new
                {
                    invoiceId = r.Id,
                    orderName = r.OrderName,
                    customerName = cName,
                    amount = r.SourceAmount,
                    currency = r.SourceCurrency ?? "EUR"
                });
            }
        }

        return Ok(overdue);
    }
}
