using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[ApiController]
[Authorize(Roles = "MasterAdmin")]
[Route("/api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email ?? "",
                    FullName = u.FullName ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    u.IsActive,
                    u.CreatedDate,
                    Roles = roles,
                    Role = roles.FirstOrDefault() ?? "User"
                });
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAll");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        return Ok(roles);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "E-posta ve şifre zorunludur." });

        var user = new User
        {
            UserName = req.UserName ?? req.Email,
            Email = req.Email,
            FullName = req.FullName,
            PhoneNumber = req.PhoneNumber,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { message = string.Join(" ", errors), errors });
        }

        if (!string.IsNullOrEmpty(req.Role))
        {
            if (!await _roleManager.RoleExistsAsync(req.Role))
                await _roleManager.CreateAsync(new IdentityRole(req.Role));
            await _userManager.AddToRoleAsync(user, req.Role);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            PhoneNumber = user.PhoneNumber ?? "",
            user.IsActive,
            user.CreatedDate,
            Roles = roles,
            Role = roles.FirstOrDefault() ?? "User"
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

        user.FullName = req.FullName ?? user.FullName;
        user.Email = req.Email ?? user.Email;
        user.UserName = req.UserName ?? user.UserName;
        user.PhoneNumber = req.PhoneNumber ?? user.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

        if (!string.IsNullOrEmpty(req.Role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!await _roleManager.RoleExistsAsync(req.Role))
                await _roleManager.CreateAsync(new IdentityRole(req.Role));
            await _userManager.AddToRoleAsync(user, req.Role);
        }

        if (!string.IsNullOrEmpty(req.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwResult = await _userManager.ResetPasswordAsync(user, token, req.NewPassword);
            if (!pwResult.Succeeded)
                return BadRequest(new { message = "Şifre güncellenemedi: " + string.Join(" ", pwResult.Errors.Select(e => e.Description)) });
        }

        return Ok(new { message = "Kullanıcı güncellendi." });
    }

    [HttpPost("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        return Ok(new { isActive = user.IsActive, message = user.IsActive ? "Kullanıcı aktif edildi." : "Kullanıcı pasif edildi." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
            return BadRequest(new { message = "Kendi hesabınızı silemezsiniz." });

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "Kullanıcı silindi." });
    }
}

public record CreateUserRequest(string? FullName, string? UserName, string Email, string Password, string? PhoneNumber, string? Role);
public record UpdateUserRequest(string? FullName, string? UserName, string? Email, string? PhoneNumber, string? Role, string? NewPassword);
