using Crypto_Payment.Authentication;
using Crypto_Payment.DTOS.ExternalApi;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Crypto_Payment.Controllers.External;

/// <summary>
/// Harici siteler için ödeme API'si. Kimlik doğrulama: X-Api-Key veya Authorization: Bearer cp_live_...
/// </summary>
[ApiController]
[Route("api/v1/payments")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
[EnableRateLimiting("external-api")]
public class ExternalPaymentsController : ControllerBase
{
    private readonly IExternalPaymentService _payments;

    public ExternalPaymentsController(IExternalPaymentService payments)
    {
        _payments = payments;
    }

    private int ApiClientId =>
        int.Parse(User.FindFirstValue("ApiClientId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Yeni kripto ödeme oluşturur; müşteriyi payment_url ile yönlendirin.</summary>
    [HttpPost]
    public async Task<ActionResult<ExternalPaymentResponse>> Create([FromBody] CreateExternalPaymentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _payments.CreatePaymentAsync(ApiClientId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    /// <summary>Ödeme durumunu payment_id ile sorgular.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExternalPaymentStatusResponse>> GetById(int id, CancellationToken ct)
    {
        var result = await _payments.GetByIdAsync(ApiClientId, id, ct);
        return result == null ? NotFound(new { message = "Ödeme bulunamadı." }) : Ok(result);
    }

    /// <summary>Ödeme durumunu order_number ile sorgular.</summary>
    [HttpGet("by-order/{orderNumber}")]
    public async Task<ActionResult<ExternalPaymentStatusResponse>> GetByOrder(string orderNumber, CancellationToken ct)
    {
        var result = await _payments.GetByOrderNumberAsync(ApiClientId, orderNumber, ct);
        return result == null ? NotFound(new { message = "Ödeme bulunamadı." }) : Ok(result);
    }
}
