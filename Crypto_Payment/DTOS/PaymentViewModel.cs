namespace Crypto_Payment.DTOS
{
    public class PaymentViewModel
    {
        public int InvoiceId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string? OrderName { get; set; }
        public string? Email { get; set; }
        public decimal SourceAmount { get; set; }
        public string SourceCurrency { get; set; } = "USD";
        public string CryptoAmount { get; set; } = "0";
        public string CryptoCurrency { get; set; } = string.Empty;
        public string Network { get; set; } = string.Empty;
        public string WalletAddress { get; set; } = string.Empty;
        public string? QrCodeUrl { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime? ExpireTime { get; set; }
        public string? TxnId { get; set; }
        public List<string> TxIds { get; set; } = new List<string>();
    }
}
