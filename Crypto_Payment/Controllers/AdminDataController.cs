using Crypto_Payment.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

/// <summary>
/// GEÇİCİ — Veritabanı inceleme endpoint'i. İnceleme bittikten sonra silinecek.
/// </summary>
[Authorize(Roles = "MasterAdmin")]
[ApiController]
[Route("api/admin-data")]
public class AdminDataController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminDataController(AppDbContext db) => _db = db;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        // ... summary endpoint (geçici)
        return Ok(new { message = "Use /api/admin-data/cleanup for data reset" });
    }

    /// <summary>
    /// Test verilerini temizle — Müşteriler HARİÇ
    /// ApprovalRequests, SystemLogs, InvoiceItems, Invoices silinir
    /// </summary>
    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup()
    {
        var results = new Dictionary<string, int>();

        // 1. InvoiceItems (FK bağımlılığı yüzünden önce bunlar)
        var invoiceItems = await _db.InvoiceItems.ToListAsync();
        _db.InvoiceItems.RemoveRange(invoiceItems);
        results["InvoiceItems"] = invoiceItems.Count;

        // 2. Invoices (soft-deleted dahil hepsi)
        var invoices = await _db.Invoices.IgnoreQueryFilters().ToListAsync();
        _db.Invoices.RemoveRange(invoices);
        results["Invoices"] = invoices.Count;

        // 3. ApprovalRequests
        var approvals = await _db.ApprovalRequests.ToListAsync();
        _db.ApprovalRequests.RemoveRange(approvals);
        results["ApprovalRequests"] = approvals.Count;

        // 4. SystemLogs
        var logs = await _db.SystemLogs.ToListAsync();
        _db.SystemLogs.RemoveRange(logs);
        results["SystemLogs"] = logs.Count;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Temizlik tamamlandı. Müşteriler korundu.",
            deleted = results,
            totalDeleted = results.Values.Sum()
        });
    }
}
