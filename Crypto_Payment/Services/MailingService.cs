using Crypto_Payment.Data;
using Crypto_Payment.DTOS;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Services;

public class MailingService : IMailingService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<MailingService> _logger;

    private static readonly string[] IstanbulTzIds = { "Europe/Istanbul", "Turkey Standard Time" };

    public MailingService(AppDbContext db, IEmailSender emailSender, IConfiguration config, ILogger<MailingService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    private static TimeZoneInfo GetIstanbulTz()
    {
        foreach (var id in IstanbulTzIds)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private static (int Month, int Day) TodayMonthDayIstanbul()
    {
        var tz = GetIstanbulTz();
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return (local.Month, local.Day);
    }

    public async Task<MailingPreviewDto> GetPreviewAsync(string segment, CancellationToken ct = default)
    {
        segment = (segment ?? "").Trim().ToLowerInvariant();
        var list = await GetRecipientsAsync(segment, ct);
        return new MailingPreviewDto
        {
            Segment = segment,
            Count = list.Count,
            Recipients = list.Select(c => new MailingRecipientPreviewDto
            {
                Id = c.Id,
                Email = c.Email ?? "",
                FirstName = c.FirstName,
                LastName = c.LastName
            }).ToList()
        };
    }

    private async Task<List<Customer>> GetRecipientsAsync(string segment, CancellationToken ct)
    {
        return segment switch
        {
            "birthday_today" => await GetBirthdayTodayAsync(ct),
            "no_deposit" => await GetNoDepositAsync(ct),
            _ => new List<Customer>()
        };
    }

    private async Task<List<Customer>> GetBirthdayTodayAsync(CancellationToken ct)
    {
        var (m, d) = TodayMonthDayIstanbul();
        return await _db.Customers
            .AsNoTracking()
            .Where(c => c.DateOfBirth != null
                && c.DateOfBirth.Value.Month == m
                && c.DateOfBirth.Value.Day == d
                && c.Email != null
                && c.Email != "")
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync(ct);
    }

    private async Task<List<Customer>> GetNoDepositAsync(CancellationToken ct)
    {
        var paidIds = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.CustomerId != null
                && i.RegistrationStatus
                && (i.Status == "completed" || i.Status == "mismatch"))
            .Select(i => i.CustomerId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var paid = paidIds.ToHashSet();

        return await _db.Customers
            .AsNoTracking()
            .Where(c => c.Email != null && c.Email != "" && !paid.Contains(c.Id))
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync(ct);
    }

    private string ResolvePortalBase(string siteBaseUrl)
    {
        var configured = _config["App:PublicPortalUrl"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured.TrimEnd('/');
        return siteBaseUrl.TrimEnd('/');
    }

    private static string ApplyPlaceholders(string html, Customer c, string portalBase)
    {
        var firma = c.CompanyName ?? "";
        var login = portalBase + "/";
        return html
            .Replace("{{Ad}}", c.FirstName, StringComparison.Ordinal)
            .Replace("{{Soyad}}", c.LastName, StringComparison.Ordinal)
            .Replace("{{Firma}}", firma, StringComparison.Ordinal)
            .Replace("{{Email}}", c.Email ?? "", StringComparison.Ordinal)
            .Replace("{{SiteUrl}}", portalBase, StringComparison.Ordinal)
            .Replace("{{GirisLinki}}", login, StringComparison.Ordinal);
    }

    public async Task<MailingSendResultDto> SendCampaignAsync(string segment, string subject, string htmlBody, string actorUserId, string? actorUserName, string? ipAddress, string siteBaseUrl, CancellationToken ct = default)
    {
        segment = (segment ?? "").Trim().ToLowerInvariant();
        subject = (subject ?? "").Trim();
        htmlBody ??= "";

        var result = new MailingSendResultDto();
        if (string.IsNullOrEmpty(subject))
        {
            result.Errors.Add("Konu boş olamaz.");
            return result;
        }

        var recipients = await GetRecipientsAsync(segment, ct);
        if (recipients.Count == 0)
        {
            result.Errors.Add("Bu segment için gönderilecek alıcı yok.");
            return result;
        }

        var portalBase = ResolvePortalBase(siteBaseUrl);
        const int delayMs = 350;

        foreach (var c in recipients)
        {
            if (string.IsNullOrWhiteSpace(c.Email))
            {
                result.SkippedNoEmail++;
                continue;
            }

            var body = ApplyPlaceholders(htmlBody, c, portalBase);
            try
            {
                await _emailSender.SendEmailAsync(c.Email.Trim(), subject, body);
                result.Sent++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                var msg = $"{c.Email}: {ex.Message}";
                if (result.Errors.Count < 30)
                    result.Errors.Add(msg);
                _logger.LogWarning(ex, "Mailing gönderilemedi: CustomerId={Id}, Email={Email}", c.Id, c.Email);
            }

            await Task.Delay(delayMs, ct);
        }

        _db.SystemLogs.Add(new SystemLog
        {
            Action = "MailingBulkSent",
            UserId = actorUserId,
            UserName = actorUserName,
            TargetEntity = "Mailing",
            TargetId = segment,
            Details = $"Konu: {subject}. Gönderilen: {result.Sent}, Hata: {result.Failed}, E-postasız atlanan: {result.SkippedNoEmail}",
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync(ct);

        return result;
    }
}
