using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Crypto_Payment.Controllers;

[ApiController]
[Authorize]
[Route("/api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    private readonly IApprovalService _approvalService;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService service,
        IApprovalService approvalService,
        AppDbContext db,
        UserManager<User> userManager,
        ILogger<CustomersController> logger)
    {
        _service = service;
        _approvalService = approvalService;
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    // LIST
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            _logger.LogInformation("Customers GetAll başlatıldı");
            var list = await _service.GetAllAsync();
            _logger.LogInformation("Customers GetAll tamamlandı, {Count} müşteri", list?.Count ?? 0);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customers GetAll hata: {Message}", ex.Message);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // TOTAL COUNT
    [HttpGet("GetTotalCustomerCount")]
    public async Task<IActionResult> GetTotalCount()
    {
        try
        {
            int totalCount = await _service.GetTotalCountAsync();
            return Ok(totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTotalCount");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    static string MapCurrency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "EUR";
        var c = code.ToUpperInvariant().Trim();
        if (c == "EURO" || c == "EU") return "EUR";
        if (c == "TL" || c == "TRY") return "TRY";
        return c;
    }

    /// <summary>Müşteri cari hareketleri: Tahsilat (ödenen faturalar), Geri ödemeler, Masraflar.</summary>
    [HttpGet("{id:int}/ledger")]
    public async Task<IActionResult> GetLedger(int id)
    {
        try
        {
            var customer = await _db.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { title = "Müşteri bulunamadı." });

            var invoices = await _db.Invoices
                .Where(i => i.CustomerId == id && i.RegistrationStatus)
                .OrderByDescending(i => i.CreatedDate)
                .Take(50)
                .Select(i => new { type = "tahsilat", id = i.Id, desc = i.OrderName, amount = i.SourceAmount, currency = i.SourceCurrency ?? "EUR", date = i.CreatedDate, status = i.Status })
                .ToListAsync();

            var collections = await _db.CustomerCollections
                .Where(c => c.CustomerId == id)
                .OrderByDescending(c => c.CreatedDate)
                .Take(50)
                .Select(c => new { type = "tahsilat", id = c.Id, desc = c.Description != "" ? c.Description : "Manuel tahsilat", amount = c.Amount, currency = c.Currency ?? "EUR", date = c.CreatedDate, status = (string?)"completed" })
                .ToListAsync();

            var refunds = await _db.CustomerRefunds
                .Where(r => r.CustomerId == id)
                .OrderByDescending(r => r.CreatedDate)
                .Take(50)
                .Select(r => new { type = "geri_odeme", id = r.Id, desc = r.Description, amount = -r.Amount, currency = r.Currency ?? "EUR", date = r.CreatedDate })
                .ToListAsync();

            var expenses = await _db.Expenses
                .Where(e => e.CustomerId == id)
                .OrderByDescending(e => e.CreatedDate)
                .Take(50)
                .Select(e => new { type = "masraf", id = e.Id, desc = e.Description, amount = -e.Amount, currency = e.Currency ?? "EUR", date = e.CreatedDate, status = e.Status })
                .ToListAsync();

            var all = invoices.Select(x => new { x.type, x.id, x.desc, x.amount, x.currency, x.date, status = (string?)x.status })
                .Concat(collections.Select(x => new { x.type, x.id, x.desc, x.amount, x.currency, x.date, status = (string?)x.status }))
                .Concat(refunds.Select(x => new { x.type, x.id, x.desc, x.amount, x.currency, x.date, status = (string?)null }))
                .Concat(expenses.Select(x => new { x.type, x.id, x.desc, x.amount, x.currency, x.date, x.status }))
                .OrderByDescending(x => x.date)
                .Take(50)
                .ToList();

            // Tahsilat: faturalar + manuel tahsilat, para birimine göre
            var invoiceByCur = await _db.Invoices
                .Where(i => i.CustomerId == id && i.RegistrationStatus && (i.Status == "completed" || i.Status == "mismatch"))
                .GroupBy(i => i.SourceCurrency ?? "EUR")
                .Select(g => new { Currency = g.Key, Sum = g.Sum(x => x.SourceAmount) })
                .ToListAsync();
            var collectionByCur = await _db.CustomerCollections
                .Where(c => c.CustomerId == id)
                .GroupBy(c => c.Currency ?? "EUR")
                .Select(g => new { Currency = g.Key, Sum = g.Sum(x => x.Amount) })
                .ToListAsync();
            var tahsilatByCurrency = new Dictionary<string, decimal> { {"EUR", 0m}, {"USD", 0m}, {"TRY", 0m} };
            foreach (var x in invoiceByCur) { var cur = MapCurrency(x.Currency); if (!tahsilatByCurrency.ContainsKey(cur)) tahsilatByCurrency[cur] = 0m; tahsilatByCurrency[cur] += x.Sum; }
            foreach (var x in collectionByCur) { var cur = MapCurrency(x.Currency); if (!tahsilatByCurrency.ContainsKey(cur)) tahsilatByCurrency[cur] = 0m; tahsilatByCurrency[cur] += x.Sum; }

            var refundByCur = await _db.CustomerRefunds.Where(r => r.CustomerId == id).GroupBy(r => r.Currency ?? "EUR").Select(g => new { Currency = g.Key, Sum = g.Sum(x => x.Amount) }).ToListAsync();
            var refundByCurrency = new Dictionary<string, decimal> { {"EUR", 0m}, {"USD", 0m}, {"TRY", 0m} };
            foreach (var x in refundByCur) { var c = MapCurrency(x.Currency); if (!refundByCurrency.ContainsKey(c)) refundByCurrency[c] = 0m; refundByCurrency[c] += x.Sum; }

            var masrafByCur = await _db.Expenses.Where(e => e.CustomerId == id && e.Status == "Approved").GroupBy(e => e.Currency ?? "EUR").Select(g => new { Currency = g.Key, Sum = g.Sum(x => x.Amount) }).ToListAsync();
            var masrafByCurrency = new Dictionary<string, decimal> { {"EUR", 0m}, {"USD", 0m}, {"TRY", 0m} };
            foreach (var x in masrafByCur) { var c = MapCurrency(x.Currency); if (!masrafByCurrency.ContainsKey(c)) masrafByCurrency[c] = 0m; masrafByCurrency[c] += x.Sum; }

            var bakiyeByCurrency = new Dictionary<string, decimal>();
            foreach (var cur in new[] { "EUR", "USD", "TRY" })
            {
                var t = tahsilatByCurrency.GetValueOrDefault(cur, 0m);
                var r = refundByCurrency.GetValueOrDefault(cur, 0m);
                var m = masrafByCurrency.GetValueOrDefault(cur, 0m);
                bakiyeByCurrency[cur] = t - r - m;
            }

            return Ok(new {
                tahsilatByCurrency,
                refundByCurrency,
                masrafByCurrency,
                bakiyeByCurrency,
                hareketler = all
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLedger CustomerId={CustomerId}", id);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    /// <summary>Müşteriye geri ödeme ekle.</summary>
    [HttpPost("{id:int}/refunds")]
    public async Task<IActionResult> AddRefund(int id, [FromBody] AddRefundDto dto)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
            if (!isMaster) return Forbid();

            var customer = await _db.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { title = "Müşteri bulunamadı." });
            if (dto.Amount <= 0) return BadRequest(new { title = "Tutar 0'dan büyük olmalı." });

            var refund = new CustomerRefund
            {
                CustomerId = id,
                Amount = dto.Amount,
                Currency = dto.Currency ?? "EUR",
                Description = dto.Description ?? "",
                Reference = dto.Reference,
                CreatedBy = user.UserName ?? user.Email
            };
            _db.CustomerRefunds.Add(refund);
            await _db.SaveChangesAsync();

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "CustomerRefundCreated",
                UserId = user.Id,
                UserName = user.UserName,
                TargetEntity = "CustomerRefund",
                TargetId = refund.Id.ToString(),
                Details = $"Müşteri #{id}: {dto.Amount} {refund.Currency} geri ödeme",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation("Geri ödeme eklendi: CustomerId={CustomerId}, RefundId={RefundId}, Amount={Amount}", id, refund.Id, dto.Amount);
            return Ok(new { id = refund.Id, message = "Geri ödeme eklendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddRefund CustomerId={CustomerId}", id);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    /// <summary>Müşteriye tahsilat ekle (manuel ödeme kaydı).</summary>
    [HttpPost("{id:int}/collections")]
    public async Task<IActionResult> AddCollection(int id, [FromBody] AddCollectionDto dto)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
            if (!isMaster) return Forbid();

            var customer = await _db.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { title = "Müşteri bulunamadı." });
            if (dto.Amount <= 0) return BadRequest(new { title = "Tutar 0'dan büyük olmalı." });

            var collection = new CustomerCollection
            {
                CustomerId = id,
                Amount = dto.Amount,
                Currency = dto.Currency ?? "EUR",
                Description = dto.Description ?? "",
                Reference = dto.Reference,
                CreatedBy = user.UserName ?? user.Email
            };
            _db.CustomerCollections.Add(collection);
            await _db.SaveChangesAsync();

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "CustomerCollectionCreated",
                UserId = user.Id,
                UserName = user.UserName,
                TargetEntity = "CustomerCollection",
                TargetId = collection.Id.ToString(),
                Details = $"Müşteri #{id}: {dto.Amount} {collection.Currency} tahsilat",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation("Tahsilat eklendi: CustomerId={CustomerId}, CollectionId={CollectionId}, Amount={Amount}", id, collection.Id, dto.Amount);
            return Ok(new { id = collection.Id, message = "Tahsilat eklendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddCollection CustomerId={CustomerId}", id);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // GET BY ID
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var customer = await _service.GetByIdAsync(id);
            return Ok(customer);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetById");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
    
    
    // CREATE
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Create");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // UPDATE — standart admin için onay kuyruğu
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDto dto)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            if (!isMaster)
            {
                // Müşteri bilgisini güvenli al (bulunamazsa description boş kalır)
                CustomerDto? customer = null;
                try { customer = await _service.GetByIdAsync(id); } catch { }

                var req = new ApprovalRequest
                {
                    RequestType = "CustomerUpdate",
                    RequestData = JsonSerializer.Serialize(new { CustomerId = id, Dto = dto }),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"Müşteri #{id} ({customer?.FirstName} {customer?.LastName}) düzenleme talebi",
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);

                _db.SystemLogs.Add(new SystemLog
                {
                    Action = "CustomerUpdateRequest",
                    UserId = user.Id, UserName = user.UserName,
                    TargetEntity = "Customer", TargetId = id.ToString(),
                    Details = $"Düzenleme onay talebi: {customer?.FirstName} {customer?.LastName}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _db.SaveChangesAsync();

                return Ok(new { pending = true, message = "Düzenleme talebiniz MasterAdmin onayına gönderildi." });
            }

            var updated = await _service.UpdateAsync(id, dto);

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "CustomerUpdated",
                UserId = user.Id, UserName = user.UserName,
                TargetEntity = "Customer", TargetId = id.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            return Ok(updated);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { title = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { title = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Update");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // DELETE — standart admin için onay kuyruğu
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            if (!isMaster)
            {
                CustomerDto? customer = null;
                try { customer = await _service.GetByIdAsync(id); } catch { }

                var req = new ApprovalRequest
                {
                    RequestType = "CustomerDelete",
                    RequestData = JsonSerializer.Serialize(new { CustomerId = id }),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"Müşteri #{id} ({customer?.FirstName} {customer?.LastName}) silme talebi",
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);

                _db.SystemLogs.Add(new SystemLog
                {
                    Action = "CustomerDeleteRequest",
                    UserId = user.Id, UserName = user.UserName,
                    TargetEntity = "Customer", TargetId = id.ToString(),
                    Details = $"Silme onay talebi: {customer?.FirstName} {customer?.LastName}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _db.SaveChangesAsync();

                return Ok(new { pending = true, message = "Silme talebiniz MasterAdmin onayına gönderildi." });
            }

            await _service.DeleteAsync(id);

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "CustomerDeleted",
                UserId = user.Id, UserName = user.UserName,
                TargetEntity = "Customer", TargetId = id.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { title = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Delete");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
}

public record AddRefundDto(decimal Amount, string? Currency, string? Description, string? Reference);
public record AddCollectionDto(decimal Amount, string? Currency, string? Description, string? Reference);
