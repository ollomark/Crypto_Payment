using Crypto_Payment.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Crypto_Payment.Middleware;

/// <summary>
/// Her authenticated request'te cookie claim'lerindeki rolleri DB ile karşılaştırır.
/// Farklıysa sign-out → sign-in yaparak cookie'yi yeniler ve redirect eder.
/// </summary>
public class RoleRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RoleRefreshMiddleware> _logger;

    public RoleRefreshMiddleware(RequestDelegate next, ILogger<RoleRefreshMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<User> userManager, SignInManager<User> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? "";
            
            // Sonsuz döngüyü önle: sadece sayfa isteklerinde çalış, API ve static dosyalarda çalışma
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var user = await userManager.GetUserAsync(context.User);
            if (user != null)
            {
                var dbRoles = await userManager.GetRolesAsync(user);
                
                // Cookie'deki tüm olası rol claim tiplerini kontrol et
                var cookieRoles = context.User.Claims
                    .Where(c => c.Type == ClaimTypes.Role || 
                                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                                c.Type == "role")
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var dbRoleSet = dbRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("RoleRefresh: User={User}, DB={DbRoles}, Cookie={CookieRoles}, Path={Path}",
                    user.Email, string.Join(",", dbRoles), string.Join(",", cookieRoles), path);

                if (!dbRoleSet.SetEquals(cookieRoles))
                {
                    _logger.LogWarning("RoleRefresh: Rol uyumsuzluğu! Sign-out/sign-in yapılıyor. User={User}", user.Email);
                    
                    // Eski cookie'yi tamamen sil
                    await signInManager.SignOutAsync();
                    
                    // Yeni cookie oluştur (DB'deki güncel rollerle)
                    await signInManager.SignInAsync(user, isPersistent: true);
                    
                    // GET isteklerinde redirect yap
                    if (context.Request.Method == "GET")
                    {
                        var redirectUrl = path + context.Request.QueryString;
                        _logger.LogInformation("RoleRefresh: Redirect → {Url}", redirectUrl);
                        context.Response.Redirect(redirectUrl);
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
