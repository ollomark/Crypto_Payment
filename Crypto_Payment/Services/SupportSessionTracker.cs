using System.Collections.Concurrent;

namespace Crypto_Payment.Services;

public class SupportSession
{
    public string SessionId { get; set; } = "";
    public string CustomerConnectionId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string OrderName { get; set; } = "";
    public int InvoiceId { get; set; }
    public string? AdminUserId { get; set; }
    public string? AdminConnectionId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public List<SupportMessage> Messages { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? PageUrl { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Isp { get; set; }
    public string? CurrentTypingText { get; set; }
}

public class SupportMessage
{
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class SupportSessionTracker
{
    private readonly ConcurrentDictionary<string, SupportSession> _sessions = new();

    public string CreateSession(string connectionId, int invoiceId, string customerName, string orderName)
    {
        var existing = _sessions.Values.FirstOrDefault(s =>
            s.CustomerConnectionId == connectionId && s.IsActive);
        if (existing != null) return existing.SessionId;

        var sessionId = Guid.NewGuid().ToString("N")[..12];
        var session = new SupportSession
        {
            SessionId = sessionId,
            CustomerConnectionId = connectionId,
            CustomerName = customerName,
            OrderName = orderName,
            InvoiceId = invoiceId
        };
        _sessions[sessionId] = session;
        return sessionId;
    }

    public List<SupportSession> GetPendingSessions()
    {
        return _sessions.Values
            .Where(s => s.IsActive && s.AdminUserId == null)
            .OrderBy(s => s.CreatedDate)
            .ToList();
    }

    public List<SupportSession> GetAllActiveSessions()
    {
        return _sessions.Values
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CreatedDate)
            .ToList();
    }

    public SupportSession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var s) ? s : null;
    }

    public SupportSession? GetSessionByCustomerConnection(string connectionId)
    {
        return _sessions.Values.FirstOrDefault(s =>
            s.CustomerConnectionId == connectionId && s.IsActive);
    }

    public SupportSession? GetSessionByAdminConnection(string connectionId)
    {
        return _sessions.Values.FirstOrDefault(s =>
            s.AdminConnectionId == connectionId && s.IsActive);
    }

    public void AssignAdmin(string sessionId, string adminUserId, string adminConnectionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.AdminUserId = adminUserId;
            session.AdminConnectionId = adminConnectionId;
        }
    }

    public void AddMessage(string sessionId, string sender, string content)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Messages.Add(new SupportMessage
            {
                Sender = sender,
                Content = content
            });
        }
    }

    public void EndSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsActive = false;
        }
    }

    public void SetVisitorInfo(string sessionId, string? ip, string? userAgent, string? pageUrl,
        string? country, string? city, string? isp)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IpAddress = ip;
            session.UserAgent = userAgent;
            session.PageUrl = pageUrl;
            session.Country = country;
            session.City = city;
            session.Isp = isp;
        }
    }

    public void SetTypingText(string sessionId, string? text)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.CurrentTypingText = text;
        }
    }

    public void CleanupOldSessions()
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);
        var old = _sessions.Where(kv => !kv.Value.IsActive && kv.Value.CreatedDate < cutoff)
            .Select(kv => kv.Key).ToList();
        foreach (var key in old) _sessions.TryRemove(key, out _);
    }
}
