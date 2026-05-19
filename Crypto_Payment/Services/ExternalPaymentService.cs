using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.DTOS.ExternalApi;
using Crypto_Payment.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

public class ExternalPaymentService : IExternalPaymentService
{
    private readonly IInvoiceService _invoiceService;
    private readonly AppDbContext _db;

    public ExternalPaymentService(IInvoiceService invoiceService, AppDbContext db)
    {
        _invoiceService = invoiceService;
        _db = db;
    }

    public async Task<ExternalPaymentResponse> CreatePaymentAsync(
        int apiClientId,
        CreateExternalPaymentRequest request,
        CancellationToken ct = default)
    {
        var duplicate = await _db.Invoices
            .AnyAsync(i => i.ApiClientId == apiClientId && i.OrderNumber == request.OrderNumber && i.RegistrationStatus, ct);
        if (duplicate)
            throw new InvalidOperationException($"Bu order_number zaten kayıtlı: {request.OrderNumber}");

        var items = request.Items?.Select(i => new InvoiceItemDto
        {
            ServiceName = i.Description,
            Quantity = i.Quantity,
            Price = i.UnitPrice,
            Total = i.Quantity * i.UnitPrice
        }).ToList() ?? new List<InvoiceItemDto>();

        var dto = new InvoiceDto
        {
            SourceCurrency = request.SourceCurrency,
            SourceAmount = request.Amount,
            OrderNumber = request.OrderNumber,
            Currency = request.CryptoCurrency,
            Email = request.Email,
            OrderName = string.IsNullOrWhiteSpace(request.OrderName) ? request.OrderNumber : request.OrderName,
            CallbackUrl = AppUrlHelper.GetPlisioCallbackUrl(),
            CustomerId = request.CustomerId,
            InvoiceItemsDto = items,
            ApiClientId = apiClientId,
            ExternalReference = request.ExternalReference,
            MerchantWebhookUrl = request.WebhookUrl
        };

        var created = await _invoiceService.CreateAsync(dto);
        if (created.Id == null)
            throw new InvalidOperationException("Fatura oluşturulamadı.");

        return MapResponse(created);
    }

    public async Task<ExternalPaymentStatusResponse?> GetByIdAsync(int apiClientId, int paymentId, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == paymentId && i.ApiClientId == apiClientId && i.RegistrationStatus, ct);
        return invoice == null ? null : MapStatus(invoice);
    }

    public async Task<ExternalPaymentStatusResponse?> GetByOrderNumberAsync(int apiClientId, string orderNumber, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ApiClientId == apiClientId && i.OrderNumber == orderNumber && i.RegistrationStatus, ct);
        return invoice == null ? null : MapStatus(invoice);
    }

    private static ExternalPaymentResponse MapResponse(InvoiceDto dto) => new()
    {
        PaymentId = dto.Id!.Value,
        OrderNumber = dto.OrderNumber,
        ExternalReference = dto.ExternalReference,
        TxnId = dto.TxnId,
        Status = dto.Status ?? "new",
        PaymentUrl = AppUrlHelper.BuildPaymentUrl(dto.Id!.Value, dto.TxnId),
        Amount = dto.SourceAmount,
        SourceCurrency = dto.SourceCurrency,
        CryptoCurrency = dto.Currency,
        CreatedAt = dto.CreatedDate ?? DateTime.UtcNow
    };

    private static ExternalPaymentStatusResponse MapStatus(Models.Invoice invoice) => new()
    {
        PaymentId = invoice.Id,
        OrderNumber = invoice.OrderNumber,
        ExternalReference = invoice.ExternalReference,
        TxnId = invoice.TxnId,
        Status = invoice.Status ?? "new",
        TransactionId = invoice.TransactionId,
        Amount = invoice.SourceAmount,
        SourceCurrency = invoice.SourceCurrency,
        CryptoCurrency = invoice.Currency,
        CreatedAt = invoice.CreatedDate,
        UpdatedAt = null
    };
}
