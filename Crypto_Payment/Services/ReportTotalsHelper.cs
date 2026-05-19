using Crypto_Payment.Data;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

/// <summary>Finance Report (report-data) ile AYNI mantık — Ay Sonu Karlılık = Report detay toplamları.</summary>
public static class ReportTotalsHelper
{
    /// <summary>Verilen ay için Tahsil ve Gider — report-data ile birebir aynı kaynaklar.</summary>
    public static async Task<(decimal Tahsil, decimal Gider)> GetTotalsAsync(AppDbContext db, IInvoiceService _, int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        // Manuel ödeme InvoiceId'leri (tek seferlik) — crypto gelirden çıkar
        var manualOneTimeIds = await db.PaymentLinks
            .Where(p => p.IsManual
                     && (p.Status == "completed" || p.Status == "manual" || p.Status == "mismatch")
                     && p.CreatedDate >= start && p.CreatedDate < end)
            .Join(db.Invoices.Where(i => !i.IsRecurring), pl => pl.InvoiceId, i => i.Id, (pl, i) => pl.InvoiceId)
            .Distinct()
            .ToListAsync();

        // Gelir 1: Tek seferlik (crypto) — report-data ile aynı
        var oneTimeSum = await db.Invoices
            .Where(i => i.RegistrationStatus && !i.IsRecurring
                     && (i.Status == "completed" || i.Status == "mismatch")
                     && i.CreatedDate >= start && i.CreatedDate < end
                     && !manualOneTimeIds.Contains(i.Id))
            .SumAsync(i => (decimal?)i.SourceAmount) ?? 0m;

        // Gelir 2: Recurring ödemeleri (PaymentLink — PaidForMonth/PaidDate) — report-data ile aynı
        var recurringIds = await db.Invoices
            .Where(i => i.RegistrationStatus && i.IsRecurring)
            .Select(i => i.Id)
            .ToListAsync();

        var recurringSum = recurringIds.Count > 0
            ? await db.PaymentLinks
                .Where(p => recurringIds.Contains(p.InvoiceId)
                        && (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                        && ((p.PaidForMonth.HasValue && p.PaidForYear.HasValue
                             && p.PaidForYear.Value * 12 + p.PaidForMonth.Value >= start.Year * 12 + start.Month
                             && p.PaidForYear.Value * 12 + p.PaidForMonth.Value < end.Year * 12 + end.Month)
                            || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= start && (p.PaidDate ?? p.CreatedDate) < end)))
                .Join(db.Invoices, pl => pl.InvoiceId, inv => inv.Id, (pl, inv) => inv.SourceAmount)
                .SumAsync()
            : 0m;

        // Gelir 3: Manuel ödemeler (tek seferlik) — report-data ile aynı
        var manualSum = await db.PaymentLinks
            .Where(p => p.IsManual
                     && (p.Status == "completed" || p.Status == "manual" || p.Status == "mismatch")
                     && p.CreatedDate >= start && p.CreatedDate < end)
            .Join(db.Invoices.Where(i => !i.IsRecurring), pl => pl.InvoiceId, i => i.Id, (pl, i) => i.SourceAmount)
            .SumAsync();

        // Gelir 4: CustomerCollections
        var collectionsSum = await db.CustomerCollections
            .Where(c => c.CreatedDate >= start && c.CreatedDate < end)
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        var tahsil = oneTimeSum + recurringSum + manualSum + collectionsSum;

        // Gider — report-data ile aynı (Expenses onaylı; StaffPayments yalnız ExpenseId=null eski kayıtlar)
        var expenseSum = await db.Expenses
            .Where(e => e.Status == "Approved" && e.CreatedDate >= start && e.CreatedDate < end)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var refundSum = await db.CustomerRefunds
            .Where(r => r.CreatedDate >= start && r.CreatedDate < end)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var staffSum = await db.StaffPayments
            .Where(p => p.ExpenseId == null && p.CreatedDate >= start && p.CreatedDate < end)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var gider = expenseSum + refundSum + staffSum;

        return (tahsil, gider);
    }
}
