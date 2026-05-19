using System.Security.Claims;
using Crypto_Payment.Data;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly IR2StorageService _r2;
    private readonly PresenceTracker _tracker;

    public ChatController(AppDbContext db, UserManager<User> userManager, IR2StorageService r2, PresenceTracker tracker)
    {
        _db = db;
        _userManager = userManager;
        _r2 = r2;
        _tracker = tracker;
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var users = await _userManager.Users
            .Where(u => u.IsActive && u.Id != userId)
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToListAsync();

        var onlineIds = _tracker.GetOnlineUsers();

        var lastMessages = await _db.ChatMessages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
            .Select(g => new
            {
                UserId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedDate).First().Content,
                LastDate = g.Max(m => m.CreatedDate),
                UnreadCount = g.Count(m => m.ReceiverId == userId && !m.IsRead)
            })
            .ToListAsync();

        var result = users.Select(u =>
        {
            var lm = lastMessages.FirstOrDefault(l => l.UserId == u.Id);
            return new
            {
                u.Id,
                Name = u.FullName ?? u.UserName ?? u.Email,
                u.Email,
                IsOnline = onlineIds.Contains(u.Id),
                LastMessage = lm?.LastMessage,
                LastDate = lm?.LastDate,
                UnreadCount = lm?.UnreadCount ?? 0
            };
        })
        .OrderByDescending(x => x.LastDate ?? DateTime.MinValue)
        .ToList();

        return Ok(result);
    }

    [HttpGet("history/{otherUserId}")]
    public async Task<IActionResult> GetHistory(string otherUserId, [FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var messages = await _db.ChatMessages
            .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId)
                     || (m.SenderId == otherUserId && m.ReceiverId == userId))
            .OrderByDescending(m => m.CreatedDate)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                m.ReceiverId,
                m.Content,
                m.FileUrl,
                m.FileName,
                m.FileSize,
                m.IsRead,
                m.CreatedDate
            })
            .ToListAsync();

        return Ok(messages.OrderBy(m => m.CreatedDate));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var count = await _db.ChatMessages
            .CountAsync(m => m.ReceiverId == userId && !m.IsRead);
        return Ok(new { count });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Dosya seçilmedi" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "Dosya 10MB'dan büyük olamaz" });

        try
        {
            using var stream = file.OpenReadStream();
            var url = await _r2.UploadFileAsync(stream, file.FileName, file.ContentType);
            return Ok(new { url, fileName = file.FileName, fileSize = file.Length });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("mark-read/{otherUserId}")]
    public async Task<IActionResult> MarkRead(string otherUserId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var unread = await _db.ChatMessages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
            .ToListAsync();

        foreach (var m in unread) m.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(new { marked = unread.Count });
    }
}
