using System.Security.Claims;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.SignalR;

namespace Crypto_Payment.Hubs;

public class SupportChatHub : Hub
{
    private readonly SupportSessionTracker _tracker;
    private readonly IHttpContextAccessor _httpAccessor;
    private const string AdminGroup = "support-admins";

    public SupportChatHub(SupportSessionTracker tracker, IHttpContextAccessor httpAccessor)
    {
        _tracker = tracker;
        _httpAccessor = httpAccessor;
    }

    public async Task JoinAsAdmin()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

        var sessions = _tracker.GetAllActiveSessions().Select(s => new
        {
            s.SessionId,
            s.CustomerName,
            s.OrderName,
            s.InvoiceId,
            s.CreatedDate,
            HasAdmin = s.AdminUserId != null,
            AdminUserId = s.AdminUserId,
            MessageCount = s.Messages.Count,
            s.IpAddress,
            s.UserAgent,
            s.PageUrl,
            s.Country,
            s.City,
            s.Isp
        });
        await Clients.Caller.SendAsync("ActiveSessions", sessions);
    }

    public async Task JoinSupport(int invoiceId, string customerName, string orderName)
    {
        var sessionId = _tracker.CreateSession(Context.ConnectionId, invoiceId, customerName, orderName);

        await Clients.Caller.SendAsync("SessionCreated", sessionId);

        await Clients.Group(AdminGroup).SendAsync("NewSupportSession", new
        {
            SessionId = sessionId,
            CustomerName = customerName,
            OrderName = orderName,
            InvoiceId = invoiceId,
            CreatedDate = DateTime.UtcNow,
            HasAdmin = false,
            MessageCount = 0,
            IpAddress = (string?)null,
            UserAgent = (string?)null,
            PageUrl = (string?)null,
            Country = (string?)null,
            City = (string?)null,
            Isp = (string?)null
        });
    }

    public async Task SendVisitorInfo(string sessionId, string? pageUrl, string? userAgent)
    {
        var httpContext = _httpAccessor.HttpContext;
        var ip = httpContext?.Connection.RemoteIpAddress?.ToString();

        var forwardedFor = httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
            ip = forwardedFor.Split(',')[0].Trim();

        _tracker.SetVisitorInfo(sessionId, ip, userAgent, pageUrl, null, null, null);

        var session = _tracker.GetSession(sessionId);
        if (session == null) return;

        await Clients.Group(AdminGroup).SendAsync("VisitorInfoUpdated", new
        {
            SessionId = sessionId,
            IpAddress = ip,
            UserAgent = userAgent,
            PageUrl = pageUrl,
            session.Country,
            session.City,
            session.Isp
        });
    }

    public async Task UpdateVisitorGeo(string sessionId, string? country, string? city, string? isp)
    {
        var session = _tracker.GetSession(sessionId);
        if (session == null) return;

        session.Country = country;
        session.City = city;
        session.Isp = isp;

        await Clients.Group(AdminGroup).SendAsync("VisitorInfoUpdated", new
        {
            SessionId = sessionId,
            session.IpAddress,
            session.UserAgent,
            session.PageUrl,
            Country = country,
            City = city,
            Isp = isp
        });
    }

    public async Task LiveTypingPreview(string sessionId, string? text)
    {
        _tracker.SetTypingText(sessionId, text);

        var session = _tracker.GetSession(sessionId);
        if (session?.AdminConnectionId != null)
        {
            await Clients.Client(session.AdminConnectionId)
                .SendAsync("LiveTypingPreview", sessionId, text ?? "");
        }
    }

    public async Task TakeSupport(string sessionId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Destek";
        if (string.IsNullOrEmpty(userId)) return;

        var session = _tracker.GetSession(sessionId);
        if (session == null || !session.IsActive) return;

        _tracker.AssignAdmin(sessionId, userId, Context.ConnectionId);

        await Clients.Client(session.CustomerConnectionId)
            .SendAsync("AdminJoined", userName);

        await Clients.Group(AdminGroup).SendAsync("SessionTaken", new
        {
            SessionId = sessionId,
            AdminUserId = userId,
            AdminName = userName
        });

        var history = session.Messages.Select(m => new
        {
            m.Sender,
            m.Content,
            m.CreatedDate
        });
        await Clients.Caller.SendAsync("SessionHistory", sessionId, history);

        await Clients.Caller.SendAsync("VisitorInfoUpdated", new
        {
            SessionId = sessionId,
            session.IpAddress,
            session.UserAgent,
            session.PageUrl,
            session.Country,
            session.City,
            session.Isp
        });
    }

    public async Task SendSupportMessage(string sessionId, string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        var session = _tracker.GetSession(sessionId);
        if (session == null || !session.IsActive) return;

        var isAdmin = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value != null
                      && Context.ConnectionId != session.CustomerConnectionId;
        var senderLabel = isAdmin ? "admin" : "customer";
        var senderName = isAdmin
            ? (Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Destek")
            : session.CustomerName;

        _tracker.AddMessage(sessionId, senderLabel, message);

        if (!isAdmin)
            _tracker.SetTypingText(sessionId, null);

        var payload = new
        {
            SessionId = sessionId,
            Sender = senderLabel,
            SenderName = senderName,
            Content = message,
            CreatedDate = DateTime.UtcNow
        };

        if (isAdmin && !string.IsNullOrEmpty(session.CustomerConnectionId))
            await Clients.Client(session.CustomerConnectionId).SendAsync("SupportMessage", payload);

        if (!isAdmin && !string.IsNullOrEmpty(session.AdminConnectionId))
            await Clients.Client(session.AdminConnectionId).SendAsync("SupportMessage", payload);

        await Clients.Caller.SendAsync("SupportMessageSent", payload);
    }

    public async Task CustomerTyping(string sessionId)
    {
        var session = _tracker.GetSession(sessionId);
        if (session?.AdminConnectionId != null)
            await Clients.Client(session.AdminConnectionId).SendAsync("SupportTyping", sessionId, "customer");
    }

    public async Task AdminTyping(string sessionId)
    {
        var session = _tracker.GetSession(sessionId);
        if (session?.CustomerConnectionId != null)
            await Clients.Client(session.CustomerConnectionId).SendAsync("SupportTyping", sessionId, "admin");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var customerSession = _tracker.GetSessionByCustomerConnection(Context.ConnectionId);
        if (customerSession != null)
        {
            _tracker.EndSession(customerSession.SessionId);

            if (!string.IsNullOrEmpty(customerSession.AdminConnectionId))
                await Clients.Client(customerSession.AdminConnectionId)
                    .SendAsync("CustomerLeft", customerSession.SessionId);

            await Clients.Group(AdminGroup)
                .SendAsync("SessionEnded", customerSession.SessionId);
        }

        var adminSession = _tracker.GetSessionByAdminConnection(Context.ConnectionId);
        if (adminSession != null)
        {
            adminSession.AdminConnectionId = null;
            adminSession.AdminUserId = null;

            await Clients.Client(adminSession.CustomerConnectionId)
                .SendAsync("AdminLeft");

            await Clients.Group(AdminGroup)
                .SendAsync("SessionUpdated", new
                {
                    adminSession.SessionId,
                    HasAdmin = false
                });
        }

        _tracker.CleanupOldSessions();
        await base.OnDisconnectedAsync(exception);
    }
}
