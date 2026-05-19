using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[ApiController]
[Authorize(Roles = "MasterAdmin")]
[Route("api/personnel")]
public class PersonnelController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public PersonnelController(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>Personel rolündeki kullanıcılar + maaş profili.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPersonnel()
    {
        var personelRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Personel");
        if (personelRole == null)
            return Ok(Array.Empty<object>());

        var userIds = await _db.UserRoles
            .Where(ur => ur.RoleId == personelRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync();

        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.FullName ?? u.Email)
            .ToListAsync();

        var profiles = await _db.StaffProfiles
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p);

        var result = users.Select(u =>
        {
            profiles.TryGetValue(u.Id, out var prof);
            return new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.UserName,
                monthlySalary = prof?.MonthlySalary,
                salaryDayOfMonth = prof?.SalaryDayOfMonth
            };
        });

        return Ok(result);
    }

    /// <summary>Personel olmayan aktif kullanıcılar (ekleme listesi).</summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> GetCandidates()
    {
        var personelRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Personel");
        if (personelRole == null)
            return Ok(await _db.Users.Where(u => u.IsActive).Select(u => new { u.Id, u.FullName, u.Email, u.UserName }).ToListAsync());

        var personelIds = await _db.UserRoles
            .Where(ur => ur.RoleId == personelRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync();

        var list = await _db.Users
            .Where(u => u.IsActive && !personelIds.Contains(u.Id))
            .OrderBy(u => u.FullName ?? u.Email)
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Personel rolü ata ve isteğe bağlı maaş profili oluştur.</summary>
    [HttpPost("assign")]
    public async Task<IActionResult> AssignPersonnel([FromBody] AssignPersonnelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserId))
            return BadRequest(new { title = "Kullanıcı seçin." });

        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null || !user.IsActive)
            return BadRequest(new { title = "Kullanıcı bulunamadı veya pasif." });

        if (await _userManager.IsInRoleAsync(user, "Personel"))
            return BadRequest(new { title = "Bu kullanıcı zaten personel." });

        if (dto.SalaryDayOfMonth is < 1 or > 31)
            return BadRequest(new { title = "Maaş günü 1–31 arası olmalı." });

        var addRole = await _userManager.AddToRoleAsync(user, "Personel");
        if (!addRole.Succeeded)
            return BadRequest(new { title = string.Join(" ", addRole.Errors.Select(e => e.Description)) });

        if (dto.MonthlySalary.HasValue || dto.SalaryDayOfMonth.HasValue)
        {
            var prof = await _db.StaffProfiles.FindAsync(dto.UserId);
            if (prof == null)
            {
                prof = new StaffProfile { UserId = dto.UserId };
                _db.StaffProfiles.Add(prof);
            }
            if (dto.MonthlySalary.HasValue) prof.MonthlySalary = dto.MonthlySalary;
            if (dto.SalaryDayOfMonth.HasValue) prof.SalaryDayOfMonth = dto.SalaryDayOfMonth;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Personel eklendi." });
    }

    /// <summary>Personel rolünü kaldır (ödeme geçmişi ve gider kayıtları kalır).</summary>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> RemovePersonnel(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        if (!await _userManager.IsInRoleAsync(user, "Personel"))
            return BadRequest(new { title = "Bu kullanıcı personel değil." });

        var rem = await _userManager.RemoveFromRoleAsync(user, "Personel");
        if (!rem.Succeeded)
            return BadRequest(new { title = string.Join(" ", rem.Errors.Select(e => e.Description)) });

        var prof = await _db.StaffProfiles.FindAsync(userId);
        if (prof != null)
            _db.StaffProfiles.Remove(prof);

        await _db.SaveChangesAsync();
        return Ok(new { message = "Personel kaydı kaldırıldı." });
    }

    /// <summary>Maaş tutarı ve ödeme günü güncelle.</summary>
    [HttpPut("{userId}/profile")]
    public async Task<IActionResult> UpdateProfile(string userId, [FromBody] UpdateStaffProfileDto dto)
    {
        if (!await IsPersonnel(userId))
            return BadRequest(new { title = "Sadece personel için profil güncellenebilir." });

        if (dto.SalaryDayOfMonth is < 1 or > 31)
            return BadRequest(new { title = "Maaş günü 1–31 arası olmalı." });

        var prof = await _db.StaffProfiles.FindAsync(userId);
        if (prof == null)
        {
            prof = new StaffProfile { UserId = userId };
            _db.StaffProfiles.Add(prof);
        }

        prof.MonthlySalary = dto.MonthlySalary;
        prof.SalaryDayOfMonth = dto.SalaryDayOfMonth;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Profil güncellendi." });
    }

    /// <summary>Personelin ödeme geçmişi.</summary>
    [HttpGet("{userId}/payments")]
    public async Task<IActionResult> GetPayments(string userId)
    {
        var payments = await _db.StaffPayments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedDate)
            .Take(100)
            .Select(p => new
            {
                p.Id,
                p.Type,
                p.Amount,
                p.Currency,
                p.PeriodYear,
                p.PeriodMonth,
                p.Description,
                p.CreatedDate,
                p.PaymentDate,
                p.ExpenseId
            })
            .ToListAsync();

        return Ok(payments);
    }

    /// <summary>Avans ekle → onaylı gider kaydı oluşturulur.</summary>
    [HttpPost("{userId}/avans")]
    public async Task<IActionResult> AddAvans(string userId, [FromBody] StaffPaymentDto dto)
    {
        if (!await IsPersonnel(userId))
            return BadRequest(new { title = "Sadece personel rolündeki kullanıcılara avans eklenebilir." });

        if (dto.Amount <= 0)
            return BadRequest(new { title = "Tutar 0'dan büyük olmalı." });

        var employee = await _userManager.FindByIdAsync(userId);
        if (employee == null)
            return BadRequest();

        var now = DateTime.UtcNow;
        var expense = BuildApprovedExpense(employee, StaffPaymentType.Avans, dto.Amount, dto.Currency ?? "EUR",
            dto.Description ?? "Avans", null, null, now, User.Identity?.Name);
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        var payment = new StaffPayment
        {
            UserId = userId,
            Type = StaffPaymentType.Avans,
            Amount = dto.Amount,
            Currency = dto.Currency ?? "EUR",
            Description = dto.Description ?? "Avans",
            PaymentDate = now,
            CreatedDate = now,
            CreatedBy = User.Identity?.Name,
            ExpenseId = expense.Id
        };

        _db.StaffPayments.Add(payment);
        await _db.SaveChangesAsync();

        return Ok(new { id = payment.Id, expenseId = expense.Id, message = "Avans eklendi ve giderlere işlendi." });
    }

    /// <summary>Maaş ekle → onaylı gider kaydı oluşturulur.</summary>
    [HttpPost("{userId}/maas")]
    public async Task<IActionResult> AddMaas(string userId, [FromBody] StaffPaymentMaasDto dto)
    {
        if (!await IsPersonnel(userId))
            return BadRequest(new { title = "Sadece personel rolündeki kullanıcılara maaş eklenebilir." });

        if (dto.Amount <= 0)
            return BadRequest(new { title = "Tutar 0'dan büyük olmalı." });

        if (dto.Year < 2020 || dto.Year > 2100 || dto.Month < 1 || dto.Month > 12)
            return BadRequest(new { title = "Geçerli ay ve yıl girin." });

        var exists = await _db.StaffPayments.AnyAsync(p =>
            p.UserId == userId && p.Type == StaffPaymentType.Maas &&
            p.PeriodYear == dto.Year && p.PeriodMonth == dto.Month);

        if (exists)
            return BadRequest(new { title = $"Bu personel için {dto.Year}/{dto.Month:00} dönemi maaşı zaten kayıtlı." });

        var employee = await _userManager.FindByIdAsync(userId);
        if (employee == null)
            return BadRequest();

        var desc = string.IsNullOrWhiteSpace(dto.Description) ? $"{dto.Year}/{dto.Month:00} Maaş" : dto.Description!;
        var now = DateTime.UtcNow;
        var expense = BuildApprovedExpense(employee, StaffPaymentType.Maas, dto.Amount, dto.Currency ?? "EUR",
            desc, dto.Year, dto.Month, now, User.Identity?.Name);
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        var payment = new StaffPayment
        {
            UserId = userId,
            Type = StaffPaymentType.Maas,
            Amount = dto.Amount,
            Currency = dto.Currency ?? "EUR",
            PeriodYear = dto.Year,
            PeriodMonth = dto.Month,
            Description = desc,
            PaymentDate = now,
            CreatedDate = now,
            CreatedBy = User.Identity?.Name,
            ExpenseId = expense.Id
        };

        _db.StaffPayments.Add(payment);
        await _db.SaveChangesAsync();

        return Ok(new { id = payment.Id, expenseId = expense.Id, message = "Maaş eklendi ve giderlere işlendi." });
    }

    static Expense BuildApprovedExpense(User employee, StaffPaymentType type, decimal amount, string currency,
        string description, int? periodYear, int? periodMonth, DateTime atUtc, string? reviewedBy)
    {
        var name = employee.FullName ?? employee.Email ?? employee.UserName ?? employee.Id;
        var cat = type == StaffPaymentType.Avans ? ExpenseCategory.Avans : ExpenseCategory.Maas;
        var typeTr = type == StaffPaymentType.Avans ? "Avans" : "Maaş";
        var period = periodYear.HasValue && periodMonth.HasValue ? $" ({periodYear}/{periodMonth:00})" : "";
        return new Expense
        {
            Amount = amount,
            Currency = currency,
            Category = cat,
            Description = $"{typeTr}: {name}{period} — {description}",
            RequesterName = name,
            Method = "Banka",
            Status = "Approved",
            ReviewedBy = reviewedBy,
            ReviewedDate = atUtc,
            CreatedDate = atUtc
        };
    }

    async Task<bool> IsPersonnel(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        return await _userManager.IsInRoleAsync(user, "Personel");
    }
}

public class AssignPersonnelDto
{
    public string UserId { get; set; } = "";
    public decimal? MonthlySalary { get; set; }
    public int? SalaryDayOfMonth { get; set; }
}

public class UpdateStaffProfileDto
{
    public decimal? MonthlySalary { get; set; }
    public int? SalaryDayOfMonth { get; set; }
}

public class StaffPaymentDto
{
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
}

public class StaffPaymentMaasDto
{
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Description { get; set; }
}
