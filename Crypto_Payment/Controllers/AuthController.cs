using System.Drawing;
using System.Drawing.Imaging;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IEmailSender _emailSender;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }
    
    
    [AllowAnonymous]
    [HttpGet("register")]
    public IActionResult Register() => View();

    [AllowAnonymous]
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([FromBody]RegisterVm model)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
            return BadRequest(new { title = "Bu email zaten kayıtlı." });

        var user = new User
        {
            FullName = model.FullName,
            UserName = model.UserName,
            Email = model.Email,
            TwoFactorEnabled = false // <-- KRİTİK
        };

        
        var create = await _userManager.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            // ApiController ile uyumlu error formatı
            var errors = create.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Description).ToArray());

            return BadRequest(new { title = "Kayıt başarısız.", errors });
        }

        // Email doğrulama linki üret
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        var link = Url.Action(
            action: "ConfirmEmail",
            controller: "Auth",
            values: new { userId = user.Id, token },
            protocol: Request.Scheme
        );
        
        await _emailSender.SendEmailAsync(
            user.Email!,
            "Email Doğrulama",
            $"Hesabınızı doğrulamak için <a href='{link}'>buraya tıklayın</a>"
        );

        return Ok(new
        {
            redirectUrl = "/api/auth/register-success"
        });
    }
    
    [AllowAnonymous]
    [HttpGet("register-success")]
    public IActionResult RegisterSuccess() => View(); // "Mail kutunu kontrol et" sayfası
    
    
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return BadRequest("Link hatalı.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return BadRequest("Kullanıcı bulunamadı.");

        token = Uri.UnescapeDataString(token); // <-- KRİTİK

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return BadRequest("Email doğrulama başarısız.");

        return RedirectToAction(nameof(Login));
    }

    
    
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }


    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromBody]LoginVm model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "Email veya şifre hatalı.");
            return View(model);
        }

        if (_userManager.Options.SignIn.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            await SendEmailConfirmation(user);
            return RedirectToAction(nameof(EmailVerification));
        }

        // Temiz login: Identity her şeyi kendi yönetir
        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
            return Ok(new { twoFactorRequired = true, redirectUrl = "/api/auth/twofactor" });

        if (result.IsLockedOut)
        {
            return Unauthorized(new { title = "Hesap kilitlendi. Biraz sonra tekrar dene." });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { title = "Email veya şifre hatalı." });
        }
        
        return Ok(new { redirectUrl = SafeReturnUrl(returnUrl) });
    }

    [AllowAnonymous]
    [HttpGet("email-verification")]
    public IActionResult EmailVerification() => View();

    private async Task SendEmailConfirmation(User user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = Url.Action(nameof(ConfirmEmail), "Auth",
            new { userId = user.Id, token }, Request.Scheme);

        await _emailSender.SendEmailAsync(
            user.Email!,
            "Email Doğrulama",
            $"Emailinizi doğrulamak için <a href='{link}'>buraya tıklayın</a>"
        );
    }

    
    [AllowAnonymous]
    [HttpGet("twofactor")]
    public IActionResult TwoFactor(string? returnUrl = null, bool rememberMe = false)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["RememberMe"] = rememberMe;
        return View();
    }

    [AllowAnonymous]
    [HttpPost("twofactor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactor(TwoFactorSignInVm model, string? returnUrl = null)
    {
        var code = model.Code.Replace(" ", "").Replace("-", "");

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
            code,
            isPersistent: model.RememberMe,
            rememberClient: model.RememberClient);

        if (result.Succeeded)
            return Redirect(SafeReturnUrl(returnUrl));

        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Çok fazla deneme. Hesap kilitlendi.");
            return View(model);
        }
        
        ModelState.AddModelError("", "Kod hatalı veya süresi geçti.");
        return View(model);
    }


    [Authorize]
    [HttpGet("2fa/setup")]
    public async Task<IActionResult> TwoFactorSetup()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Key yoksa üret
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user) ?? user.UserName ?? "user";
        var uri = BuildOtpAuthUri("CryptoPayment", email, key);

        var qrBase64 = GenerateQrPngBase64(uri);

        // View'da göstereceksin:
        // - key (isteyen manuel girsin)
        // - qrBase64 (img src)
        ViewData["SharedKey"] = key;
        ViewData["QrImage"] = $"data:image/png;base64,{qrBase64}";

        return View();
    }


    [Authorize]
    [HttpPost("2fa/enable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactorEnable(TwoFactorEnableVm model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var code = model.Code.Replace(" ", "").Replace("-", "");

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);

        if (!valid)
        {
            ModelState.AddModelError("", "Kod hatalı. Tekrar deneyin.");
            return RedirectToAction(nameof(TwoFactorSetup));
        }
        
        await _userManager.SetTwoFactorEnabledAsync(user, true);

        // Recovery codes
        var recovery = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        // İstersen bunları View'da göster / db'ye logla / kullanıcıya indirttir
        TempData["RecoveryCodes"] = string.Join("\n", recovery);

        return RedirectToAction(nameof(TwoFactorEnabled));
    }
    
    [Authorize]
    [HttpGet("2fa/enabled")]
    public IActionResult TwoFactorEnabled() => View(); // TempData["RecoveryCodes"] göster

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    
    
    private static string SafeReturnUrl(string? returnUrl)
        => (!string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
            ? returnUrl
            : "/";

    private static string BuildOtpAuthUri(string issuer, string email, string secretKey)
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
               $"?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
    }
    
    
    
    private static string GenerateQrPngBase64(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCode(qrData);

        using Bitmap bitmap = qrCode.GetGraphic(20);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);

        return Convert.ToBase64String(ms.ToArray());
    }
    
}