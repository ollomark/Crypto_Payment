using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crypto_Payment.Models;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _push;
    private readonly UserManager<User> _userManager;

    public PushController(IPushNotificationService push, UserManager<User> userManager)
    {
        _push = push;
        _userManager = userManager;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult GetStatus()
    {
        return Ok(new { configured = false });
    }

    [Authorize]
    [HttpPost("test")]
    public async Task<IActionResult> SendTest()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        await _push.SendToUsersAsync(new[] { user.Id }, "Bildirimler Açıldı", "Onay, fatura ve taleplerden anında haberdar olacaksınız.", "/", "test");
        return Ok(new { success = true });
    }
}
