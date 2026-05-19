namespace Crypto_Payment.Services;

/// <summary>Push bildirimleri devre dışı. Hiçbir şey göndermez.</summary>
public class NoOpPushNotificationService : IPushNotificationService
{
    public Task SendToUsersAsync(IEnumerable<string> userIds, string title, string body, string url = "/", string? tag = null)
        => Task.CompletedTask;

    public Task SendToMasterAdminsAsync(string title, string body, string url = "/", string? tag = null)
        => Task.CompletedTask;
}
