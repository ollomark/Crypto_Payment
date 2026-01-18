using Crypto_Payment.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CallbackController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<CallbackController> _logger;

    public CallbackController(IInvoiceService invoiceService, ILogger<CallbackController> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }
    
    /// <summary>
    /// Plisio callback endpoint - Ödeme durumu güncellemeleri için
    /// </summary>
    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> HandleCallback()
    {
        try
        {
            // Query string veya form data'dan parametreleri al
            var txnId = Request.Query["txn_id"].FirstOrDefault() 
                        ?? Request.Form["txn_id"].FirstOrDefault();
            var status = Request.Query["status"].FirstOrDefault() 
                        ?? Request.Form["status"].FirstOrDefault();
            var orderId = Request.Query["order_number"].FirstOrDefault() 
                         ?? Request.Form["order_number"].FirstOrDefault();
            
            _logger.LogInformation($"Plisio Callback received - TxnId: {txnId}, Status: {status}, OrderId: {orderId}");
            
            if (string.IsNullOrEmpty(txnId))
            {
                _logger.LogWarning("Callback received without txn_id");
                return Ok(new { status = "error", message = "txn_id is required" });
            }
            
            // Faturayı bul
            var invoice = await _invoiceService.GetByTxnIdAsync(txnId);
            if (invoice == null)
            {
                _logger.LogWarning($"Invoice not found for txn_id: {txnId}");
                return Ok(new { status = "error", message = "Invoice not found" });
            }
            
            // Durumu güncelle
            var newStatus = MapPlisioStatus(status);
            if (newStatus != invoice.Status)
            {
                await _invoiceService.UpdateStatusAsync(invoice.Id, newStatus);
                _logger.LogInformation($"Invoice {invoice.Id} status updated from {invoice.Status} to {newStatus}");
            }
            
            // Plisio'ya başarılı yanıt dön
            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Plisio callback");
            return Ok(new { status = "error", message = ex.Message });
        }
    }
    
    private string MapPlisioStatus(string? plisioStatus)
    {
        if (string.IsNullOrEmpty(plisioStatus))
            return "pending";
            
        return plisioStatus.ToLower() switch
        {
            "completed" => "completed",
            "confirmed" => "completed",
            "mismatch" => "completed", // Tutar farklı ama ödeme yapılmış
            "expired" => "expired",
            "cancelled" => "expired",
            "error" => "expired",
            "new" => "pending",
            "pending" => "pending",
            _ => "pending"
        };
    }
}
