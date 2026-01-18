using System.Globalization;
using System.Text.Json;
using Crypto_Payment.DTOS;
using Crypto_Payment.Services;
using Microsoft.Extensions.Options;

public class PlisioManager : IPlisioService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public PlisioManager(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Plisio:ApiKey"]; // appsettings.json'a koy
    }
    
    public async Task<PlisioInvoiceResult> CreateInvoiceAsync(InvoiceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        // callback_url için json=true ekle (Plisio bazı dillerde öneriyor)
        var callbackUrl = AddJsonTrue(dto.CallbackUrl);

        var url =
            "https://api.plisio.net/api/v1/invoices/new" +
            $"?source_currency={Uri.EscapeDataString(dto.SourceCurrency)}" +
            $"&source_amount={Uri.EscapeDataString(dto.SourceAmount.ToString(CultureInfo.InvariantCulture))}" +
            $"&order_number={Uri.EscapeDataString(dto.OrderNumber)}" +
            $"&currency={Uri.EscapeDataString(dto.Currency)}" +
            $"&email={Uri.EscapeDataString(dto.Email)}" +
            $"&order_name={Uri.EscapeDataString(dto.OrderName)}" +
            $"&callback_url={Uri.EscapeDataString(callbackUrl)}" +
            $"&api_key={Uri.EscapeDataString(_apiKey)}";

        var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        // HTTP hata ise bile body içinde Plisio mesajı olabilir
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
            // status:error -> data.message / data.code gibi
            var data = doc.RootElement.GetProperty("data");
            return new PlisioInvoiceResult
            {
                IsSuccess = false,
                ErrorMessage = data.TryGetProperty("message", out var m) ? m.GetString() : body,
                ErrorCode = data.TryGetProperty("code", out var c) ? c.GetInt32() : (int?)null
            };
        }
    }
    
    public async Task<PlisioInvoiceDetails?> GetInvoiceDetailsAsync(string? txnId)
    {
        if (string.IsNullOrEmpty(txnId)) return null;

        try
        {
            var url = $"https://api.plisio.net/api/v1/operations/{txnId}?api_key={Uri.EscapeDataString(_apiKey)}";
            var resp = await _http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetString();

            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var data = doc.RootElement.GetProperty("data");
            
            var details = new PlisioInvoiceDetails
            {
                Status = data.TryGetProperty("status", out var s) ? s.GetString() : null,
                Amount = data.TryGetProperty("amount", out var a) ? a.GetString() : null,
                Currency = data.TryGetProperty("currency", out var cur) ? cur.GetString() : null
            };

            // Cüzdan adresi
            if (data.TryGetProperty("wallet_hash", out var wh))
            {
                details.WalletAddress = wh.GetString();
            }

            // Expire time (Unix timestamp)
            if (data.TryGetProperty("expire_utc", out var exp))
            {
                if (exp.TryGetInt64(out var expUnix))
                {
                    details.ExpireTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                }
            }

            // QR Code URL
            if (data.TryGetProperty("qr_code", out var qr))
            {
                details.QrCodeUrl = qr.GetString();
            }

            return details;
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    private static string AddJsonTrue(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (url.Contains("json=true", StringComparison.OrdinalIgnoreCase)) return url;
        return url.Contains("?") ? url + "&json=true" : url + "?json=true";
    }
}

public class PlisioInvoiceResult
{
    public bool IsSuccess { get; set; }
    public string? InvoiceId { get; set; }   // txn_id
    public string? TxnId { get; set; }       // txn_id (aynı değer)
    public string? InvoiceUrl { get; set; }  // invoice_url
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }
}

public class PlisioOptions
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.plisio.net";
}
