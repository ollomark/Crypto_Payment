using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Crypto_Payment.Services;

namespace Crypto_Payment.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class PaymentController : Controller
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IPlisioService _plisioService;
        private readonly ILogger<PaymentController> _logger;
        private readonly AppDbContext _db;

        public PaymentController(
            IInvoiceService invoiceService,
            IPlisioService plisioService,
            ILogger<PaymentController> logger,
            AppDbContext db)
        {
            _invoiceService = invoiceService;
            _plisioService = plisioService;
            _logger = logger;
            _db = db;
        }

        [HttpGet("/pay/{id}")]
        public async Task<IActionResult> Index(int id, [FromQuery] string? txnId = null)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null)
            {
                return NotFound("Fatura bulunamadı.");
            }

            // Unauthenticated access requires valid TxnId
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                if (string.IsNullOrEmpty(txnId) || txnId != invoice.TxnId)
                {
                    return NotFound("Fatura bulunamadı.");
                }
            }

            var viewModel = BuildBaseViewModel(invoice);

            // Plisio'dan detayları çek — retry mekanizması ile
            // Bazı coinlerde (BTC, ETH) wallet adresi hemen hazır olmayabilir
            var plisioDetails = await GetPlisioDetailsWithRetry(invoice.TxnId, maxRetries: 3, delayMs: 1500);

            if (plisioDetails != null)
            {
                ApplyPlisioDetails(viewModel, plisioDetails, invoice.Currency);
            }
            else
            {
                _logger.LogWarning(
                    "Plisio detayları alınamadı - Invoice {Id}, TxnId: {TxnId}. " +
                    "Kullanıcıya bekleme sayfası gösterilecek.",
                    id, invoice.TxnId);
                // Wallet adresi henüz hazır değil — view bunu handle edecek
                viewModel.Status = "preparing";
            }

            return View(viewModel);
        }

        [HttpGet("/result-invoice/{id}")]
        public async Task<IActionResult> ResultInvoice(int id, [FromQuery] string? txnId = null)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null)
            {
                return NotFound("Fatura bulunamadı.");
            }

            // Unauthenticated access requires valid TxnId
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                if (string.IsNullOrEmpty(txnId) || txnId != invoice.TxnId)
                {
                    return NotFound("Fatura bulunamadı.");
                }
            }

            var viewModel = BuildBaseViewModel(invoice);

            // Result sayfasında retry gerekmez — tek deneme yeterli
            var plisioDetails = await _plisioService.GetInvoiceDetailsAsync(invoice.TxnId);
            if (plisioDetails != null)
            {
                ApplyPlisioDetails(viewModel, plisioDetails, invoice.Currency);
            }

            return View(viewModel);
        }

        /// <summary>
        /// Süresi dolmuş faturayı yeniler — aynı tutar/para birimi ile Plisio'da yeni işlem oluşturur.
        /// Fatura ID'si ve ödeme linki sabit kalır, sadece txnId güncellenir.
        /// </summary>
        [HttpPost("/pay/{id}/renew")]
        public async Task<IActionResult> RenewInvoice(int id, [FromQuery] string? txnId = null)
        {
            try
            {
                var invoice = await _invoiceService.GetByIdAsync(id);
                if (invoice == null)
                    return NotFound("Fatura bulunamadı.");

                // Unauthenticated access requires valid TxnId
                if (!(User.Identity?.IsAuthenticated ?? false))
                {
                    if (string.IsNullOrEmpty(txnId) || txnId != invoice.TxnId)
                        return NotFound("Fatura bulunamadı.");
                }

                // Ödendi/onaylandı ise yenilemeye izin verme
                var paidStatuses = new[] { InvoiceStatus.Completed, InvoiceStatus.Mismatch };
                if (paidStatuses.Contains(invoice.Status))
                {
                    _logger.LogWarning("Renewal rejected — Invoice {Id} already paid (status: {Status})", id, invoice.Status);
                    return BadRequest(new { message = "Bu fatura zaten ödenmiş, yenileme yapılamaz." });
                }

                // Plisio'da yeni işlem oluştur (aynı tutar, aynı para birimi)
                var renewDto = new InvoiceDto
                {
                    SourceCurrency = invoice.SourceCurrency,
                    SourceAmount = invoice.SourceAmount,
                    OrderNumber = invoice.OrderNumber,
                    Currency = invoice.Currency,
                    Email = invoice.Email,
                    OrderName = invoice.OrderName,
                    CallbackUrl = invoice.CallbackUrl
                };

                var plisioResult = await _plisioService.CreateInvoiceAsync(renewDto);
                if (!plisioResult.IsSuccess)
                {
                    _logger.LogError("Plisio renewal failed for Invoice {Id}: {Error}", id, plisioResult.ErrorMessage);
                    return StatusCode(500, new { message = "Fatura yenileme başarısız: " + plisioResult.ErrorMessage });
                }

                // DB'deki faturayı güncelle — yeni txnId, status reset
                var oldTxnId = invoice.TxnId;
                await _invoiceService.UpdateTxnAsync(
                    id,
                    plisioResult.TxnId!,
                    plisioResult.InvoiceUrl,
                    InvoiceStatus.New);

                _logger.LogInformation(
                    "Invoice {Id} renewed: OldTxn={OldTxn} → NewTxn={NewTxn}",
                    id, oldTxnId, plisioResult.TxnId);

                // SystemLog'a kaydet
                _db.SystemLogs.Add(new SystemLog
                {
                    Action = "InvoiceRenewed",
                    TargetEntity = "Invoice",
                    TargetId = id.ToString(),
                    OldValue = oldTxnId,
                    NewValue = plisioResult.TxnId,
                    Details = "Fatura müşteri tarafından yenilendi (süresi dolmuş)",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    CreatedDate = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                // Aynı ödeme sayfasına yönlendir (yeni txnId ile)
                return Redirect($"/pay/{id}?txnId={Uri.EscapeDataString(plisioResult.TxnId!)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing Invoice {Id}", id);
                return StatusCode(500, new { message = "Fatura yenileme sırasında bir hata oluştu." });
            }
        }

        /// <summary>
        /// Plisio'dan fatura detaylarını retry mekanizması ile çeker.
        /// Bazı coinlerde (BTC, ETH vb.) wallet adresi hemen hazır olmayabilir.
        /// Tron (TRC20) genelde anlık döner.
        /// </summary>
        private async Task<PlisioInvoiceDetails?> GetPlisioDetailsWithRetry(
            string? txnId, int maxRetries = 3, int delayMs = 1500)
        {
            if (string.IsNullOrEmpty(txnId)) return null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var details = await _plisioService.GetInvoiceDetailsAsync(txnId);

                // Detaylar geldi ve wallet adresi hazır — başarılı
                if (details != null && !string.IsNullOrEmpty(details.WalletAddress))
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "Plisio detayları {Attempt}. denemede alındı - TxnId: {TxnId}",
                            attempt, txnId);
                    }
                    return details;
                }

                // Detaylar geldi ama wallet adresi yok — coin henüz hazır değil
                if (details != null && string.IsNullOrEmpty(details.WalletAddress))
                {
                    _logger.LogInformation(
                        "Plisio wallet adresi henüz hazır değil (deneme {Attempt}/{Max}) - TxnId: {TxnId}",
                        attempt, maxRetries, txnId);
                }

                // Son deneme değilse bekle ve tekrar dene
                if (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }

            // Tüm denemeler bitti — son detayları döndür (wallet olmasa bile)
            return await _plisioService.GetInvoiceDetailsAsync(txnId);
            // Not: Bu son çağrı kasıtlı — wallet adresi olmasa bile status/amount bilgisi döner
        }

        /// <summary>
        /// Invoice DTO'dan temel PaymentViewModel oluşturur.
        /// </summary>
        private static PaymentViewModel BuildBaseViewModel(InvoiceDto invoice)
        {
            return new PaymentViewModel
            {
                InvoiceId = invoice.Id ?? 0,
                OrderNumber = invoice.OrderNumber ?? $"INV-{invoice.Id}",
                OrderName = invoice.OrderName,
                Email = invoice.Email,
                SourceAmount = invoice.SourceAmount,
                SourceCurrency = invoice.SourceCurrency ?? "USD",
                CryptoCurrency = invoice.Currency ?? "USDT_TRX",
                Status = invoice.Status ?? "pending",
                TxnId = invoice.TxnId
            };
        }

        /// <summary>
        /// Plisio detaylarını PaymentViewModel'e uygular.
        /// </summary>
        private void ApplyPlisioDetails(
            PaymentViewModel viewModel,
            PlisioInvoiceDetails details,
            string? currency)
        {
            viewModel.WalletAddress = details.WalletAddress ?? "";
            viewModel.CryptoAmount = details.Amount ?? "0";
            viewModel.Network = GetNetworkName(currency);
            viewModel.ExpireTime = details.ExpireTime;
            viewModel.QrCodeUrl = details.QrCodeUrl;
            viewModel.TxIds = details.TxIds ?? new List<string>();

            // Plisio'dan gelen status varsa güncelle
            if (!string.IsNullOrEmpty(details.Status))
            {
                viewModel.Status = details.Status;
            }
        }

        private static string GetNetworkName(string? currency)
        {
            if (string.IsNullOrEmpty(currency)) return "Unknown";

            return currency.ToUpper() switch
            {
                "USDT_TRX" => "Tron (TRC20)",
                "USDT_ETH" => "Ethereum (ERC20)",
                "USDT_BSC" => "BSC (BEP20)",
                "BTC" => "Bitcoin",
                "ETH" => "Ethereum",
                "TRX" => "Tron",
                "LTC" => "Litecoin",
                "DOGE" => "Dogecoin",
                _ => currency
            };
        }
    }
}
