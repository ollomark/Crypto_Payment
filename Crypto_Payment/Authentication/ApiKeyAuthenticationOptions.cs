using Microsoft.AspNetCore.Authentication;

namespace Crypto_Payment.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}
