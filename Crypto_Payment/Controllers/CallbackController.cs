using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Helpers;
using Crypto_Payment.Manager;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CallbackController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IPlisioService _plisioService;
    private readonly ILogger<CallbackController> _logger;
    private readonly string _secretKey;
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;

    public CallbackController(
        IInvoiceService invoiceService,
        IPlisioService plisioService,
        ILogger<CallbackController> logger,
        IOptions<PlisioOptions> plisioOptions,
        IWebHostEnvironment env,
        AppDbContext db)
    {
        _invoiceService = invoiceService;
        _plisioService = plisioService;
        _logger = logger;
        _secretKey = plisioOptions.Value.SecretKey;
        _env = env;
        _db = db;
    }

    // JSON body'den parse edilen parametreler (callback boyunca paylaşılır)
    private Dictionary<string, string>? _jsonBodyParams;
    // Ham JSON body (HMAC doğrulaması için orijinal sırayı korur)
    private string? _rawJsonBody;

    /// <summary>
    /// Plisio callback endpoint - Ödeme durumu güncellemeleri için
    /// HMAC-SHA1 doğrulaması ile korunur
    /// </summary>
    [HttpPost]
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("callback")]
    public async Task<IActionResult> HandleCallback()
    {
        try
        {
            _logger.LogInformation("=== PLISIO CALLBACK RECEIVED ===");
            _logger.LogInformation("Method: {Method}", Request.Method);
            _logger.LogInformation("ContentType: {ContentType}", Request.ContentType ?? "null");

            var allQueryParams = string.Join(", ", Request.Query.Select(q => $"{q.Key}={q.Value}"));
            _logger.LogInformation("Query Params: {Params}", allQueryParams);

            // JSON body varsa parse et ve cache'le
            await TryParseJsonBody();

            if (_jsonBodyParams != null)
            {
                var bodyKeys = string.Join(", ", _jsonBodyParams.Keys);
                _logger.LogInformation("JSON Body Keys: {Keys}", bodyKeys);
                var verifyHashInBody = _jsonBodyParams.ContainsKey("verify_hash") ? "FOUND" : "NOT FOUND";
                _logger.LogInformation("verify_hash in body: {Status}", verifyHashInBody);
            }

            // HMAC-SHA1 doğrulaması
            if (!VerifyPlisioCallback())
            {
                _logger.LogWarning("Callback HMAC doğrulaması başarısız");
                return StatusCode(403, new { status = "error", message = "Invalid verify_hash" });
            }

            var txnId = GetParam("txn_id");
            var status = GetParam("status");
            var orderId = GetParam("order_number");

            _logger.LogInformation("Parsed - TxnId: {TxnId}, Status: {Status}, OrderId: {OrderId}", txnId, status, orderId);

            if (string.IsNullOrEmpty(txnId))
            {
                _logger.LogWarning("Callback received without txn_id");
                return Ok(new { status = "error", message = "txn_id is required" });
            }

            var newStatus = StatusMapper.MapPlisioStatus(status);

            var invoice = await _invoiceService.GetByTxnIdAsync(txnId);
            if (invoice != null)
            {
                if (newStatus != invoice.Status)
                {
                    await _invoiceService.UpdateStatusAsync(invoice.Id!.Value, newStatus);
                    _logger.LogInformation("Invoice {InvoiceId} status updated from {OldStatus} to {NewStatus}",
                        invoice.Id, invoice.Status, newStatus);
                }
                await TrySaveTransactionId(invoice, txnId!);
                return Ok(new { status = "success" });
            }

            var paymentLink = await _db.PaymentLinks
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(p => p.TxnId == txnId);

            if (paymentLink != null)
            {
                var oldPlStatus = paymentLink.Status;
                if (newStatus != oldPlStatus)
                {
                    paymentLink.Status = newStatus;
                    if (newStatus == "expired") paymentLink.ExpiredDate = DateTime.UtcNow;
                    if (newStatus == "completed" || newStatus == "mismatch") paymentLink.PaidDate = DateTime.UtcNow;

                    var blockchainTxId = GetParam("tx_id");
                    if (!string.IsNullOrEmpty(blockchainTxId))
                        paymentLink.TransactionId = blockchainTxId;

                    await _db.SaveChangesAsync();
                    _logger.LogInformation("PaymentLink {PlId} (Invoice {InvId}) status updated: {Old} → {New}",
                        paymentLink.Id, paymentLink.InvoiceId, oldPlStatus, newStatus);
                }

                if (string.IsNullOrEmpty(paymentLink.TransactionId))
                {
                    try
                    {
                        var details = await _plisioService.GetInvoiceDetailsAsync(txnId);
                        if (details?.TxIds != null && details.TxIds.Count > 0)
                        {
                            paymentLink.TransactionId = details.TxIds[0];
                            await _db.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch TX ID for PaymentLink {PlId}", paymentLink.Id);
                    }
                }

                return Ok(new { status = "success" });
            }

            _logger.LogWarning("No Invoice or PaymentLink found for txn_id: {TxnId}", txnId);
            return Ok(new { status = "error", message = "Invoice not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Plisio callback");
            return Ok(new { status = "error", message = "An error occurred processing the callback." });
        }
    }

    /// <summary>
    /// Request body'yi JSON olarak parse eder ve _jsonBodyParams'a yazar.
    /// Plisio callback'i application/json ile POST ediyor.
    /// </summary>
    private async Task TryParseJsonBody()
    {
        try
        {
            var contentType = Request.ContentType ?? "";
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                return;

            Request.EnableBuffering();
            var body = await new System.IO.StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
            Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body)) return;

            _logger.LogInformation("JSON Body (first 500 chars): {Body}", body.Length > 500 ? body[..500] : body);

            // Ham JSON body'yi sakla — HMAC doğrulaması için orijinal sıra gerekli
            _rawJsonBody = body;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            _jsonBodyParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var val = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "",
                    _ => prop.Value.GetRawText()
                };
                _jsonBodyParams[prop.Name] = val;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JSON body parse hatası (form veya query olabilir)");
        }
    }

    /// <summary>
    /// Test için manuel callback simülasyonu - Sadece Development ortamında çalışır
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> TestCallback([FromQuery] int invoiceId, [FromQuery] string status = "completed")
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            _logger.LogInformation("TEST CALLBACK - InvoiceId: {InvoiceId}, Status: {Status}", invoiceId, status);

            var invoice = await _invoiceService.GetByIdAsync(invoiceId);
            if (invoice == null)
            {
                return NotFound(new { error = "Invoice not found", invoiceId });
            }

            var oldStatus = invoice.Status;
            var newStatus = StatusMapper.MapPlisioStatus(status);

            await _invoiceService.UpdateStatusAsync(invoiceId, newStatus);

            return Ok(new
            {
                success = true,
                invoiceId,
                oldStatus,
                newStatus,
                message = $"Invoice status updated from '{oldStatus}' to '{newStatus}'"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TEST CALLBACK ERROR - InvoiceId: {InvoiceId}", invoiceId);
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Plisio callback HMAC-SHA1 doğrulaması.
    /// JSON callback (json=true): verify_hash çıkarılıp kalan JSON stringify → HMAC-SHA1
    /// Form callback: ksort → PHP serialize → HMAC-SHA1
    /// </summary>
    internal bool VerifyPlisioCallback()
    {
        var verifyHash = GetParam("verify_hash");
        if (string.IsNullOrEmpty(verifyHash))
        {
            _logger.LogWarning("verify_hash parametresi bulunamadı");
            return false;
        }

        // JSON callback (json=true ile gönderildiğinde Plisio JSON.stringify kullanır)
        if (_rawJsonBody != null && _jsonBodyParams != null)
        {
            // Yöntem: Ham JSON'dan verify_hash key-value çiftini çıkar, kalan JSON'u hash'le
            // Plisio Node.js örneği: delete ordered.verify_hash; JSON.stringify(ordered);
            var jsonForHmac = RemoveJsonKey(_rawJsonBody, "verify_hash");
            _logger.LogInformation("HMAC input (JSON, first 300): {Input}", jsonForHmac.Length > 300 ? jsonForHmac[..300] : jsonForHmac);

            using var hmacJson = new HMACSHA1(Encoding.UTF8.GetBytes(_secretKey));
            var computedJson = Convert.ToHexString(hmacJson.ComputeHash(Encoding.UTF8.GetBytes(jsonForHmac))).ToLowerInvariant();
            _logger.LogInformation("HMAC computed (JSON): {Computed}, received: {Received}", computedJson, verifyHash);

            if (string.Equals(computedJson, verifyHash, StringComparison.OrdinalIgnoreCase))
                return true;

            // Fallback: PHP serialize yöntemi de dene (bazı Plisio versiyonları farklı olabilir)
            _logger.LogInformation("JSON HMAC eşleşmedi, PHP serialize yöntemi deneniyor...");
        }

        // Form/fallback: PHP serialize yöntemi
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal);

        if (_jsonBodyParams != null)
        {
            foreach (var kv in _jsonBodyParams)
            {
                if (!string.Equals(kv.Key, "verify_hash", StringComparison.OrdinalIgnoreCase))
                    parameters[kv.Key] = kv.Value;
            }
        }

        if (Request.HasFormContentType)
        {
            foreach (var param in Request.Form)
            {
                if (!string.Equals(param.Key, "verify_hash", StringComparison.OrdinalIgnoreCase))
                    parameters[param.Key] = param.Value.ToString();
            }
        }

        foreach (var param in Request.Query)
        {
            if (!string.Equals(param.Key, "verify_hash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(param.Key, "json", StringComparison.OrdinalIgnoreCase))
            {
                parameters[param.Key] = param.Value.ToString();
            }
        }

        var serialized = PhpSerialize(parameters);
        _logger.LogInformation("HMAC input (PHP serialized, first 300): {Serialized}", serialized.Length > 300 ? serialized[..300] : serialized);

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_secretKey));
        var computedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();
        _logger.LogInformation("HMAC computed (PHP): {Computed}, received: {Received}", computedHash, verifyHash);

        return string.Equals(computedHash, verifyHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ham JSON string'den belirli bir key-value çiftini çıkarır.
    /// Plisio Node.js: delete ordered.verify_hash; JSON.stringify(ordered);
    /// Orijinal JSON sırasını ve formatını korur.
    /// </summary>
    internal static string RemoveJsonKey(string json, string keyToRemove)
    {
        using var doc = JsonDocument.Parse(json);
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, keyToRemove, StringComparison.OrdinalIgnoreCase))
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// PHP serialize() formatını C#'ta üretir.
    /// Plisio'nun kullandığı format: a:N:{s:keyLen:"key";s:valLen:"val";...}
    /// </summary>
    internal static string PhpSerialize(SortedDictionary<string, string> dict)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"a:{dict.Count}:{{");
        foreach (var kv in dict)
        {
            sb.Append($"s:{Encoding.UTF8.GetByteCount(kv.Key)}:\"{kv.Key}\";");
            sb.Append($"s:{Encoding.UTF8.GetByteCount(kv.Value)}:\"{kv.Value}\";");
        }
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// HMAC-SHA1 hash hesaplama (statik, test edilebilir)
    /// </summary>
    internal static string ComputeHmacSha1(SortedDictionary<string, string> parameters, string secretKey)
    {
        var serialized = PhpSerialize(parameters);
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();
    }

    /// <summary>
    /// Blockchain TX ID'yi callback verilerinden veya Plisio API'den alıp DB'ye kaydeder.
    /// Sadece henüz TransactionId kaydedilmemiş faturalar için çalışır.
    /// </summary>
    private async Task TrySaveTransactionId(InvoiceDto invoice, string txnId)
    {
        try
        {
            // Zaten kaydedilmişse atla
            if (!string.IsNullOrEmpty(invoice.TransactionId)) return;

            // 1) Callback body'den tx_id dene
            var blockchainTxId = GetParam("tx_id");

            // 2) Yoksa Plisio API'den çek
            if (string.IsNullOrEmpty(blockchainTxId))
            {
                var details = await _plisioService.GetInvoiceDetailsAsync(txnId);
                if (details?.TxIds != null && details.TxIds.Count > 0)
                {
                    blockchainTxId = details.TxIds[0];
                }
            }

            if (!string.IsNullOrEmpty(blockchainTxId))
            {
                await _invoiceService.UpdateTransactionIdAsync(invoice.Id!.Value, blockchainTxId);
                _logger.LogInformation("Invoice {InvoiceId} TransactionId saved: {TxId}", invoice.Id, blockchainTxId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save TransactionId for Invoice {InvoiceId}", invoice.Id);
        }
    }

    /// <summary>
    /// Parametreyi önce JSON body'den, sonra form'dan, sonra query string'den okur.
    /// </summary>
    private string? GetParam(string key)
    {
        // 1) JSON body
        if (_jsonBodyParams != null && _jsonBodyParams.TryGetValue(key, out var jsonVal))
            return jsonVal;

        // 2) Form body
        if (Request.HasFormContentType)
        {
            var formVal = Request.Form[key].FirstOrDefault();
            if (formVal != null) return formVal;
        }

        // 3) Query string
        return Request.Query[key].FirstOrDefault();
    }
}
