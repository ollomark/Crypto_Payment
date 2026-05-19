using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Route("")]
public class ExpensePageController : Controller
{
    // Giriş yapmadan erişilebilen public talep formu
    [AllowAnonymous]
    [HttpGet("talep-olustur")]
    public IActionResult TalepOlustur() => View();

    // Admin: Gider/çekim yönetim sayfası
    [Authorize]
    [HttpGet("expenses")]
    public IActionResult ExpenseList() => View();

    // Admin: Onay bekleyen işlemler sayfası (sadece MasterAdmin)
    [Authorize(Roles = "MasterAdmin")]
    [HttpGet("approvals")]
    public IActionResult ApprovalList() => View();

    // Normal admin: kendi taleplerini görür
    [Authorize]
    [HttpGet("my-requests")]
    public IActionResult MyRequests() => View();
}
