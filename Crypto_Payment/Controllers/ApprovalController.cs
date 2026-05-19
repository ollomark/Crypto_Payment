using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Crypto_Payment.Controllers;

[Authorize(Roles = "MasterAdmin")]
[ApiController]
[Route("api/approvals")]
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly IInvoiceService _invoiceService;
    private readonly ICustomerService _customerService;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ApprovalController> _logger;

    public ApprovalController(
        IApprovalService approvalService,
        IInvoiceService invoiceService,
        ICustomerService customerService,
        AppDbContext db,
        UserManager<User> userManager,
        ILogger<ApprovalController> logger)
    {
        _approvalService = approvalService;
        _invoiceService = invoiceService;
        _customerService = customerService;
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var list = await _approvalService.GetPendingAsync();
        return Ok(list);
    }

    // Normal admin kendi taleplerini görebilir
    [Authorize]
    [HttpGet("my-requests")]
    public async Task<IActionResult> MyRequests()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var list = await _approvalService.GetByUserAsync(user.Id);
        return Ok(list);
    }

    // Tüm giriş yapmış kullanıcılar sayıyı görebilir (bildirim çanı için)
    [Authorize]
    [HttpGet("pending-count")]
    public async Task<IActionResult> GetPendingCount()
    {
        // Sadece MasterAdmin gerçek sayıyı görür, diğerleri 0 alır
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await _userManager.IsInRoleAsync(user, "MasterAdmin"))
            return Ok(new { count = 0 });
        var count = await _approvalService.GetPendingCountAsync();
        return Ok(new { count });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1)
    {
        var list = await _approvalService.GetAllAsync(page);
        return Ok(list);
    }

    // Sadece MasterAdmin onaylayabilir
    // Detay endpoint'i — RequestData JSON'ını ve mevcut fatura bilgisini döndür
    [HttpGet("{id}/detail")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var req = await _approvalService.GetByIdAsync(id);
        if (req == null) return NotFound();

        object? parsedData = null;
        object? currentInvoice = null;
        try
        {
            if (req.RequestType == "InvoiceEdit")
            {
                var data = JsonSerializer.Deserialize<InvoiceEditData>(req.RequestData);
                if (data != null)
                {
                    parsedData = data;
                    var inv = await _invoiceService.GetByIdAsync(data.Id);
                    if (inv != null)
                        currentInvoice = new { inv.Id, inv.OrderName, inv.SourceAmount, inv.SourceCurrency, inv.Status };
                }
            }
            else if (req.RequestType == "InvoiceStatusChange")
            {
                var data = JsonSerializer.Deserialize<InvoiceStatusChangeData>(req.RequestData);
                if (data != null)
                {
                    parsedData = data;
                    var inv = await _invoiceService.GetByIdAsync(data.InvoiceId);
                    if (inv != null)
                        currentInvoice = new { inv.Id, inv.OrderName, inv.SourceAmount, inv.SourceCurrency, inv.Status };
                }
            }
            else if (req.RequestType == "InvoiceDelete")
            {
                var data = JsonSerializer.Deserialize<InvoiceDeleteData>(req.RequestData);
                if (data != null)
                {
                    parsedData = data;
                    var inv = await _invoiceService.GetByIdAsync(data.InvoiceId);
                    if (inv != null)
                        currentInvoice = new { inv.Id, inv.OrderName, inv.SourceAmount, inv.SourceCurrency, inv.Status };
                }
            }
            else
            {
                // Genel JSON parse
                parsedData = JsonSerializer.Deserialize<object>(req.RequestData);
            }
        }
        catch { parsedData = req.RequestData; }

        return Ok(new
        {
            req.Id,
            req.RequestType,
            req.RequestedByName,
            req.Description,
            req.Status,
            req.AdminNote,
            req.CreatedDate,
            req.ReviewedDate,
            req.ReviewedBy,
            requestData = parsedData,
            currentInvoice
        });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        if (!isMaster) return Forbid();

        var req = await _approvalService.GetByIdAsync(id);
        if (req == null) return NotFound();

        // İşlemi gerçekleştir
        await ExecuteApprovalAction(req);

        await _approvalService.ApproveAsync(id, user.UserName ?? user.Email ?? "");

        // Audit log
        await AddLog("ApprovalApproved", user.Id, user.UserName, "ApprovalRequest", id.ToString(),
            "Pending", "Approved", $"Onaylandı: {req.Description}");

        return Ok(new { message = "Onaylandı." });
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectDto dto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        if (!isMaster) return Forbid();

        var req = await _approvalService.GetByIdAsync(id);
        if (req == null) return NotFound();

        await _approvalService.RejectAsync(id, user.UserName ?? user.Email ?? "", dto.Note);

        await AddLog("ApprovalRejected", user.Id, user.UserName, "ApprovalRequest", id.ToString(),
            "Pending", "Rejected", $"Reddedildi: {req.Description}. Not: {dto.Note}");

        return Ok(new { message = "Reddedildi." });
    }

    private async Task ExecuteApprovalAction(ApprovalRequest req)
    {
        try
        {
            switch (req.RequestType)
            {
                case "InvoiceStatusChange":
                {
                    var data = JsonSerializer.Deserialize<InvoiceStatusChangeData>(req.RequestData);
                    if (data != null)
                        await _invoiceService.UpdateStatusAsync(data.InvoiceId, data.NewStatus);
                    break;
                }
                case "InvoiceDelete":
                {
                    var data = JsonSerializer.Deserialize<InvoiceDeleteData>(req.RequestData);
                    if (data != null)
                        await _invoiceService.UpdateRegistrationStatusAsync(data.InvoiceId, false);
                    break;
                }
                case "CustomerUpdate":
                {
                    var data = JsonSerializer.Deserialize<CustomerUpdateData>(req.RequestData);
                    if (data?.Dto != null)
                        await _customerService.UpdateAsync(data.CustomerId, data.Dto);
                    break;
                }
                case "CustomerDelete":
                {
                    var data = JsonSerializer.Deserialize<CustomerDeleteData>(req.RequestData);
                    if (data != null)
                        await _customerService.DeleteAsync(data.CustomerId);
                    break;
                }
                case "InvoiceEdit":
                {
                    var data = JsonSerializer.Deserialize<InvoiceEditData>(req.RequestData);
                    if (data != null)
                    {
                        var dto = new DTOS.InvoiceDto
                        {
                            Id = data.Id,
                            OrderName = data.OrderName,
                            SourceAmount = data.SourceAmount,
                            SourceCurrency = data.SourceCurrency,
                            IsRecurring = data.IsRecurring ?? false,
                            RecurringDay = data.IsRecurring == true ? data.RecurringDay : null
                        };
                        await _invoiceService.UpdateAsync(data.Id, dto);
                    }
                    break;
                }
                case "PaymentCancel":
                {
                    var data = JsonSerializer.Deserialize<PaymentCancelData>(req.RequestData);
                    if (data != null)
                    {
                        var link = await _db.PaymentLinks.Include(p => p.Invoice).FirstOrDefaultAsync(p => p.Id == data.PaymentLinkId && p.InvoiceId == data.InvoiceId);
                        if (link != null && (link.Status == "completed" || link.Status == "mismatch"))
                        {
                            link.Status = "cancelled";
                            if (link.Invoice != null && !link.Invoice.IsRecurring)
                            {
                                var hasOtherPaid = await _db.PaymentLinks
                                    .AnyAsync(p => p.InvoiceId == data.InvoiceId && p.Id != data.PaymentLinkId && (p.Status == "completed" || p.Status == "mismatch"));
                                if (!hasOtherPaid) link.Invoice.Status = "pending";
                            }
                            await _db.SaveChangesAsync();
                        }
                    }
                    break;
                }
                case "ManualPayment":
                {
                    var data = JsonSerializer.Deserialize<ManualPaymentData>(req.RequestData);
                    if (data != null)
                    {
                        string[] mNames = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                        var monthLabel = $"{mNames[data.Month]} {data.Year}";
                        var monthStart = new DateTime(data.Year, data.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        var monthEnd = monthStart.AddMonths(1);
                        var alreadyPaid = await _db.PaymentLinks
                            .AnyAsync(p => p.InvoiceId == data.InvoiceId && p.IsManual
                                && (p.Status == "completed" || p.Status == "mismatch")
                                && ((p.PaidForMonth == data.Month && p.PaidForYear == data.Year)
                                    || (p.PaidForMonth == null && p.CreatedDate >= monthStart && p.CreatedDate < monthEnd)));

                        if (!alreadyPaid)
                        {
                            var noteWithMonth = string.IsNullOrEmpty(data.Note) ? monthLabel : $"{monthLabel} — {data.Note}";
                            _db.PaymentLinks.Add(new PaymentLink
                            {
                                InvoiceId = data.InvoiceId,
                                Status = "completed",
                                IsManual = true,
                                Note = noteWithMonth,
                                PaidForMonth = data.Month,
                                PaidForYear = data.Year,
                                CreatedDate = DateTime.UtcNow
                            });
                            await _db.SaveChangesAsync();
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Onay işlemi gerçekleştirilemedi: {Type}", req.RequestType);
        }
    }

    private async Task AddLog(string action, string? userId, string? userName, string? entity, string? entityId, string? oldVal, string? newVal, string? details)
    {
        _db.SystemLogs.Add(new SystemLog
        {
            Action = action,
            UserId = userId,
            UserName = userName,
            TargetEntity = entity,
            TargetId = entityId,
            OldValue = oldVal,
            NewValue = newVal,
            Details = details,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }

    public record RejectDto(string? Note);
    private record InvoiceStatusChangeData(int InvoiceId, string NewStatus);
    private record InvoiceDeleteData(int InvoiceId);
    private record CustomerUpdateData(int CustomerId, DTOS.CustomerDto? Dto);
    private record CustomerDeleteData(int CustomerId);
    private record InvoiceEditData(int Id, string OrderName, decimal SourceAmount, string SourceCurrency, string? Note, bool? IsRecurring = null, int? RecurringDay = null);
    private record ManualPaymentData(int InvoiceId, int Year, int Month, string? Note);
    private record PaymentCancelData(int InvoiceId, int PaymentLinkId);
}
