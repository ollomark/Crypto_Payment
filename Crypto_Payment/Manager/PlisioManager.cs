using System.Globalization;
using System.Text.Json;
using Crypto_Payment.DTOS;
using Crypto_Payment.Services;
using Microsoft.Extensions.Options;

namespace Crypto_Payment.Manager;

public class PlisioManager : IPlisioService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<PlisioManager> _logger;

    public PlisioManager(HttpClient http, IOptions<PlisioOptions> opts, ILogger<PlisioManager> logger)
    {
        _http = http;
        _apiKey = opts.Value.ApiKey;
        _baseUrl = opts.Value.BaseUrl;
        _logger = logger;
    }

    public async Task<PlisioInvoiceResult> CreateInvoiceAsync(InvoiceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        
        // callback_url için json=true ekle
        var callbackUrl = AddJsonTrue(dto.CallbackUrl);
        _logger.LogInformation("[CREATE INVOICE] Callback URL: {CallbackUrl}", callbackUrl);
        
        // Para birimi normalizasyonu (EURO → EUR)
        var sourceCurrency = dto.SourceCurrency.ToUpper() == "EURO" ? "EUR" : dto.SourceCurrency;
        
        var url =
            $"{_baseUrl}/api/v1/invoices/new" +
            $"?source_currency={Uri.EscapeDataString(sourceCurrency)}" +
            $"&source_amount={Uri.EscapeDataString(dto.SourceAmount.ToString(CultureInfo.InvariantCulture))}" +
            $"&order_number={Uri.EscapeDataString(dto.OrderNumber)}" +
            $"&currency={Uri.EscapeDataString(dto.Currency)}" +
            $"&email={Uri.EscapeDataString(dto.Email)}" +
            $"&order_name={Uri.EscapeDataString(dto.OrderName)}" +
            $"&callback_url={Uri.EscapeDataString(callbackUrl)}" +
            $"&api_key={Uri.EscapeDataString(_apiKey)}";
        
        _logger.LogInformation("[CREATE INVOICE] Plisio API URL: {Url}", url.Replace(_apiKey, "***"));
        var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var data = doc.RootElement.GetProperty("data");
            var txnId = data.GetProperty("txn_id").GetString();
            var invoiceUrl = data.GetProperty("invoice_url").GetString();
            
            return new PlisioInvoiceResult
            {
                IsSuccess = true,
                InvoiceId = txnId,
                TxnId = txnId,
                InvoiceUrl = invoiceUrl
            };
        }
        else
        {
            string? errorMessage = body;
            int? errorCode = null;
            if (doc.RootElement.TryGetProperty("data", out var errData))
            {
                errorMessage = errData.TryGetProperty("message", out var m) ? m.GetString() : body;
                errorCode = errData.TryGetProperty("code", out var c) ? c.GetInt32() : (int?)null;
            }
            return new PlisioInvoiceResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
    
    public async Task<PlisioInvoiceDetails?> GetInvoiceDetailsAsync(string? txnId)
    {
        if (string.IsNullOrEmpty(txnId)) return null;
        try
        {
            // Use /invoices endpoint to get full details including wallet_hash
            var url = $"{_baseUrl}/api/v1/invoices/{txnId}?api_key={Uri.EscapeDataString(_apiKey)}";
            var resp = await _http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Plisio API Response for {TxnId}: {Body}", txnId, body);
            
            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetString();
            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Plisio API returned non-success status: {Status}", status);
                return null;
            }
            
            var data = doc.RootElement.GetProperty("data");
            
            // Invoice details are nested inside data.invoice
            if (!data.TryGetProperty("invoice", out var invoice))
            {
                _logger.LogWarning("No 'invoice' property in Plisio response data");
                return null;
            }
            
            var details = new PlisioInvoiceDetails
            {
                Status = invoice.TryGetProperty("status", out var s) ? s.GetString() : null,
                Amount = invoice.TryGetProperty("amount", out var a) ? a.GetString() : null,
                Currency = invoice.TryGetProperty("currency", out var cur) ? cur.GetString() : null,
                TxIds = new List<string>()
            };
            
            // Wallet address - try multiple fields
            if (invoice.TryGetProperty("wallet_hash", out var wh) && !string.IsNullOrEmpty(wh.GetString()))
            {
                details.WalletAddress = wh.GetString();
            }
            else if (invoice.TryGetProperty("wallet", out var w) && !string.IsNullOrEmpty(w.GetString()))
            {
                details.WalletAddress = w.GetString();
            }
            else if (invoice.TryGetProperty("address", out var addr) && !string.IsNullOrEmpty(addr.GetString()))
            {
                details.WalletAddress = addr.GetString();
            }
            
            _logger.LogInformation("Wallet Address: {WalletAddress}", details.WalletAddress ?? "NULL");
            
            // Expire time (Unix timestamp as string)
            if (invoice.TryGetProperty("expire_utc", out var exp))
            {
                string? expStr = null;
                if (exp.ValueKind == JsonValueKind.String)
                {
                    expStr = exp.GetString();
                }
                else if (exp.ValueKind == JsonValueKind.Number)
                {
                    expStr = exp.GetInt64().ToString();
                }
                
                if (!string.IsNullOrEmpty(expStr) && long.TryParse(expStr, out var expUnix))
                {
                    details.ExpireTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                }
            }
            
            // QR Code URL - Generate from wallet address
            if (!string.IsNullOrEmpty(details.WalletAddress))
            {
                // Create QR with wallet address and amount if available
                var qrData = details.WalletAddress;
                if (!string.IsNullOrEmpty(details.Amount) && !string.IsNullOrEmpty(details.Currency))
                {
                    // For crypto payments, include amount in QR
                    qrData = $"{details.WalletAddress}?amount={details.Amount}";
                }
                details.QrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(qrData)}";
                _logger.LogInformation("QR Code URL generated: {QrCodeUrl}", details.QrCodeUrl);
            }
            else
            {
                _logger.LogWarning("Cannot generate QR code: Wallet address is null or empty");
            }
            
            // tx array
            if (invoice.TryGetProperty("tx", out var txArray)
                && txArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var tx in txArray.EnumerateArray())
                {
                    if (tx.TryGetProperty("txid", out var txId)
                        && txId.ValueKind == JsonValueKind.String)
                    {
                        var txIdStr = txId.GetString();
                        if (!string.IsNullOrEmpty(txIdStr))
                        {
                            details.TxIds.Add(txIdStr);
                        }
                    }
                }
            }

            // single tx_id
            if (invoice.TryGetProperty("tx_id", out var singleTxId)
                && singleTxId.ValueKind == JsonValueKind.String)
            {
                var txIdStr = singleTxId.GetString();
                if (!string.IsNullOrEmpty(txIdStr)
                    && !details.TxIds.Contains(txIdStr))
                {
                    details.TxIds.Add(txIdStr);
                }
            }

            
            return details;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Plisio invoice details for TxnId: {TxnId}", txnId);
            return null;
        }
    }
    
    private static string AddJsonTrue(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (url.Contains("json=true", StringComparison.OrdinalIgnoreCase)) return url;
        return url.Contains("?") ? url + "&json=true" : url + "?json=true";
    }

    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/v1/currencies/USDT_TRX?api_key={Uri.EscapeDataString(_apiKey)}";
            var resp = await _http.GetAsync(url);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

public class PlisioInvoiceResult
{
    public bool IsSuccess { get; set; }
    public string? InvoiceId { get; set; }
    public string? TxnId { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }
}

public class PlisioOptions
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.plisio.net";
}
