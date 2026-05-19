using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crypto_Payment.Controllers;

[Route("")]
public class PublicRequestController : Controller
{
    private readonly IWithdrawalRequestService _service;
    private readonly AppDbContext _db;

    public PublicRequestController(IWithdrawalRequestService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    // Public: Token ile form sayfası
    [AllowAnonymous]
    [HttpGet("secure-request/{token:guid}")]
    public async Task<IActionResult> Fill(Guid token)
    {
        var wr = await _service.GetByTokenAsync(token);
        if (wr == null || wr.Status != WithdrawalStatus.LinkCreated)
        {
            return View("InvalidLink");
        }
        ViewBag.Token = token;
        ViewBag.Category = wr.Category;
        ViewBag.CategoryLabel = wr.Category switch
        {
            WithdrawalCategory.Maas => "Maa\u015f",
            WithdrawalCategory.SunucuOdemesi => "Sunucu \u00d6demesi",
            WithdrawalCategory.Avans => "Avans",
            WithdrawalCategory.BayiOdemesi => "Bayi \u00d6demesi",
            _ => "Di\u011fer"
        };
        ViewBag.RequestCode = wr.RequestCode;
        return View("FillForm");
    }

    // Public: Form submit
    [AllowAnonymous]
    [HttpPost("secure-request/{token:guid}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> FillPost(Guid token, [FromForm] FillWithdrawalDto dto)
    {
        var wr = await _service.GetByTokenAsync(token);
        if (wr == null || wr.Status != WithdrawalStatus.LinkCreated)
        {
            return View("InvalidLink");
        }

        // Validasyon
        if (string.IsNullOrWhiteSpace(dto.CustomerName))
            ModelState.AddModelError("CustomerName", "Ad Soyad zorunludur.");
        if (dto.Amount <= 0)
            ModelState.AddModelError("Amount", "Tutar 0'dan büyük olmalıdır.");
        if (string.IsNullOrWhiteSpace(dto.WalletAddressOrIban))
            ModelState.AddModelError("WalletAddressOrIban", "IBAN veya Cüzdan Adresi zorunludur.");
        if (dto.PaymentMethod == WdPaymentMethod.Kripto && dto.CryptoNetwork == null)
            ModelState.AddModelError("CryptoNetwork", "Kripto ağı seçimi zorunludur.");

        if (!ModelState.IsValid)
        {
            ViewBag.Token = token;
            ViewBag.Dto = dto;
            ViewBag.Category = wr.Category;
            ViewBag.CategoryLabel = wr.Category switch
            {
                WithdrawalCategory.Maas => "Maa\u015f",
                WithdrawalCategory.SunucuOdemesi => "Sunucu \u00d6demesi",
                WithdrawalCategory.Avans => "Avans",
                WithdrawalCategory.BayiOdemesi => "Bayi \u00d6demesi",
                _ => "Di\u011fer"
            };
            ViewBag.RequestCode = wr.RequestCode;
            return View("FillForm");
        }

        try
        {
            await _service.FillByCustomerAsync(
                token,
                dto.CustomerName.Trim(),
                dto.CompanyName?.Trim(),
                dto.Amount,
                dto.PaymentMethod,
                dto.CryptoNetwork,
                dto.WalletAddressOrIban.Trim(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                dto.CustomerNote?.Trim(),
                dto.Currency
            );

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "WithdrawalFilled",
                TargetEntity = "WithdrawalRequest",
                TargetId = wr.Id.ToString(),
                NewValue = "CustomerFilled",
                Details = $"Müşteri: {dto.CustomerName}, Tutar: {dto.Amount}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            // Talep kodunu ThankYou sayfasına gönder
            ViewBag.RequestCode = wr.RequestCode;
            return View("ThankYou");
        }
        catch (InvalidOperationException)
        {
            return View("InvalidLink");
        }
    }

    // Admin panel: Çekim talepleri yönetim sayfası
    [Authorize]
    [HttpGet("withdrawal-requests")]
    public IActionResult WithdrawalList() => View();
}

public class FillWithdrawalDto
{
    public string CustomerName { get; set; } = "";
    public string? CompanyName { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public WdPaymentMethod PaymentMethod { get; set; }
    public CryptoNetwork? CryptoNetwork { get; set; }
    public string WalletAddressOrIban { get; set; } = "";
    public string? CustomerNote { get; set; }
}
