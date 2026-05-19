using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationCheckController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public NotificationCheckController(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Ok(new { approvals = 0, withdrawals = 0, expenses = 0, myRequests = 0 });
        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        var approvals = isMaster ? await _db.ApprovalRequests.CountAsync(a => a.Status == "Pending") : 0;
        var withdrawals = isMaster ? await _db.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.CustomerFilled) : 0;
        var expenses = isMaster ? await _db.Expenses.CountAsync(e => e.Status == "Pending") : 0;
        var myRequests = await _db.ApprovalRequests.CountAsync(a => a.RequestedBy == user.Id && a.Status == "Pending");
        return Ok(new { approvals, withdrawals, expenses, myRequests });
    }
}
