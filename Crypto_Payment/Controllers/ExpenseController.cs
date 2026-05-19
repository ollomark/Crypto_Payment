using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/expenses")]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ExpenseController> _logger;

    public ExpenseController(IExpenseService expenseService, AppDbContext db, UserManager<User> userManager, ILogger<ExpenseController> logger)
    {
        _expenseService = expenseService;
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    // Herkese açık talep formu submit — giriş gerekmez
    [AllowAnonymous]
    [HttpPost("public-request")]
    public async Task<IActionResult> PublicRequest([FromBody] PublicExpenseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RequesterName) || dto.Amount <= 0)
            return BadRequest(new { title = "Ad Soyad ve Tutar zorunludur." });

        var expense = new Expense
        {
            Amount = dto.Amount,
            Currency = dto.Currency ?? "USD",
            Category = dto.Category,
            Description = dto.Description ?? "",
            RequesterName = dto.RequesterName.Trim(),
            RequesterCompany = dto.RequesterCompany?.Trim(),
            Method = dto.Method ?? "Banka",
            PaymentNetwork = dto.PaymentNetwork?.Trim(),
            WalletAddress = dto.WalletAddress?.Trim(),
            BankInfo = dto.BankInfo?.Trim(),
            Status = "Pending",
            RequestedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        await _expenseService.CreateAsync(expense);

        // Audit log
        _db.SystemLogs.Add(new SystemLog
        {
            Action = "ExpenseRequest",
            TargetEntity = "Expense",
            TargetId = expense.Id.ToString(),
            NewValue = "Pending",
            Details = $"Talep: {dto.RequesterName}, Tutar: {dto.Amount} {dto.Currency}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Talebiniz alındı. İncelendikten sonra işleme alınacaktır." });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int? category,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? customerId)
    {
        ExpenseCategory? cat = category.HasValue ? (ExpenseCategory)category.Value : null;
        _logger.LogInformation("Expenses GetAll: status={Status}, category={Cat}, customerId={CustId}", status, cat, customerId);
        var list = await _expenseService.GetAllAsync(status, cat, from, to, customerId);
        _logger.LogInformation("Expenses GetAll: {Count} kayıt döndü", list.Count);
        return Ok(list.Select(e => new {
            e.Id, e.Amount, e.Currency, e.CustomerId,
            Category = e.Category.ToString(),
            CategoryId = (int)e.Category,
            e.Description, e.RequesterName, e.RequesterCompany,
            e.Method, e.PaymentNetwork, e.WalletAddress, e.BankInfo,
            e.Status, e.AdminNote, e.ReviewedBy,
            e.CreatedDate, e.ReviewedDate,
            requestedByIp = e.RequestedByIp
        }));
    }

    /// <summary>MasterAdmin: Doğrudan gider ekle (Gider Ekle butonu için).</summary>
    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        if (dto.Amount <= 0) return BadRequest(new { title = "Tutar 0'dan büyük olmalı." });

        var expense = new Expense
        {
            Amount = dto.Amount,
            Currency = dto.Currency ?? "EUR",
            Category = dto.Category,
            Description = dto.Description ?? "",
            RequesterName = dto.RequesterName ?? (user.FullName ?? user.UserName ?? user.Email ?? "Admin"),
            RequesterCompany = dto.RequesterCompany,
            Method = dto.Method ?? "Banka",
            PaymentNetwork = dto.PaymentNetwork,
            WalletAddress = dto.WalletAddress,
            BankInfo = dto.BankInfo,
            Status = "Pending",
            CustomerId = dto.CustomerId,
            RequestedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        await _expenseService.CreateAsync(expense);

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "ExpenseCreated",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "Expense",
            TargetId = expense.Id.ToString(),
            Details = $"Gider eklendi: {expense.Amount} {expense.Currency}, Kategori: {expense.Category}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { id = expense.Id, message = "Gider talebi oluşturuldu." });
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        if (!isMaster) return Forbid();

        await _expenseService.ApproveAsync(id, user.UserName ?? user.Email ?? "");

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "ExpenseApproved",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "Expense",
            TargetId = id.ToString(),
            OldValue = "Pending",
            NewValue = "Approved",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Gider onaylandı." });
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectDto dto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        if (!isMaster) return Forbid();

        await _expenseService.RejectAsync(id, user.UserName ?? user.Email ?? "", dto.Note);

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "ExpenseRejected",
            UserId = user.Id,
            UserName = user.UserName,
            TargetEntity = "Expense",
            TargetId = id.ToString(),
            OldValue = "Pending",
            NewValue = "Rejected",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Gider reddedildi." });
    }

    public record RejectDto(string? Note);
}

public record CreateExpenseDto(
    decimal Amount,
    string? Currency,
    ExpenseCategory Category,
    string? Description,
    string? RequesterName,
    string? RequesterCompany,
    string? Method,
    string? PaymentNetwork,
    string? WalletAddress,
    string? BankInfo,
    int? CustomerId
);

public record PublicExpenseDto(
    string RequesterName,
    string? RequesterCompany,
    decimal Amount,
    string? Currency,
    ExpenseCategory Category,
    string? Description,
    string? Method,
    string? PaymentNetwork,
    string? WalletAddress,
    string? BankInfo
);
