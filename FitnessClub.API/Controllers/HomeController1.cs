// Controllers/TrainerBookingsController.cs
using FitnessClub.API.Data;
using FitnessClub.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/bot")]
[Authorize(Roles = "Admin,Trainer")]
public class TrainerBookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITelegramService _telegram;

    public TrainerBookingsController(AppDbContext db, ITelegramService telegram)
    {
        _db = db;
        _telegram = telegram;
    }

    // ══════════════════════════════════════════════════════════
    //  ЗАПИТИ НА БРОНЮВАННЯ З TELEGRAM (для тренерської вкладки)
    // ══════════════════════════════════════════════════════════
    [HttpGet("pending-bookings")]
    public async Task<IActionResult> GetPendingBookings([FromQuery] int? trainerId)
    {
        var query = _db.Bookings
            .Include(b => b.Client).ThenInclude(c => c.User)
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .Include(b => b.Schedule).ThenInclude(s => s.Trainer).ThenInclude(t => t.User)
            .Where(b => b.TelegramStatus == "Pending");

        if (trainerId.HasValue)
            query = query.Where(b => b.Schedule.TrainerId == trainerId.Value);

        var bookings = await query
            .OrderBy(b => b.Schedule.StartDatetime)
            .Select(b => new
            {
                b.Id,
                b.ScheduleId,
                ClientName = b.Client.User.FirstName + " " + b.Client.User.LastName,
                ClientPhone = b.Client.User.Phone,
                ClientChatId = b.Client.User.TelegramChatId,
                ClassName = b.Schedule.ClassType.Name,
                ScheduleDate = b.Schedule.StartDatetime,
                StartTime = b.Schedule.StartDatetime.ToLocalTime().ToString("HH:mm"),
                EndTime = b.Schedule.EndDatetime.ToLocalTime().ToString("HH:mm"),
                Room = b.Schedule.Room,
                b.BookedAt,
                b.TelegramStatus,
                b.Source
            })
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpPost("booking/{id}/approve")]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Client).ThenInclude(c => c.User)
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        booking.TelegramStatus = "Confirmed";
        booking.Status = "Confirmed";
        await _db.SaveChangesAsync();

        if (booking.Client.User.TelegramChatId.HasValue)
        {
            var localTime = booking.Schedule.StartDatetime.ToLocalTime();
            var msg = $"✅ *Запис підтверджено\\!*\n\n" +
                      $"📅 *{EscapeMd(booking.Schedule.ClassType.Name)}*\n" +
                      $"🕐 {EscapeMd(localTime.ToString("dd.MM.yyyy"))} о {EscapeMd(localTime.ToString("HH:mm"))}";
            if (!string.IsNullOrEmpty(booking.Schedule.Room))
                msg += $"\n🚪 Зал: {EscapeMd(booking.Schedule.Room)}";

            try { await _telegram.SendMessageAsync(booking.Client.User.TelegramChatId.Value, msg); }
            catch { /* не критично */ }
        }

        return Ok(new { success = true });
    }

    [HttpPost("booking/{id}/reject")]
    public async Task<IActionResult> RejectBooking(int id, [FromBody] RejectBookingRequest? request)
    {
        var booking = await _db.Bookings
            .Include(b => b.Client).ThenInclude(c => c.User)
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        booking.TelegramStatus = "Rejected";
        booking.Status = "Cancelled";
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (booking.Client.User.TelegramChatId.HasValue)
        {
            var localTime = booking.Schedule.StartDatetime.ToLocalTime();
            var reason = string.IsNullOrEmpty(request?.Reason) ? "" : $"\n💬 Причина: {EscapeMd(request.Reason)}";
            var msg = $"❌ *Запис не підтверджено*\n\n" +
                      $"📅 {EscapeMd(booking.Schedule.ClassType.Name)}\n" +
                      $"🕐 {EscapeMd(localTime.ToString("dd.MM.yyyy HH:mm"))}" +
                      reason +
                      $"\n\nЗверніться до адміністратора або оберіть інший час\\. /schedule";

            try { await _telegram.SendMessageAsync(booking.Client.User.TelegramChatId.Value, msg); }
            catch { /* не критично */ }
        }

        return Ok(new { success = true });
    }

    private static string EscapeMd(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var c in new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' })
            text = text.Replace(c.ToString(), $"\\{c}");
        return text;
    }
}