using Crypto_Payment.DTOS;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crypto_Payment.Models;

namespace Crypto_Payment.Controllers;

[ApiController]
[Authorize(Roles = "MasterAdmin,Admin")]
[Route("api/mailing")]
public class MailingController : ControllerBase
{
    private readonly IMailingService _mailing;
    private readonly UserManager<User> _userManager;

    public MailingController(IMailingService mailing, UserManager<User> userManager)
    {
        _mailing = mailing;
        _userManager = userManager;
    }

    private string SiteBaseUrl()
    {
        return $"{Request.Scheme}://{Request.Host}";
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] string segment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return BadRequest(new { title = "segment gerekli (birthday_today | no_deposit)." });
        var s = segment.Trim().ToLowerInvariant();
        if (s is not ("birthday_today" or "no_deposit"))
            return BadRequest(new { title = "Geçersiz segment." });

        var preview = await _mailing.GetPreviewAsync(s, ct);
        return Ok(preview);
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] MailingSendRequestDto dto, CancellationToken ct)
    {
        if (dto == null)
            return BadRequest(new { title = "İstek gövdesi gerekli." });

        var s = (dto.Segment ?? "").Trim().ToLowerInvariant();
        if (s is not ("birthday_today" or "no_deposit"))
            return BadRequest(new { title = "Geçersiz segment." });

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _mailing.SendCampaignAsync(s, dto.Subject, dto.HtmlBody, user.Id, user.UserName, ip, SiteBaseUrl(), ct);
        return Ok(result);
    }
}
