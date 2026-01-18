using Crypto_Payment.DTOS;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    private readonly IPlisioService _plisioService;

    public InvoicesController(IInvoiceService service, IPlisioService plisioService)
    {
        _service = service;
        _plisioService = plisioService;
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
            return StatusCode(500, new { title = "Sunucu hatası", detail = ex.Message });
        }
    }
    
    // STATUS CHECK - Ödeme durumunu kontrol et
    [HttpGet("status/{id}")]
    public async Task<IActionResult> GetStatus(int id)
    {
        try
        {
            var invoice = await _service.GetByIdAsync(id);
            if (invoice == null)
            {
                return NotFound(new { status = "not_found" });
            }
            
            // Eğer zaten completed veya expired ise direkt dön
            if (invoice.Status == "completed" || invoice.Status == "expired")
            {
                return Ok(new { status = invoice.Status });
            }
            
            // Plisio'dan güncel durumu al
            if (!string.IsNullOrEmpty(invoice.TxnId))
            {
                var plisioDetails = await _plisioService.GetInvoiceDetailsAsync(invoice.TxnId);
                if (plisioDetails != null && !string.IsNullOrEmpty(plisioDetails.Status))
                {
                    var newStatus = MapPlisioStatus(plisioDetails.Status);
                    
                    // Durum değiştiyse güncelle
                    if (newStatus != invoice.Status)
                    {
                        await _service.UpdateStatusAsync(id, newStatus);
                        return Ok(new { status = newStatus });
                    }
                }
            }
            
            return Ok(new { status = invoice.Status ?? "pending" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
    
    private string MapPlisioStatus(string plisioStatus)
    {
        return plisioStatus?.ToLower() switch
        {
            "completed" => "completed",
            "confirmed" => "completed",
            "mismatch" => "completed", // Tutar farklı ama ödeme yapılmış
            "expired" => "expired",
            "cancelled" => "expired",
            "error" => "expired",
            _ => "pending"
        };
    }
}
