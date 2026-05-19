using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/withdrawal-requests")]
public class WithdrawalRequestController : ControllerBase
{
    private readonly IWithdrawalRequestService _service;
    private readonly IExpenseService _expenseService;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public WithdrawalRequestController(
        IWithdrawalRequestService service,
        IExpenseService expenseService,
        AppDbContext db,
        UserManager<User> userManager)
    {
        _service = service;
        _expenseService = expenseService;
        _db = db;
        _userManager = userManager;
    }

    // Admin: Yeni link oluştur
    [Authorize]
    [HttpPost("create-link")]
    public async Task<IActionResult> CreateLink([FromQuery] int category = 5)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var cat = Enum.IsDefined(typeof(WithdrawalCategory), category) ? (WithdrawalCategory)category : WithdrawalCategory.Diger;
        var wr = await _service.CreateLinkAsync(user.Id, user.FullName ?? user.UserName ?? user.Email ?? "", cat);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var link = $"{baseUrl}/secure-request/{wr.Token}";

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "WithdrawalLinkCreated",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "WithdrawalRequest",
            TargetId = wr.Id.ToString(),
            NewValue = wr.Token.ToString(),
            Details = $"Çekim linki oluşturuldu",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { id = wr.Id, token = wr.Token, link });
    }

    // Admin: Tüm talepleri listele
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? status)
    {
        WithdrawalStatus? s = status.HasValue ? (WithdrawalStatus)status.Value : null;
        var list = await _service.GetAllAsync(s);
        return Ok(list.Select(w => new
        {
            w.Id, w.Token,
            w.CreatedByAdminName,
            w.CustomerName, w.CompanyName,
            w.Amount,
            w.Currency,
            PaymentMethod = w.PaymentMethod?.ToString(),
            CryptoNetwork = w.CryptoNetwork?.ToString(),
            w.WalletAddressOrIban,
            Status = w.Status.ToString(),
            StatusId = (int)w.Status,
            Category = w.Category.ToString(),
            CategoryId = (int)w.Category,
            w.RequestCode,
            w.CustomerNote,
            w.RequestDate, w.FilledDate, w.ReviewedDate,
            w.ReviewedBy, w.AdminNote
        }));
    }

    // MasterAdmin: Onay bekleyenleri listele
    [Authorize(Roles = "MasterAdmin")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var list = await _service.GetPendingForApprovalAsync();
        return Ok(list.Select(w => new
        {
            w.Id, w.Token,
            w.CreatedByAdminName,
            w.CustomerName, w.CompanyName,
            w.Amount,
            PaymentMethod = w.PaymentMethod?.ToString(),
            CryptoNetwork = w.CryptoNetwork?.ToString(),
            w.WalletAddressOrIban,
            Status = w.Status.ToString(),
            Category = w.Category.ToString(),
            CategoryId = (int)w.Category,
            w.RequestCode,
            w.CustomerNote,
            w.RequestDate, w.FilledDate
        }));
    }

    // MasterAdmin: Onayla → Expense tablosuna da işle
    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var wr = await _service.GetByIdAsync(id);
        if (wr == null) return NotFound();
        if (wr.Status != WithdrawalStatus.CustomerFilled)
            return BadRequest(new { title = "Bu talep onay bekleyen durumda değil." });

        await _service.ApproveAsync(id, user.UserName ?? user.Email ?? "");

        // Expense tablosuna gider olarak işle → Net Kasa'dan düşülsün
        var expense = new Expense
        {
            Amount = wr.Amount ?? 0,
            Currency = wr.Currency ?? "USD",
            Category = ExpenseCategory.KarCekimi,
            Description = $"Çekim Talebi #{wr.Id} - {wr.CustomerName}",
            RequesterName = wr.CustomerName ?? "",
            RequesterCompany = wr.CompanyName,
            Method = wr.PaymentMethod == WdPaymentMethod.Kripto ? "Kripto" : "Banka",
            PaymentNetwork = wr.CryptoNetwork?.ToString(),
            WalletAddress = wr.PaymentMethod == WdPaymentMethod.Kripto ? wr.WalletAddressOrIban : null,
            BankInfo = wr.PaymentMethod == WdPaymentMethod.Banka ? wr.WalletAddressOrIban : null,
            Status = "Approved",
            ReviewedBy = user.UserName ?? user.Email ?? "",
            ReviewedDate = DateTime.UtcNow
        };
        await _expenseService.CreateAsync(expense);

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "WithdrawalApproved",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "WithdrawalRequest",
            TargetId = id.ToString(),
            OldValue = "CustomerFilled",
            NewValue = "Approved",
            Details = $"Tutar: {wr.Amount}, Müşteri: {wr.CustomerName}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Çekim talebi onaylandı ve gider olarak işlendi." });
    }

    // MasterAdmin: Reddet
    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] WithdrawalRejectDto dto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var wr = await _service.GetByIdAsync(id);
        if (wr == null) return NotFound();

        await _service.RejectAsync(id, user.UserName ?? user.Email ?? "", dto.Note);

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "WithdrawalRejected",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "WithdrawalRequest",
            TargetId = id.ToString(),
            OldValue = "CustomerFilled",
            NewValue = "Rejected",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Çekim talebi reddedildi." });
    }

    // Bekleyen çekim talebi sayısı
    [Authorize(Roles = "MasterAdmin")]
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount()
    {
        var count = await _service.GetPendingCountAsync();
        return Ok(new { count });
    }

    // MasterAdmin: Çekim talebini sil
    [Authorize(Roles = "MasterAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var wr = await _service.GetByIdAsync(id);
        if (wr == null) return NotFound();

        _db.WithdrawalRequests.Remove(wr);

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "WithdrawalDeleted",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "WithdrawalRequest",
            TargetId = id.ToString(),
            OldValue = wr.Status.ToString(),
            Details = $"Çekim talebi silindi - Müşteri: {wr.CustomerName}, Tutar: {wr.Amount}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Çekim talebi silindi." });
    }
}

public record WithdrawalRejectDto(string? Note);
