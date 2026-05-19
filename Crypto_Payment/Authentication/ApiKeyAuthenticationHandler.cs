using System.Security.Claims;
using System.Text.Encodings.Web;
using Crypto_Payment.Data;
using Crypto_Payment.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crypto_Payment.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly AppDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? apiKey = null;

        if (Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerKey)
            && !string.IsNullOrWhiteSpace(headerKey))
        {
            apiKey = headerKey.ToString().Trim();
        }
        else if (Request.Headers.Authorization.Count > 0)
        {
            var auth = Request.Headers.Authorization.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                apiKey = auth["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrEmpty(apiKey) || !apiKey.StartsWith(ApiKeyHelper.KeyPrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var hash = ApiKeyHelper.HashApiKey(apiKey);
        var client = await _db.ApiClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ApiKeyHash == hash && c.IsActive);

        if (client == null)
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, client.Id.ToString()),
            new Claim(ClaimTypes.Name, client.Name),
            new Claim("ApiClientId", client.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
