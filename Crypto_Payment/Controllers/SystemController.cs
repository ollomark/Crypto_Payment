using Crypto_Payment.Data;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[Authorize(Roles = "MasterAdmin")]
[Route("system")]
public class SystemController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPlisioService _plisioService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(AppDbContext db, IPlisioService plisioService, ILogger<SystemController> logger)
    {
        _db = db;
        _plisioService = plisioService;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var results = new Dictionary<string, object>();
        var allOk = true;

        // 1. PostgreSQL bağlantısı
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            results["database"] = new { ok = canConnect, message = canConnect ? "Bağlantı başarılı" : "Bağlantı kurulamadı" };
            if (!canConnect) allOk = false;
        }
        catch (Exception ex)
        {
            results["database"] = new { ok = false, message = ex.Message };
            allOk = false;
        }

        // 2. ApprovalRequests tablosu yazılabilir mi
        try
        {
            var count = await _db.ApprovalRequests.CountAsync();
            results["approvalRequests"] = new { ok = true, message = $"Tablo erişilebilir, {count} kayıt" };
        }
        catch (Exception ex)
        {
            results["approvalRequests"] = new { ok = false, message = ex.Message };
            allOk = false;
        }

        // 3. Expenses tablosu
        try
        {
            var count = await _db.Expenses.CountAsync();
            results["expenses"] = new { ok = true, message = $"Tablo erişilebilir, {count} kayıt" };
        }
        catch (Exception ex)
        {
            results["expenses"] = new { ok = false, message = ex.Message };
            allOk = false;
        }

        // 4. Kritik view dosyaları
        var viewPaths = new[]
        {
            "Views/Home/Index.cshtml",
            "Views/Shared/_Layout.cshtml",
            "Views/Invoice/InvoiceList.cshtml",
            "Views/Customer/CustomerList.cshtml"
        };
        var missingViews = viewPaths.Where(p => !System.IO.File.Exists(System.IO.Path.Combine(Directory.GetCurrentDirectory(), p))).ToList();
        results["viewFiles"] = new { ok = missingViews.Count == 0, message = missingViews.Count == 0 ? "Tüm view dosyaları mevcut" : $"Eksik: {string.Join(", ", missingViews)}" };
        if (missingViews.Count > 0) allOk = false;

        // 5. Plisio API erişimi
        try
        {
            var plisioOk = await _plisioService.IsApiAvailableAsync();
            results["plisioApi"] = new { ok = plisioOk, message = plisioOk ? "API erişilebilir" : "API erişilemiyor" };
            if (!plisioOk) allOk = false;
        }
        catch (Exception ex)
        {
            results["plisioApi"] = new { ok = false, message = ex.Message };
            allOk = false;
        }

        results["timestamp"] = DateTime.UtcNow;
        results["overall"] = allOk ? "HEALTHY" : "DEGRADED";

        if (Request.Headers.Accept.ToString().Contains("json"))
            return Json(new { status = allOk ? "healthy" : "degraded", checks = results });

        ViewBag.Results = results;
        ViewBag.AllOk = allOk;
        return View();
    }
}
