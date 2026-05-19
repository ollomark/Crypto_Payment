using System.Security.Claims;
using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly PresenceTracker _tracker;
    private readonly AppDbContext _db;

    public ChatHub(PresenceTracker tracker, AppDbContext db)
    {
        _tracker = tracker;
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        await _tracker.UserConnected(userId, Context.ConnectionId);
        await Clients.Others.SendAsync("UserOnline", userId);
        await Clients.Caller.SendAsync("OnlineUsers", _tracker.GetOnlineUsers());

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            var isOffline = await _tracker.UserDisconnected(userId, Context.ConnectionId);
            if (isOffline)
                await Clients.Others.SendAsync("UserOffline", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string receiverId, string content)
    {
        var senderId = GetUserId();
        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(content)) return;

        var msg = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            CreatedDate = DateTime.UtcNow
        };
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var senderName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Bilinmeyen";

        var payload = new
        {
            msg.Id,
            msg.SenderId,
            SenderName = senderName,
            msg.ReceiverId,
            msg.Content,
            msg.FileUrl,
            msg.FileName,
            msg.FileSize,
            msg.CreatedDate
        };

        foreach (var connId in _tracker.GetConnections(receiverId))
            await Clients.Client(connId).SendAsync("ReceiveMessage", payload);

        await Clients.Caller.SendAsync("MessageSent", payload);
    }

    public async Task SendFileMessage(string receiverId, string fileUrl, string fileName, long fileSize)
    {
        var senderId = GetUserId();
        if (string.IsNullOrEmpty(senderId)) return;

        var msg = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            CreatedDate = DateTime.UtcNow
        };
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var senderName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Bilinmeyen";

        var payload = new
        {
            msg.Id,
            msg.SenderId,
            SenderName = senderName,
            msg.ReceiverId,
            msg.Content,
            msg.FileUrl,
            msg.FileName,
            msg.FileSize,
            msg.CreatedDate
        };

        foreach (var connId in _tracker.GetConnections(receiverId))
            await Clients.Client(connId).SendAsync("ReceiveMessage", payload);

        await Clients.Caller.SendAsync("MessageSent", payload);
    }

    public async Task MarkAsRead(string otherUserId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var unread = await _db.ChatMessages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
            .ToListAsync();

        foreach (var m in unread) m.IsRead = true;
        await _db.SaveChangesAsync();

        foreach (var connId in _tracker.GetConnections(otherUserId))
            await Clients.Client(connId).SendAsync("MessagesRead", userId);
    }

    public async Task Typing(string receiverId)
    {
        var senderId = GetUserId();
        if (string.IsNullOrEmpty(senderId)) return;

        foreach (var connId in _tracker.GetConnections(receiverId))
            await Clients.Client(connId).SendAsync("UserTyping", senderId);
    }

    private string? GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
