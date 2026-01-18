using Microsoft.AspNetCore.Mvc;
using Crypto_Payment.DTOS;
using Crypto_Payment.Services;

namespace Crypto_Payment.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IPlisioService _plisioService;

        public PaymentController(IInvoiceService invoiceService, IPlisioService plisioService)
        {
            _invoiceService = invoiceService;
            _plisioService = plisioService;
        }

        [HttpGet("/pay/{id}")]
        public async Task<IActionResult> Index(int id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            var plisioDetails = await _plisioService.GetInvoiceDetailsAsync(invoice.TxnId);

            var viewModel = new PaymentViewModel
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

            if (plisioDetails != null)
            {
                viewModel.WalletAddress = plisioDetails.WalletAddress ?? "";
                viewModel.CryptoAmount = plisioDetails.Amount ?? "0";
                viewModel.Network = GetNetworkName(invoice.Currency);
                viewModel.ExpireTime = plisioDetails.ExpireTime;
                viewModel.QrCodeUrl = plisioDetails.QrCodeUrl;
                viewModel.TxIds = plisioDetails.TxIds ?? new List<string>();
                
                // Update status from Plisio if available
                if (!string.IsNullOrEmpty(plisioDetails.Status))
                {
                    viewModel.Status = plisioDetails.Status;
                }
            }

            return View(viewModel);
        }

        private string GetNetworkName(string? currency)
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
