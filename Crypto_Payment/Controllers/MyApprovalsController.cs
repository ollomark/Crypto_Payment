using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crypto_Payment.Models;

namespace Crypto_Payment.Controllers;

// Tüm giriş yapmış kullanıcıların erişebildiği onay endpoint'leri
[Authorize]
[ApiController]
[Route("api/my-approvals")]
public class MyApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly UserManager<User> _userManager;

    public MyApprovalsController(IApprovalService approvalService, UserManager<User> userManager)
    {
        _approvalService = approvalService;
        _userManager = userManager;
    }

    // Mevcut kullanıcının rol bilgisini döndür (debug)
    [HttpGet("whoami")]
    public async Task<IActionResult> WhoAmI()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var dbRoles = await _userManager.GetRolesAsync(user);
        
        // Cookie'deki claim'leri de döndür
        var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        var cookieRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                        c.Type == "role")
            .Select(c => c.Value).ToList();
        
        var isInRoleMaster = User.IsInRole("MasterAdmin");
        
        return Ok(new
        {
            userId         = user.Id,
            email          = user.Email,
            userName       = user.UserName,
            fullName       = user.FullName,
            isActive       = user.IsActive,
            emailConfirmed = user.EmailConfirmed,
            dbRoles        = dbRoles,
            cookieRoles    = cookieRoles,
            isInRoleMasterAdmin = isInRoleMaster,
            allClaims      = allClaims
        });
    }

    // Normal admin kendi taleplerini görür
    [HttpGet("my-requests")]
    public async Task<IActionResult> MyRequests()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var list = await _approvalService.GetByUserAsync(user.Id);
        return Ok(list);
    }

    // Bildirim çanı için: MasterAdmin gerçek sayı, diğerleri 0
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Ok(new { count = 0 });
        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        if (!isMaster) return Ok(new { count = 0 });
        var count = await _approvalService.GetPendingCountAsync();
        return Ok(new { count });
    }

    // Bildirim çanı listesi: sadece MasterAdmin görür
    [HttpGet("pending-list")]
    public async Task<IActionResult> PendingList()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Ok(new List<object>());
        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        if (!isMaster) return Ok(new List<object>());
        var list = await _approvalService.GetPendingAsync();
        return Ok(list);
    }
}
