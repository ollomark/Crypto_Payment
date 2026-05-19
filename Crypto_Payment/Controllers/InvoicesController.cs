using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Helpers;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Crypto_Payment.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    private readonly IPlisioService _plisioService;
    private readonly IApprovalService _approvalService;
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<InvoicesController> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public InvoicesController(
        IInvoiceService service,
        IPlisioService plisioService,
        IApprovalService approvalService,
        AppDbContext db,
        UserManager<User> userManager,
        ILogger<InvoicesController> logger,
        IHttpClientFactory httpFactory)
    {
        _service = service;
        _plisioService = plisioService;
        _approvalService = approvalService;
        _db = db;
        _userManager = userManager;
        _logger = logger;
        _httpFactory = httpFactory;
    }
    
    [HttpPost("invoice-add")]
    public async Task<IActionResult> InvoiceAdd([FromBody] InvoiceDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // iş kuralı hatası (Plisio, ödeme reddi vs.)
            return UnprocessableEntity(new
            {
                message = ex.Message
            });
        }
        catch (Exception)
        {
            // beklenmeyen sistem hatası
            return StatusCode(500, new
            {
                message = "Beklenmeyen bir hata oluştu."
            });
        }
    }
    
    [HttpPost("invoice-update-registration-status")]
    public async Task<IActionResult> InvoiceUpdateRegistrationStatus([FromQuery] int id, [FromQuery] bool status)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            // Silme işlemi (status=false): MasterAdmin direkt siler, diğerleri onay kuyruğuna girer
            if (!status && !isMaster)
            {
                var invoice = await _service.GetByIdAsync(id);
                if (invoice == null) return NotFound("Fatura bulunamadı.");

                var req = new ApprovalRequest
                {
                    RequestType = "InvoiceDelete",
                    RequestData = JsonSerializer.Serialize(new { InvoiceId = id }),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"Fatura #{id} ({invoice.OrderName}) silme talebi",
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);

                // Audit log
                _db.SystemLogs.Add(new SystemLog
                {
                    Action = "InvoiceDeleteRequest",
                    UserId = user.Id,
                    UserName = user.UserName,
                    TargetEntity = "Invoice",
                    TargetId = id.ToString(),
                    Details = $"Silme onay talebi oluşturuldu: {invoice.OrderName}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _db.SaveChangesAsync();

                return Ok(new { pending = true, message = "Silme talebiniz MasterAdmin onayına gönderildi." });
            }

            await _service.UpdateRegistrationStatusAsync(id, status);

            // MasterAdmin işlemi için audit log
            _db.SystemLogs.Add(new SystemLog
            {
                Action = "InvoiceDeleted",
                UserId = user.Id,
                UserName = user.UserName,
                TargetEntity = "Invoice",
                TargetId = id.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            return Ok("İşlem Başarılı");
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Fatura bulunamadı.");
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // Manuel durum değiştirme — standart admin için onay kuyruğu
    [HttpPost("manual-status-change")]
    public async Task<IActionResult> ManualStatusChange([FromQuery] int id, [FromQuery] string newStatus, [FromQuery] string? note = null)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var invoice = await _service.GetByIdAsync(id);
            if (invoice == null) return NotFound("Fatura bulunamadı.");

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            if (!isMaster)
            {
                var req = new ApprovalRequest
                {
                    RequestType = "InvoiceStatusChange",
                    RequestData = JsonSerializer.Serialize(new { InvoiceId = id, NewStatus = newStatus, Note = note }),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"Fatura #{id} ({invoice.OrderName}) durum değişimi: {invoice.Status} → {newStatus}" +
                        (!string.IsNullOrWhiteSpace(note) ? $" — Not: {note}" : ""),
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);

                _db.SystemLogs.Add(new SystemLog
                {
                    Action = "InvoiceStatusChangeRequest",
                    UserId = user.Id,
                    UserName = user.UserName,
                    TargetEntity = "Invoice",
                    TargetId = id.ToString(),
                    OldValue = invoice.Status,
                    NewValue = newStatus,
                    Details = $"Onay talebi: {invoice.OrderName}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _db.SaveChangesAsync();

                return Ok(new { pending = true, message = "Durum değişikliği talebiniz MasterAdmin onayına gönderildi." });
            }

            await _service.UpdateStatusAsync(id, newStatus);

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "InvoiceStatusChanged",
                UserId = user.Id,
                UserName = user.UserName,
                TargetEntity = "Invoice",
                TargetId = id.ToString(),
                OldValue = invoice.Status,
                NewValue = newStatus,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Durum güncellendi." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Fatura bulunamadı.");
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
    
    // Fatura düzenleme — standart admin için onay kuyruğu
    [HttpPost("edit")]
    public async Task<IActionResult> EditInvoice([FromBody] InvoiceEditDto dto)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var invoice = await _service.GetByIdAsync(dto.Id);
            if (invoice == null) return NotFound("Fatura bulunamadı.");

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            if (!isMaster)
            {
                var req = new ApprovalRequest
                {
                    RequestType = "InvoiceEdit",
                    RequestData = JsonSerializer.Serialize(dto),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"Fatura #{dto.Id} ({invoice.OrderName}) düzenleme talebi" +
                        (!string.IsNullOrWhiteSpace(dto.Note) ? $" — Not: {dto.Note}" : ""),
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);

                _db.SystemLogs.Add(new SystemLog
                {
                    Action = "InvoiceEditRequest",
                    UserId = user.Id,
                    UserName = user.UserName,
                    TargetEntity = "Invoice",
                    TargetId = dto.Id.ToString(),
                    Details = $"Düzenleme onay talebi: {invoice.OrderName}, Not: {dto.Note}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _db.SaveChangesAsync();

                return Ok(new { pending = true, message = "Düzenleme talebiniz MasterAdmin onayına gönderildi." });
            }

            // MasterAdmin direkt düzenler
            var updateDto = new InvoiceDto
            {
                Id = dto.Id,
                OrderName = dto.OrderName,
                SourceAmount = dto.SourceAmount,
                SourceCurrency = dto.SourceCurrency,
                OrderNumber = dto.OrderNumber ?? invoice.OrderNumber,
                Currency = dto.Currency ?? invoice.Currency,
                Email = dto.Email ?? invoice.Email,
                CustomerId = dto.CustomerId ?? invoice.CustomerId,
                CallbackUrl = invoice.CallbackUrl,
                IsRecurring = dto.IsRecurring ?? invoice.IsRecurring,
                RecurringDay = dto.IsRecurring == true ? dto.RecurringDay : null
            };
            await _service.UpdateAsync(dto.Id, updateDto);

            _db.SystemLogs.Add(new SystemLog
            {
                Action = "InvoiceEdited",
                UserId = user.Id,
                UserName = user.UserName,
                TargetEntity = "Invoice",
                TargetId = dto.Id.ToString(),
                Details = $"Fatura düzenlendi: {dto.OrderName}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Fatura güncellendi." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Fatura bulunamadı.");
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    [HttpPost("{invoiceId:int}/payment-links")]
    public async Task<IActionResult> CreatePaymentLink(int invoiceId, [FromBody] CreatePaymentLinkRequest? req = null)
    {
        try
        {
            var invoice = await _service.GetByIdAsync(invoiceId);
            if (invoice == null) return NotFound("Fatura bulunamadı.");

            var methodId = req?.PaymentMethodId;
            PaymentMethod? method = null;

            if (methodId.HasValue)
            {
                method = await _db.PaymentMethods.FindAsync(methodId.Value);
                if (method == null || !method.IsActive)
                    return BadRequest(new { message = "Geçersiz veya pasif ödeme yöntemi." });
            }
            else
            {
                method = await _db.PaymentMethods.FirstOrDefaultAsync(m => m.IsDefault && m.IsActive)
                    ?? await _db.PaymentMethods.FirstOrDefaultAsync(m => m.Type == "plisio" && m.IsActive);
            }

            var link = new PaymentLink
            {
                InvoiceId = invoiceId,
                PaymentMethodId = method?.Id,
                Status = "new",
                CreatedDate = DateTime.UtcNow
            };

            if (method?.Type == "plisio")
            {
                var plisioDto = new InvoiceDto
                {
                    Currency = invoice.Currency,
                    SourceAmount = invoice.SourceAmount,
                    OrderNumber = $"{invoice.OrderNumber}-PL{DateTime.UtcNow:HHmmss}",
                    OrderName = invoice.OrderName,
                    Email = invoice.Email,
                    CallbackUrl = invoice.CallbackUrl,
                    SourceCurrency = invoice.SourceCurrency
                };

                var plisio = await _plisioService.CreateInvoiceAsync(plisioDto);
                if (!plisio.IsSuccess)
                    return UnprocessableEntity(new { message = plisio.ErrorMessage ?? "Plisio ödeme linki oluşturulamadı." });

                link.TxnId = plisio.TxnId;
                link.InvoiceUrl = plisio.InvoiceUrl;
            }
            else if (method?.Type == "bank_transfer")
            {
                link.Note = $"Banka: {method.BankName}, IBAN: {method.Iban}, Hesap Sahibi: {method.AccountHolder}";
                link.InvoiceUrl = null;
            }
            else if (method?.Type == "fast_crypto")
            {
                var cryptoCur = req?.CryptoCurrency ?? "USDT_TRC20";
                var selectedWallet = method.WalletAddress ?? "";

                if (!string.IsNullOrEmpty(method.ExtraConfig))
                {
                    try
                    {
                        var wallets = JsonSerializer.Deserialize<List<string>>(method.ExtraConfig);
                        if (wallets != null && wallets.Count > 0)
                        {
                            selectedWallet = wallets[Random.Shared.Next(wallets.Count)];
                        }
                    }
                    catch { }
                }

                link.CryptoWalletAddress = selectedWallet;
                link.CryptoCurrency = cryptoCur;

                var srcCurrency = (invoice.SourceCurrency ?? "USD").ToUpperInvariant();
                if (srcCurrency == "EURO") srcCurrency = "EUR";
                if (srcCurrency == "TL") srcCurrency = "TRY";
                decimal cryptoAmount = invoice.SourceAmount;
                if (srcCurrency != "USD")
                {
                    var usdRate = await GetExchangeRateToUsd(srcCurrency);
                    if (usdRate > 0)
                        cryptoAmount = Math.Round(invoice.SourceAmount * usdRate, 2);
                }
                link.ExpectedAmount = cryptoAmount;

                var durationHours = req?.DurationHours ?? 0;
                link.ExpiredDate = durationHours > 0
                    ? DateTime.UtcNow.AddHours(durationHours)
                    : (DateTime?)null;

                link.InvoiceUrl = null;
                var durLabel = durationHours switch { 0 => "Süresiz", _ => $"{durationHours} saat" };
                link.Note = $"Hızlı Kripto: {cryptoCur} - {selectedWallet} ({durLabel})";
            }
            else if (method?.Type == "crypto_wallet")
            {
                link.Note = $"Cüzdan: {method.WalletAddress}, Ağ: {method.WalletNetwork}";
                link.InvoiceUrl = null;
            }
            else
            {
                link.Note = req?.Note;
                link.InvoiceUrl = null;
            }

            _db.PaymentLinks.Add(link);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = link.Id,
                txnId = link.TxnId,
                invoiceUrl = link.InvoiceUrl,
                status = link.Status,
                createdDate = link.CreatedDate,
                paymentMethodId = link.PaymentMethodId,
                methodName = method?.Name,
                methodType = method?.Type,
                note = link.Note,
                cryptoPayUrl = method?.Type == "fast_crypto" ? $"/crypto-pay/{link.Id}" : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment link for Invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    public record CreatePaymentLinkRequest(int? PaymentMethodId, string? Note, string? CryptoCurrency, int? DurationHours);

    [HttpPost("{invoiceId:int}/payment-links/{linkId:int}/cancel")]
    public async Task<IActionResult> CancelPaymentLink(int invoiceId, int linkId)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var link = await _db.PaymentLinks
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(p => p.Id == linkId && p.InvoiceId == invoiceId);
            if (link == null) return NotFound("Ödeme kaydı bulunamadı.");

            if (link.Status != "completed" && link.Status != "mismatch")
                return BadRequest(new { message = "Sadece tamamlanmış ödemeler iptal edilebilir." });

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            if (!isMaster)
            {
                string[] monthNames = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                var monthLabel = link.PaidForMonth.HasValue ? $"{monthNames[link.PaidForMonth.Value]} {link.PaidForYear}" : link.CreatedDate.ToString("MMM yyyy");
                var req = new ApprovalRequest
                {
                    RequestType = "PaymentCancel",
                    RequestData = JsonSerializer.Serialize(new { InvoiceId = invoiceId, PaymentLinkId = linkId }),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"#{invoiceId} {link.Invoice?.OrderName ?? "Fatura"} — Ödeme iptali ({monthLabel})",
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);
                return Ok(new { pending = true, message = "Ödeme iptali talebiniz MasterAdmin onayına gönderildi." });
            }

            link.Status = "cancelled";
            await RevertInvoiceStatusIfNeeded(invoiceId, link.Invoice, linkId);
            await _db.SaveChangesAsync();

            _logger.LogInformation("PaymentLink {LinkId} (Invoice {InvoiceId}) cancelled by {User}", linkId, invoiceId, user.UserName);
            return Ok(new { message = "Ödeme iptal edildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling PaymentLink {LinkId} for Invoice {InvoiceId}", linkId, invoiceId);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    [HttpGet("{invoiceId:int}/payment-links")]
    public async Task<IActionResult> GetPaymentLinks(int invoiceId)
    {
        try
        {
            var links = await _db.PaymentLinks
                .Where(p => p.InvoiceId == invoiceId)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new
                {
                    p.Id,
                    p.TxnId,
                    p.InvoiceUrl,
                    p.Status,
                    p.TransactionId,
                    p.CreatedDate,
                    p.ExpiredDate,
                    p.Note,
                    p.IsManual,
                    p.PaymentMethodId,
                    MethodName = p.PaymentMethod != null ? p.PaymentMethod.Name : null,
                    MethodType = p.PaymentMethod != null ? p.PaymentMethod.Type : null,
                    MethodIcon = p.PaymentMethod != null ? p.PaymentMethod.Icon : null,
                    p.CryptoCurrency,
                    p.ExpectedAmount,
                    p.ConfirmedTxHash,
                    p.ReceivedAmount,
                    p.CryptoWalletAddress
                })
                .ToListAsync();

            return Ok(links);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment links for Invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    [HttpGet("{invoiceId:int}/monthly-history")]
    public async Task<IActionResult> GetMonthlyHistory(int invoiceId, [FromQuery] int? year)
    {
        try
        {
            var invoice = await _service.GetByIdAsync(invoiceId);
            if (invoice == null) return NotFound();

            var now = DateTime.UtcNow;
            var targetYear = year ?? now.Year;
            var createdYear = invoice.CreatedDate?.Year ?? targetYear;
            var createdMonth = invoice.CreatedDate?.Month ?? 1;
            var dueDay = invoice.RecurringDay ?? 1;

            var allLinks = await _db.PaymentLinks
                .Where(p => p.InvoiceId == invoiceId)
                .Select(p => new { p.Status, p.CreatedDate, p.PaidDate, p.TransactionId, p.TxnId, p.IsManual, p.Note, p.PaidForMonth, p.PaidForYear })
                .ToListAsync();

            var invoiceStatus = (invoice.Status ?? "pending").ToLowerInvariant();
            var invoiceIsPaid = invoiceStatus == "completed" || invoiceStatus == "mismatch";

            var result = new List<object>();

            for (int m = 1; m <= 12; m++)
            {
                if (targetYear < createdYear || (targetYear == createdYear && m < createdMonth))
                {
                    result.Add(new { month = m, status = "na", paidDate = (DateTime?)null, transactionId = (string?)null, txnId = (string?)null, amount = invoice.SourceAmount, isManual = false, note = (string?)null });
                    continue;
                }

                if (targetYear > now.Year || (targetYear == now.Year && m > now.Month))
                {
                    result.Add(new { month = m, status = "na", paidDate = (DateTime?)null, transactionId = (string?)null, txnId = (string?)null, amount = invoice.SourceAmount, isManual = false, note = (string?)null });
                    continue;
                }

                var monthStart = new DateTime(targetYear, m, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);

                var paidLink = allLinks
                    .Where(p => (p.Status == "completed" || p.Status == "mismatch")
                        && (
                            (p.PaidForMonth.HasValue && p.PaidForYear.HasValue
                                ? (p.PaidForMonth == m && p.PaidForYear == targetYear)
                                : ((p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd))
                        ))
                    .OrderByDescending(p => (DateTime?)(p.PaidDate ?? p.CreatedDate))
                    .FirstOrDefault();

                if (paidLink != null)
                {
                    result.Add(new { month = m, status = "paid", paidDate = (DateTime?)(paidLink.PaidDate ?? paidLink.CreatedDate), transactionId = paidLink.TransactionId, txnId = paidLink.TxnId, amount = invoice.SourceAmount, isManual = paidLink.IsManual, note = paidLink.Note });
                    continue;
                }

                if (!invoice.IsRecurring)
                {
                    result.Add(new { month = m, status = invoiceIsPaid ? "paid" : "unpaid", paidDate = (DateTime?)null, transactionId = invoiceIsPaid ? invoice.TransactionId : null, txnId = invoiceIsPaid ? invoice.TxnId : null, amount = invoice.SourceAmount, isManual = false, note = (string?)null });
                    continue;
                }

                // Bu ay için iptal edilmiş PaymentLink varsa "ödenmiş" gösterme (iptal öncelikli)
                var hasCancelledLinkForMonth = allLinks.Any(p =>
                    p.Status == "cancelled" && (
                        (p.PaidForMonth.HasValue && p.PaidForYear.HasValue && p.PaidForMonth == m && p.PaidForYear == targetYear)
                        || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)));

                // Recurring: faturanın kendi TxnId'si ile ödendiyse ve bu ödeme bu aya aitse (sadece bu ay için iptal yoksa)
                if (!hasCancelledLinkForMonth && invoiceIsPaid && invoice.CreatedDate.HasValue
                    && invoice.CreatedDate.Value >= monthStart && invoice.CreatedDate.Value < monthEnd)
                {
                    result.Add(new { month = m, status = "paid", paidDate = (DateTime?)invoice.CreatedDate, transactionId = invoice.TransactionId, txnId = invoice.TxnId, amount = invoice.SourceAmount, isManual = false, note = (string?)null });
                    continue;
                }

                // Recurring: Invoice.Status completed ama ay eşleşmiyor -- kontrol et
                // Belki ödeme farklı bir kaynaktan (callback) gelip Invoice.Status güncellendi
                // ama PaymentLink oluşturulmadı. O ay için "pending/overdue" kalır.
                var isCurrentMonth = (targetYear == now.Year && m == now.Month);
                var isPastMonth = (targetYear < now.Year || (targetYear == now.Year && m < now.Month));
                string unpaidStatus;
                if (isPastMonth)
                    unpaidStatus = "overdue";
                else if (isCurrentMonth && dueDay < now.Day)
                    unpaidStatus = "overdue";
                else
                    unpaidStatus = "pending";

                result.Add(new { month = m, status = unpaidStatus, paidDate = (DateTime?)null, transactionId = (string?)null, txnId = (string?)null, amount = invoice.SourceAmount, isManual = false, note = (string?)null });
            }

            return Ok(new { year = targetYear, invoiceId, months = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monthly history for Invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    [HttpPost("{invoiceId:int}/manual-payment")]
    public async Task<IActionResult> ManualPayment(int invoiceId, [FromBody] ManualPaymentRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var invoice = await _service.GetByIdAsync(invoiceId);
            if (invoice == null) return NotFound();

            string[] monthNames = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
            var monthStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var alreadyPaid = await _db.PaymentLinks
                .AnyAsync(p => p.InvoiceId == invoiceId
                    && (p.Status == "completed" || p.Status == "mismatch")
                    && ((p.PaidForMonth == request.Month && p.PaidForYear == request.Year)
                        || (p.PaidForMonth == null && p.CreatedDate >= monthStart && p.CreatedDate < monthEnd)));

            if (alreadyPaid)
                return BadRequest(new { message = "Bu ay zaten ödenmiş olarak işaretli." });

            var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");

            if (!isMaster)
            {
                var req = new ApprovalRequest
                {
                    RequestType = "ManualPayment",
                    RequestData = JsonSerializer.Serialize(new { InvoiceId = invoiceId, Year = request.Year, Month = request.Month, Note = request.Note }),
                    RequestedBy = user.Id,
                    RequestedByName = user.FullName ?? user.UserName ?? user.Email ?? "",
                    Description = $"#{invoiceId} {invoice.OrderName} — {monthNames[request.Month]} {request.Year} manuel ödeme" + (string.IsNullOrEmpty(request.Note) ? "" : $" ({request.Note})"),
                    Status = "Pending"
                };
                await _approvalService.CreateAsync(req);
                return Ok(new { pending = true, message = "Manuel ödeme talebi onaya gönderildi." });
            }

            var monthLabel = $"{monthNames[request.Month]} {request.Year}";
            var noteWithMonth = string.IsNullOrEmpty(request.Note) ? monthLabel : $"{monthLabel} — {request.Note}";
            var link = new PaymentLink
            {
                InvoiceId = invoiceId,
                Status = "completed",
                IsManual = true,
                Note = noteWithMonth,
                PaidForMonth = request.Month,
                PaidForYear = request.Year,
                CreatedDate = DateTime.UtcNow
            };
            _db.PaymentLinks.Add(link);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Manual payment recorded for Invoice {InvoiceId}, {Year}-{Month}: {Note}",
                invoiceId, request.Year, request.Month, request.Note);

            return Ok(new { message = "Manuel ödeme kaydedildi.", linkId = link.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording manual payment for Invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    public record ManualPaymentRequest(int Year, int Month, string? Note);

    /// <summary>Tek seferlik: Fatura 529 - Şubat ödendi, Mart ödenmedi olarak düzelt.</summary>
    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("fix-529-feb-paid")]
    public async Task<IActionResult> Fix529FebPaid()
    {
        const int invoiceId = 529;
        const int year = 2026;
        var invoice = await _db.Invoices.FindAsync(invoiceId);
        if (invoice == null) return NotFound(new { message = "Fatura bulunamadı." });

        // Mart için yanlışlıkla eklenmiş manuel ödeme varsa kaldır
        var marchLinks = await _db.PaymentLinks
            .Where(p => p.InvoiceId == invoiceId && p.IsManual && p.PaidForMonth == 3 && p.PaidForYear == year)
            .ToListAsync();
        if (marchLinks.Count > 0)
        {
            _db.PaymentLinks.RemoveRange(marchLinks);
            await _db.SaveChangesAsync();
        }

        // Şubat zaten ödendiyse ekleme
        var febExists = await _db.PaymentLinks
            .AnyAsync(p => p.InvoiceId == invoiceId && (p.Status == "completed" || p.Status == "mismatch")
                && ((p.PaidForMonth == 2 && p.PaidForYear == year)
                    || (p.PaidForMonth == null && p.CreatedDate >= new DateTime(year, 2, 1, 0, 0, 0, DateTimeKind.Utc) && p.CreatedDate < new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc))));
        if (febExists)
            return Ok(new { message = "Şubat zaten ödenmiş. Mart kaydı kaldırıldı.", marchRemoved = marchLinks.Count });

        _db.PaymentLinks.Add(new PaymentLink
        {
            InvoiceId = invoiceId,
            Status = "completed",
            IsManual = true,
            Note = "Şubat 2026",
            PaidForMonth = 2,
            PaidForYear = year,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Şubat ödendi olarak işaretlendi. Mart ödenmedi.", marchRemoved = marchLinks.Count });
    }

    [AllowAnonymous]
    [HttpGet("payment-link-status/{txnId}")]
    public async Task<IActionResult> GetPaymentLinkStatus(string txnId)
    {
        try
        {
            var link = await _db.PaymentLinks.FirstOrDefaultAsync(p => p.TxnId == txnId);
            if (link == null) return NotFound();

            if (link.Status == "completed" || link.Status == "mismatch" || link.Status == "cancelled")
                return Ok(new { status = link.Status, terminal = true });

            if (!string.IsNullOrEmpty(link.TxnId))
            {
                try
                {
                    var details = await _plisioService.GetInvoiceDetailsAsync(link.TxnId);
                    if (details != null && !string.IsNullOrEmpty(details.Status))
                    {
                        var newStatus = StatusMapper.MapPlisioStatus(details.Status);
                        if (newStatus != link.Status)
                        {
                            link.Status = newStatus;
                            if (newStatus == "expired") link.ExpiredDate = DateTime.UtcNow;
                            if (details.TxIds != null && details.TxIds.Count > 0)
                                link.TransactionId = details.TxIds[0];
                            await _db.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Plisio status check failed for PaymentLink TxnId {TxnId}", txnId);
                }
            }

            return Ok(new { status = link.Status, terminal = link.Status == "completed" || link.Status == "mismatch" || link.Status == "expired" || link.Status == "cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking payment link status {TxnId}", txnId);
            return StatusCode(500, new { status = "error" });
        }
    }

    // LIST
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all invoices");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
    
    // TOTAL COUNT
    [HttpGet("GetTotalInvoiceCount")]
    public async Task<IActionResult> GetTotalCount()
    {
        try
        {
            int totalCount = await _service.GetTotalCountAsync();
            return Ok(totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total invoice count");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
    
    // STATUS CHECK - Ödeme durumunu kontrol et (public — polling için)
    // Hibrit: DB'den oku + bekleyen faturalar için Plisio API fallback
    [AllowAnonymous]
    [HttpGet("status/{txnId}")]
    public async Task<IActionResult> GetStatus(string txnId)
    {
        try
        {
            var invoice = await _service.GetByTxnIdAsync(txnId);
            if (invoice == null)
                return NotFound(new { status = "not_found" });

            var invoiceId = invoice.Id!.Value;
            var currentStatus = invoice.Status ?? InvoiceStatus.Pending;

            // Zaten terminal durumda — Plisio'ya sormaya gerek yok
            if (InvoiceStatus.IsTerminal(currentStatus))
            {
                return Ok(new { status = currentStatus, updated = false, walletReady = true, terminal = true });
            }

            // Bekleyen durumlar (preparing, new, pending) → Plisio API'den güncel durumu al
            if (!string.IsNullOrEmpty(invoice.TxnId))
            {
                PlisioInvoiceDetails? plisioDetails = null;
                try
                {
                    plisioDetails = await _plisioService.GetInvoiceDetailsAsync(invoice.TxnId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Plisio API error for Invoice {TxnId}", txnId);
                }

                var walletReady = plisioDetails != null && !string.IsNullOrEmpty(plisioDetails.WalletAddress);

                if (plisioDetails != null && !string.IsNullOrEmpty(plisioDetails.Status))
                {
                    var newStatus = StatusMapper.MapPlisioStatus(plisioDetails.Status);

                    // TX ID varsa ve DB'de yoksa kaydet
                    if (plisioDetails.TxIds != null && plisioDetails.TxIds.Count > 0
                        && string.IsNullOrEmpty(invoice.TransactionId))
                    {
                        try
                        {
                            await _service.UpdateTransactionIdAsync(invoiceId, plisioDetails.TxIds[0]);
                            _logger.LogInformation("Invoice {TxnId} TransactionId saved via polling: {TxId}", txnId, plisioDetails.TxIds[0]);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to save TransactionId via polling for {TxnId}", txnId);
                        }
                    }

                    if (newStatus != currentStatus)
                    {
                        await _service.UpdateStatusAsync(invoiceId, newStatus);
                        _logger.LogInformation("Invoice {TxnId} status synced from Plisio: {OldStatus} → {NewStatus}", txnId, currentStatus, newStatus);
                        return Ok(new { status = newStatus, updated = true, walletReady, terminal = InvoiceStatus.IsTerminal(newStatus) });
                    }
                }

                return Ok(new
                {
                    status = currentStatus,
                    updated = false,
                    walletReady,
                    terminal = false,
                    maxRetries = currentStatus == InvoiceStatus.Preparing ? 20 : (int?)null
                });
            }

            return Ok(new { status = currentStatus, updated = false, walletReady = false, terminal = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking status for Invoice {TxnId}", txnId);
            return StatusCode(500, new { status = "error", message = "Beklenmeyen bir hata oluştu." });
        }
    }

    /// <summary>Ödeme iptal edildiğinde: Tek seferlik faturada başka ödenmiş link yoksa Invoice.Status → pending. Aylık durum ve tüm raporlar PaymentLink.Status üzerinden güncellenir.</summary>
    private async Task RevertInvoiceStatusIfNeeded(int invoiceId, Invoice? invoice, int excludedLinkId)
    {
        if (invoice == null || invoice.IsRecurring) return;
        var hasOtherPaid = await _db.PaymentLinks
            .AnyAsync(p => p.InvoiceId == invoiceId && p.Id != excludedLinkId && (p.Status == "completed" || p.Status == "mismatch"));
        if (!hasOtherPaid)
        {
            invoice.Status = "pending";
        }
    }

    private async Task<decimal> GetExchangeRateToUsd(string fromCurrency)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            var cur = fromCurrency.ToLowerInvariant();
            var url = $"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{cur}.json";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                url = $"https://latest.currency-api.pages.dev/v1/currencies/{cur}.json";
                response = await client.GetAsync(url);
            }
            if (!response.IsSuccessStatusCode) return 0;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(cur, out var rates) && rates.TryGetProperty("usd", out var usdVal))
            {
                return usdVal.GetDecimal();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exchange rate fetch failed for {Currency}", fromCurrency);
        }
        return 0;
    }
}

public record InvoiceEditDto(
    int Id,
    string OrderName,
    decimal SourceAmount,
    string SourceCurrency,
    string? Note,
    string? OrderNumber = null,
    string? Currency = null,
    string? Email = null,
    int? CustomerId = null,
    bool? IsRecurring = null,
    int? RecurringDay = null
);
