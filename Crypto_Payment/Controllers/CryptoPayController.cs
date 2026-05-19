using Crypto_Payment.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[AllowAnonymous]
public class CryptoPayController : Controller
{
    private readonly AppDbContext _db;

    public CryptoPayController(AppDbContext db) => _db = db;

    [HttpGet("/crypto-pay/{linkId:int}")]
    public async Task<IActionResult> Index(int linkId)
    {
        var link = await _db.PaymentLinks
            .Include(p => p.Invoice).ThenInclude(i => i!.Customer)
            .Include(p => p.PaymentMethod)
            .FirstOrDefaultAsync(p => p.Id == linkId);

        if (link == null) return NotFound("Ödeme linki bulunamadı.");
        if (link.PaymentMethod?.Type != "fast_crypto") return NotFound("Bu link hızlı kripto ödeme değil.");

        ViewBag.Link = link;
        return View("~/Views/CryptoPay/Index.cshtml");
    }

    [HttpGet("/api/crypto-pay/{linkId:int}/status")]
    public async Task<IActionResult> Status(int linkId)
    {
        var link = await _db.PaymentLinks
            .Select(p => new {
                p.Id, p.Status, p.ConfirmedTxHash, p.ReceivedAmount,
                p.CryptoCurrency, p.ExpiredDate, p.CreatedDate
            })
            .FirstOrDefaultAsync(p => p.Id == linkId);

        if (link == null) return NotFound();

        return Ok(new
        {
            status = link.Status,
            txHash = link.ConfirmedTxHash,
            receivedAmount = link.ReceivedAmount,
            currency = link.CryptoCurrency,
            expired = link.Status == "expired" || (link.ExpiredDate.HasValue && link.ExpiredDate < DateTime.UtcNow),
            terminal = link.Status == "completed" || link.Status == "expired" || link.Status == "cancelled"
        });
    }
}
