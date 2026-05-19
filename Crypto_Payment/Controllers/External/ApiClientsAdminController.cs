using Crypto_Payment.Data;
using Crypto_Payment.DTOS.ExternalApi;
using Crypto_Payment.Helpers;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers.External;

[Authorize(Roles = "MasterAdmin")]
[ApiController]
[Route("api/admin/api-clients")]
public class ApiClientsAdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public ApiClientsAdminController(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<ApiClientListItem>>> List()
    {
        var list = await _db.ApiClients
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Select(c => new ApiClientListItem
            {
                Id = c.Id,
                Name = c.Name,
                ApiKeyPrefix = c.ApiKeyPrefix,
                DefaultWebhookUrl = c.DefaultWebhookUrl,
                IsActive = c.IsActive,
                CreatedDate = c.CreatedDate
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<CreateApiClientResponse>> Create([FromBody] CreateApiClientRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        var apiKey = ApiKeyHelper.GenerateApiKey();
        var webhookSecret = ApiKeyHelper.GenerateWebhookSecret();

        var client = new ApiClient
        {
            Name = request.Name.Trim(),
            ApiKeyHash = ApiKeyHelper.HashApiKey(apiKey),
            ApiKeyPrefix = ApiKeyHelper.GetPrefix(apiKey),
            WebhookSecret = webhookSecret,
            DefaultWebhookUrl = string.IsNullOrWhiteSpace(request.DefaultWebhookUrl) ? null : request.DefaultWebhookUrl.Trim(),
            IsActive = true,
            CreatedByUserId = user?.Id
        };

        _db.ApiClients.Add(client);
        await _db.SaveChangesAsync();

        return Ok(new CreateApiClientResponse
        {
            Id = client.Id,
            Name = client.Name,
            ApiKey = apiKey,
            WebhookSecret = webhookSecret,
            DefaultWebhookUrl = client.DefaultWebhookUrl,
            ApiKeyPrefix = client.ApiKeyPrefix
        });
    }

    [HttpPost("{id:int}/regenerate-key")]
    public async Task<ActionResult<CreateApiClientResponse>> RegenerateKey(int id)
    {
        var client = await _db.ApiClients.FindAsync(id);
        if (client == null) return NotFound();

        var apiKey = ApiKeyHelper.GenerateApiKey();
        client.ApiKeyHash = ApiKeyHelper.HashApiKey(apiKey);
        client.ApiKeyPrefix = ApiKeyHelper.GetPrefix(apiKey);
        await _db.SaveChangesAsync();

        return Ok(new CreateApiClientResponse
        {
            Id = client.Id,
            Name = client.Name,
            ApiKey = apiKey,
            WebhookSecret = client.WebhookSecret,
            DefaultWebhookUrl = client.DefaultWebhookUrl,
            ApiKeyPrefix = client.ApiKeyPrefix,
            Message = "Yeni API anahtarı oluşturuldu; eski anahtar geçersizdir."
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var client = await _db.ApiClients.FindAsync(id);
        if (client == null) return NotFound();
        client.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(new { message = "API istemcisi devre dışı bırakıldı." });
    }
}
