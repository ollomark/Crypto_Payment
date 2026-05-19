namespace Crypto_Payment.Services;

public interface IPushNotificationService
{
    /// <summary>Belirtilen kullanıcılara bildirim gönderir.</summary>
    Task SendToUsersAsync(IEnumerable<string> userIds, string title, string body, string url = "/", string? tag = null);
    /// <summary>MasterAdmin rollerindeki tüm kullanıcılara bildirim gönderir.</summary>
    Task SendToMasterAdminsAsync(string title, string body, string url = "/", string? tag = null);
}
