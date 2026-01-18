using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface IPlisioService
{
    public Task<PlisioInvoiceResult> CreateInvoiceAsync(InvoiceDto dto);
    public Task<PlisioInvoiceDetails?> GetInvoiceDetailsAsync(string? txnId);
}

public class PlisioInvoiceDetails
{
    public string? WalletAddress { get; set; }
    public string? Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? ExpireTime { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? Status { get; set; }
    public List<string>? TxIds { get; set; }
}
