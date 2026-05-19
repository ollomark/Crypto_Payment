using System.Text.Json.Nodes;
using Crypto_Payment.Data;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramBotService _bot;
    private readonly AppDbContext _db;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        ITelegramBotService bot,
        AppDbContext db,
        ILogger<TelegramWebhookController> logger)
    {
        _bot    = bot;
        _db     = db;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonObject update)
    {
        if (update == null) return Ok();
        _logger.LogInformation("Telegram webhook update alindi");
        await _bot.HandleUpdateAsync(update, _db);
        return Ok();
    }

    [HttpGet("setup")]
    public async Task<IActionResult> Setup()
    {
        var appUrl = Environment.GetEnvironmentVariable("APP_URL")
                     ?? "https://payment.wanda.to";
        var webhookUrl = $"{appUrl}/api/telegram/webhook";
        await _bot.RegisterWebhookAsync(webhookUrl);
        return Ok(new { message = "Webhook kayıt edildi.", url = webhookUrl });
    }
}
