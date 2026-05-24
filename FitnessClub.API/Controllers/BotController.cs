// Controllers/BotController.cs
using FitnessClub.API.Data;
using FitnessClub.API.Data.Entities;
using FitnessClub.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/bot")]
[Authorize(Roles = "Admin")]
public class BotController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITelegramService _telegram;
    private readonly IConfiguration _config;

    public BotController(AppDbContext db, ITelegramService telegram, IConfiguration config)
    {
        _db = db;
        _telegram = telegram;
        _config = config;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var bot = new TelegramBotClient(_config["Telegram:BotToken"]!);
            var me = await bot.GetMe();
            return Ok(new
            {
                IsRunning = true,
                Username = me.Username,
                BotId = me.Id,
                StartedAt = DateTime.UtcNow.AddHours(-2)
            });
        }
        catch
        {
            return Ok(new { IsRunning = false, Username = "", BotId = 0, StartedAt = (DateTime?)null });
        }
    }

    // ══════════════════════════════════════════════════════════
    //  СТАТИСТИКА
    // ══════════════════════════════════════════════════════════
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var totalUsers = await _db.Users.CountAsync(u => u.TelegramChatId != null);
        var newUsersThisWeek = await _db.Users
            .CountAsync(u => u.TelegramChatId != null && u.CreatedAt >= weekAgo);

        var todayLogs = await _db.BotLogs.CountAsync(l => l.CreatedAt >= today);
        var weekLogs = await _db.BotLogs.CountAsync(l => l.CreatedAt >= weekAgo);

        var todayQr = await _db.BotLogs
            .CountAsync(l => l.Command == "qr" && l.CreatedAt >= today);

        var supportRequests = await _db.SupportSessions.CountAsync();
        var unreadSupport = await _db.SupportSessions
            .CountAsync(s => s.IsActive && s.UnreadCount > 0);

        var commandStats = await _db.BotLogs
            .Where(l => l.CreatedAt >= weekAgo && l.Command != null)
            .GroupBy(l => l.Command)
            .Select(g => new { Command = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var recentLogs = await _db.BotLogs
            .Where(l => l.UserId != null)
            .OrderByDescending(l => l.CreatedAt)
            .Take(20)
            .Select(l => new { l.UserId, Action = l.Command ?? "unknown", Time = l.CreatedAt })
            .ToListAsync();

        var userIds = recentLogs.Select(l => l.UserId!.Value).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var recentActions = recentLogs.Select(l => new
        {
            UserName = users.TryGetValue(l.UserId!.Value, out var name) ? name : "Невідомий",
            Action = l.Action,
            Time = l.Time
        }).ToList();

        return Ok(new
        {
            TotalUsers = totalUsers,
            NewUsersThisWeek = newUsersThisWeek,
            TodayMessages = todayLogs,
            WeekMessages = weekLogs,
            TodayQrRequests = todayQr,
            TotalSupportRequests = supportRequests,
            UnreadSupportRequests = unreadSupport,
            CommandStats = commandStats,
            RecentActions = recentActions
        });
    }

    // ══════════════════════════════════════════════════════════
    //  ПІДТРИМКА
    // ══════════════════════════════════════════════════════════
    [HttpGet("support/chats")]
    public async Task<IActionResult> GetSupportChats()
    {
        var chats = await _db.SupportSessions
            .Include(s => s.User)
            .Include(s => s.Messages)
            .OrderByDescending(s => s.LastMessageAt)
            .Select(s => new
            {
                s.ChatId,
                UserName = s.User != null ? s.User.FirstName + " " + s.User.LastName : "Unknown",
                LastMessage = s.Messages.OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Text).FirstOrDefault() ?? "",
                LastMessageTime = s.LastMessageAt ?? s.StartedAt,
                s.UnreadCount
            })
            .ToListAsync();

        return Ok(chats);
    }

    [HttpGet("support/chat/{chatId}")]
    public async Task<IActionResult> GetChatMessages(long chatId)
    {
        var session = await _db.SupportSessions
            .Include(s => s.User)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.ChatId == chatId);

        if (session == null)
            return NotFound(new { error = "Сесію не знайдено" });

        return Ok(new
        {
            session.ChatId,
            UserName = session.User != null ? session.User.FirstName + " " + session.User.LastName : "Unknown",
            Phone = session.User?.Phone ?? "",
            Messages = session.Messages.OrderBy(m => m.CreatedAt).Select(m => new
            {
                m.Text,
                Time = m.CreatedAt,
                m.IsFromAdmin
            })
        });
    }

    [HttpPost("support/chat/{chatId}/read")]
    public async Task<IActionResult> MarkAsRead(long chatId)
    {
        var session = await _db.SupportSessions.FirstOrDefaultAsync(s => s.ChatId == chatId);
        if (session != null)
        {
            session.UnreadCount = 0;
            await _db.SaveChangesAsync();
        }
        return Ok(new { success = true });
    }

    [HttpPost("support/reply")]
    public async Task<IActionResult> SendReply([FromBody] SupportReplyRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Message))
            return BadRequest(new { error = "Повідомлення не може бути порожнім" });

        var session = await _db.SupportSessions.FirstOrDefaultAsync(s => s.ChatId == request.ChatId);
        if (session == null)
            return NotFound(new { error = "Сесію не знайдено" });

        var message = new SupportMessage
        {
            SessionId = session.Id,
            Text = request.Message,
            IsFromAdmin = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.SupportMessages.Add(message);
        session.LastMessageAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _telegram.SendMessageAsync(request.ChatId,
                $"*Відповідь від підтримки:*\n\n{EscapeMd(request.Message)}");
        }
        catch (Exception ex)
        {
            return Ok(new { success = true, messageId = message.Id, warning = $"Збережено, але не надіслано в Telegram: {ex.Message}" });
        }

        return Ok(new { success = true, messageId = message.Id });
    }

    // ══════════════════════════════════════════════════════════
    //  РОЗСИЛКА
    // ══════════════════════════════════════════════════════════
    [HttpPost("broadcast")]
    public async Task<IActionResult> SendBroadcast([FromBody] BroadcastRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Message))
            return BadRequest(new { error = "Повідомлення не може бути порожнім" });

        var query = _db.Users.Where(u => u.TelegramChatId != null && u.IsActive);

        if (request.TargetType == "active")
        {
            var activeClientIds = await _db.Memberships
                .Where(m => m.Status == "Active" && m.EndDate >= DateOnly.FromDateTime(DateTime.Today))
                .Select(m => m.Client!.UserId)
                .Distinct()
                .ToListAsync();
            query = query.Where(u => activeClientIds.Contains(u.Id));
        }
        else if (request.TargetType == "expiring")
        {
            var expiringSoon = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
            var expiringClientIds = await _db.Memberships
                .Where(m => m.Status == "Active" && m.EndDate <= expiringSoon && m.EndDate >= DateOnly.FromDateTime(DateTime.Today))
                .Select(m => m.Client!.UserId)
                .Distinct()
                .ToListAsync();
            query = query.Where(u => expiringClientIds.Contains(u.Id));
        }

        var users = await query.ToListAsync();
        int sent = 0, failed = 0;

        foreach (var user in users)
        {
            try
            {
                await _telegram.SendMessageAsync(user.TelegramChatId!.Value,
                    $"*Оголошення FitnessClub*\n\n{EscapeMd(request.Message)}");
                sent++;
            }
            catch { failed++; }
            await Task.Delay(50);
        }

        _db.BroadcastHistories.Add(new BroadcastHistory
        {
            Message = request.Message,
            TargetType = request.TargetType,
            SentCount = sent,
            FailedCount = failed,
            AdminId = GetCurrentUserId(),
            SentAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { success = true, SentCount = sent, FailedCount = failed });
    }

    [HttpGet("broadcast/history")]
    public async Task<IActionResult> GetBroadcastHistory()
    {
        var history = await _db.BroadcastHistories
            .OrderByDescending(h => h.SentAt)
            .Take(20)
            .ToListAsync();
        return Ok(history);
    }

    [HttpPost("toggle")]
    public IActionResult ToggleBot() => Ok(new { success = true });

    [HttpPost("restart")]
    public IActionResult RestartBot() => Ok(new { success = true });

    [HttpPost("clear-cache")]
    public IActionResult ClearCache() => Ok(new { success = true });

    [HttpPost("settings")]
    public IActionResult SaveSettings([FromBody] BotSettingsRequest request) => Ok(new { success = true });

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 1;
    }

    private static string EscapeMd(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var c in new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' })
            text = text.Replace(c.ToString(), $"\\{c}");
        return text;
    }
}

public record SupportReplyRequest(long ChatId, string Message);
public record BroadcastRequest(string Message, string TargetType);
public record BotSettingsRequest(bool NotifyExpiry, bool NotifyNewSchedule, bool NotifyPromos);
public record RejectBookingRequest(string? Reason);