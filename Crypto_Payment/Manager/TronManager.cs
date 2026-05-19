using System.Text.Json;
using Crypto_Payment.Services;

namespace Crypto_Payment.Manager;

public class TronManager : ITronService
{
    private readonly HttpClient _http;
    private readonly ILogger<TronManager> _logger;
    private const string TronGridBase = "https://api.trongrid.io";
    // USDT TRC20 kontrat adresi (mainnet)
    private const string UsdtContract = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";

    public TronManager(HttpClient http, ILogger<TronManager> logger)
    {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<TronTransactionResult?> CheckPaymentAsync(
        string walletAddress, decimal expectedAmount, string currency, DateTime sinceUtc, HashSet<string>? excludeTxHashes = null)
    {
        try
        {
            if (currency.Equals("USDT_TRC20", StringComparison.OrdinalIgnoreCase) ||
                currency.Equals("USDT", StringComparison.OrdinalIgnoreCase))
            {
                return await CheckUsdtTransferAsync(walletAddress, expectedAmount, sinceUtc, excludeTxHashes);
            }
            else if (currency.Equals("TRX", StringComparison.OrdinalIgnoreCase))
            {
                return await CheckTrxTransferAsync(walletAddress, expectedAmount, sinceUtc, excludeTxHashes);
            }

            _logger.LogWarning("Unsupported currency for Tron check: {Currency}", currency);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Tron payment for {Wallet}", walletAddress);
            return null;
        }
    }

    private async Task<TronTransactionResult?> CheckUsdtTransferAsync(
        string walletAddress, decimal expectedAmount, DateTime sinceUtc, HashSet<string>? excludeTxHashes = null)
    {
        var sinceMs = new DateTimeOffset(sinceUtc).ToUnixTimeMilliseconds();
        var url = $"{TronGridBase}/v1/accounts/{walletAddress}/transactions/trc20" +
                  $"?only_to=true&limit=50&min_timestamp={sinceMs}" +
                  $"&contract_address={UsdtContract}";

        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TronGrid USDT API returned {Status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;

        var expectedSun = expectedAmount * 1_000_000m; // USDT has 6 decimals
        var tolerance = expectedSun * 0.005m; // 0.5% alt tolerans
        var maxOverpay = expectedSun * 0.15m; // En fazla %15 fazla ödeme (yanlış fatura eşleşmesini önler)

        foreach (var tx in data.EnumerateArray())
        {
            var txId = tx.GetProperty("transaction_id").GetString();
            if (!string.IsNullOrEmpty(txId) && excludeTxHashes?.Contains(txId) == true) continue;

            var toAddr = tx.GetProperty("to").GetString();
            if (!string.Equals(toAddr, walletAddress, StringComparison.OrdinalIgnoreCase)) continue;

            var valueStr = tx.GetProperty("value").GetString() ?? "0";
            if (!decimal.TryParse(valueStr, out var valueSun)) continue;

            // Beklenen ± %15 aralığı (eksik veya çok farklı tutarları reddet)
            if (valueSun >= expectedSun - tolerance && valueSun <= expectedSun + maxOverpay)
            {
                var tsMs = tx.GetProperty("block_timestamp").GetInt64();
                var ts = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime;
                var fromAddr = tx.GetProperty("from").GetString();

                return new TronTransactionResult
                {
                    Found = true,
                    TxHash = txId,
                    Amount = valueSun / 1_000_000m,
                    FromAddress = fromAddr,
                    Timestamp = ts,
                    Confirmed = true
                };
            }
        }

        return null;
    }

    private async Task<TronTransactionResult?> CheckTrxTransferAsync(
        string walletAddress, decimal expectedAmount, DateTime sinceUtc, HashSet<string>? excludeTxHashes = null)
    {
        var sinceMs = new DateTimeOffset(sinceUtc).ToUnixTimeMilliseconds();
        var url = $"{TronGridBase}/v1/accounts/{walletAddress}/transactions" +
                  $"?only_to=true&limit=50&min_timestamp={sinceMs}";

        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TronGrid TRX API returned {Status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;

        var expectedSun = expectedAmount * 1_000_000m; // TRX has 6 decimals
        var tolerance = expectedSun * 0.005m;
        var maxOverpay = expectedSun * 0.15m;

        foreach (var tx in data.EnumerateArray())
        {
            var txId = tx.TryGetProperty("txID", out var txIdEl) ? txIdEl.GetString() : null;
            if (!string.IsNullOrEmpty(txId) && excludeTxHashes?.Contains(txId) == true) continue;

            if (!tx.TryGetProperty("raw_data", out var rawData)) continue;
            if (!rawData.TryGetProperty("contract", out var contracts)) continue;

            foreach (var contract in contracts.EnumerateArray())
            {
                if (!contract.TryGetProperty("type", out var typeEl)) continue;
                if (typeEl.GetString() != "TransferContract") continue;

                if (!contract.TryGetProperty("parameter", out var param)) continue;
                if (!param.TryGetProperty("value", out var val)) continue;

                var toHex = val.TryGetProperty("to_address", out var toEl) ? toEl.GetString() : null;
                var amountSun = val.TryGetProperty("amount", out var amtEl) ? amtEl.GetInt64() : 0;

                if (amountSun >= (long)(expectedSun - tolerance) && amountSun <= (long)(expectedSun + maxOverpay))
                {
                    var tsMs = rawData.TryGetProperty("timestamp", out var tsEl) ? tsEl.GetInt64() : 0;
                    var ts = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime;

                    var confirmed = false;
                    if (tx.TryGetProperty("ret", out var ret))
                    {
                        foreach (var r in ret.EnumerateArray())
                        {
                            if (r.TryGetProperty("contractRet", out var cr) && cr.GetString() == "SUCCESS")
                                confirmed = true;
                        }
                    }

                    return new TronTransactionResult
                    {
                        Found = true,
                        TxHash = txId,
                        Amount = amountSun / 1_000_000m,
                        Timestamp = ts,
                        Confirmed = confirmed
                    };
                }
            }
        }

        return null;
    }
}
