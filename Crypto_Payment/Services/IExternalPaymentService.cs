using Crypto_Payment.DTOS.ExternalApi;
using Crypto_Payment.Models;

namespace Crypto_Payment.Services;

public interface IExternalPaymentService
{
    Task<ExternalPaymentResponse> CreatePaymentAsync(int apiClientId, CreateExternalPaymentRequest request, CancellationToken ct = default);
    Task<ExternalPaymentStatusResponse?> GetByIdAsync(int apiClientId, int paymentId, CancellationToken ct = default);
    Task<ExternalPaymentStatusResponse?> GetByOrderNumberAsync(int apiClientId, string orderNumber, CancellationToken ct = default);
}
