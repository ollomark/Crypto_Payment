namespace Crypto_Payment.Models;


public enum InvoiceStatus
{
    New = 0,
    Pending = 1,
    PendingInternal = 2,
    Expired = 3,
    Completed = 4,
    Error = 5,
    Cancelled = 6,
    CancelledDuplicate = 7
}

public class Invoice
{
    public int Id { get; set; }

    // --- Merchant (senin sistemin) ---
    public string OrderNumber { get; set; } = default!;   // Plisio'da unique olmalı :contentReference[oaicite:3]{index=3}
    public string? OrderName { get; set; }                 // merchant internal order name :contentReference[oaicite:4]{index=4}
    public string SourceCurrency { get; set; } = "USD";     // fiat :contentReference[oaicite:5]{index=5}
    public decimal SourceAmount { get; set; }               // fiat amount :contentReference[oaicite:6]{index=6}

    // müşteri emaili (Plisio “email” alanı opsiyonel ama sende login var)
    public string? CustomerEmail { get; set; }

    // --- Create Invoice Request (opsiyonel alanlar) ---
    public string? Currency { get; set; }                   // kripto kodu (BTC, ETH...) :contentReference[oaicite:7]{index=7}
    public string? AllowedPsysCids { get; set; }            // "BTC,ETH" şeklinde :contentReference[oaicite:8]{index=8}
    public string? Description { get; set; }                // merchant invoice description :contentReference[oaicite:9]{index=9}
    public string? CallbackUrl { get; set; }                // status callback :contentReference[oaicite:10]{index=10}
    public string? SuccessCallbackUrl { get; set; }
    public string? FailCallbackUrl { get; set; }
    public string? SuccessInvoiceUrl { get; set; }
    public string? FailInvoiceUrl { get; set; }
    public int? ExpireMin { get; set; }                     // expire_min :contentReference[oaicite:11]{index=11}

    // --- Plisio response fields ---
    public string? TxnId { get; set; }                      // Plisio internal id :contentReference[oaicite:12]{index=12}
    public string? InvoiceUrl { get; set; }                 // invoice_url :contentReference[oaicite:13]{index=13}

    // White-label alanları (response + callback)
    public decimal? AmountCrypto { get; set; }              // amount :contentReference[oaicite:14]{index=14}
    public decimal? PendingAmountCrypto { get; set; }        // pending_amount :contentReference[oaicite:15]{index=15}
    public string? WalletHash { get; set; }                 // wallet_hash :contentReference[oaicite:16]{index=16}
    public string? PsysCid { get; set; }                    // psys_cid :contentReference[oaicite:17]{index=17}
    public decimal? SourceRate { get; set; }                // source_rate :contentReference[oaicite:18]{index=18}
    public long? ExpireUtc { get; set; }                    // expire_utc unix :contentReference[oaicite:19]{index=19}
    public int? ExpectedConfirmations { get; set; }         // expected_confirmations :contentReference[oaicite:20]{index=20}
    public string? QrCodeBase64 { get; set; }               // qr_code (base64) :contentReference[oaicite:21]{index=21}
    public string? VerifyHash { get; set; }                 // verify_hash :contentReference[oaicite:22]{index=22}

    public decimal? InvoiceCommission { get; set; }         // invoice_commission :contentReference[oaicite:23]{index=23}
    public decimal? InvoiceSum { get; set; }                // invoice_sum :contentReference[oaicite:24]{index=24}
    public decimal? InvoiceTotalSum { get; set; }           // invoice_total_sum :contentReference[oaicite:25]{index=25}

    // --- Callback fields ---
    public InvoiceStatus Status { get; set; } = InvoiceStatus.New;  // new/pending/completed... :contentReference[oaicite:26]{index=26}
    public int? Confirmations { get; set; }                 // confirmations :contentReference[oaicite:27]{index=27}
    public string? Comment { get; set; }                    // comment :contentReference[oaicite:28]{index=28}

    // Duplicate/switching
    public string? SwitchId { get; set; }                   // switch_id :contentReference[oaicite:29]{index=29}
    public string? NewId { get; set; }                      // doc “new_id” kavramı var (not) :contentReference[oaicite:30]{index=30}

    // Merchant info (callback)
    public string? Merchant { get; set; }                   // merchant :contentReference[oaicite:31]{index=31}
    public string? MerchantId { get; set; }                 // merchant_id :contentReference[oaicite:32]{index=32}

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}