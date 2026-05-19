using Crypto_Payment.DTOS;

namespace Crypto_Payment.Services;

public interface IMailingService
{
    Task<MailingPreviewDto> GetPreviewAsync(string segment, CancellationToken ct = default);
    Task<MailingSendResultDto> SendCampaignAsync(string segment, string subject, string htmlBody, string actorUserId, string? actorUserName, string? ipAddress, string siteBaseUrl, CancellationToken ct = default);
}
