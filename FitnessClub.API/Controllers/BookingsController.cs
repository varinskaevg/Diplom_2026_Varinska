using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;
using FitnessClub.API.Models;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingsController(AppDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> GetAll([FromQuery] int? scheduleId)
    {
        var query = _db.Bookings
            .Include(b => b.Client).ThenInclude(c => c.User)
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .AsQueryable();

        if (scheduleId.HasValue)
            query = query.Where(b => b.ScheduleId == scheduleId.Value);

        var bookings = await query.ToListAsync();

        var bookingIds = bookings.Select(b => b.Id).ToList();
        var payments = await _db.Payments
            .Where(p => p.Description != null && bookingIds.Any(id => p.Description.Contains("Booking:" + id)))
            .ToListAsync();

        var result = bookings.Select(b =>
        {
            var payment = payments.FirstOrDefault(p =>
                p.Description != null && p.Description.Contains("Booking:" + b.Id));
            return new
            {
                b.Id,
                b.ScheduleId,
                b.ClientId,
                ClientName = b.Client.User.FirstName + " " + b.Client.User.LastName,
                ClientPhone = b.Client.User.Phone,
                b.Status,
                b.BookedAt,
                ClassName = b.Schedule.ClassType.Name,
                ExtraCharge = payment?.Amount ?? 0m,
                ChargeReason = payment != null
                    ? payment.Description!.Split(" | Booking:")[0]
                    : (string?)null,
                // ✅ НОВІ поля
                Source = b.Source,
                TelegramStatus = b.TelegramStatus
            };
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var schedule = await _db.Schedules
            .Include(s => s.Bookings)
            .Include(s => s.ClassType)
            .Include(s => s.Trainer)
            .FirstOrDefaultAsync(s => s.Id == dto.ScheduleId);

        if (schedule == null)
            return NotFound(new { error = "Заняття не знайдено" });

        if (schedule.Status == "Cancelled")
            return BadRequest(new { error = "Заняття скасовано" });

        var exists = await _db.Bookings.AnyAsync(b =>
            b.ScheduleId == dto.ScheduleId &&
            b.ClientId == dto.ClientId &&
            b.Status != "Cancelled");
        if (exists)
            return BadRequest(new { error = "Клієнт вже записаний на це заняття" });

        if (schedule.MaxCapacity > 0 &&
            schedule.Bookings.Count(b => b.Status != "Cancelled") >= schedule.MaxCapacity)
            return BadRequest(new { error = "Немає вільних місць" });

        var today = DateOnly.FromDateTime(DateTime.Today);
        var membership = await _db.Memberships
            .Include(m => m.MembershipType)
            .Where(m => m.ClientId == dto.ClientId
                     && m.Status == "Active"
                     && m.EndDate >= today)
            .OrderByDescending(m => m.EndDate)
            .FirstOrDefaultAsync();

        decimal extraCharge = 0;
        string chargeReason = "";
        bool needsPayment = false;

        if (membership == null || !membership.MembershipType.IncludesClasses)
        {
            needsPayment = true;
            var trainer = schedule.Trainer;

            if (schedule.ClassType.IsIndividual)
            {
                extraCharge = trainer?.IndividualRate ?? trainer?.HourlyRate ?? 0;
                chargeReason = $"Індивідуальне заняття «{schedule.ClassType.Name}» (без абонементу)";
            }
            else
            {
                extraCharge = trainer?.GroupRate ?? trainer?.HourlyRate ?? 0;
                chargeReason = $"Групове заняття «{schedule.ClassType.Name}» (без абонементу)";
            }
        }

        var booking = new Booking
        {
            ScheduleId = dto.ScheduleId,
            ClientId = dto.ClientId,
            Status = "Confirmed",
            Source = "Manual",           // ✅ завжди Manual при записі з десктопу
            TelegramStatus = "None"      // ✅ без Telegram-підтвердження
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        if (needsPayment && extraCharge > 0)
        {
            var payment = new Payment
            {
                ClientId = dto.ClientId,
                Amount = extraCharge,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = dto.PaymentMethod ?? "Cash",
                Status = "Completed",
                Description = $"{chargeReason} | Booking:{booking.Id}"
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            booking.Id,
            ExtraCharge = extraCharge,
            ChargeReason = chargeReason,
            HasMembership = membership != null,
            MembershipName = membership?.MembershipType.Name,
            IncludesClasses = membership?.MembershipType.IncludesClasses ?? false,
            NeedsPayment = needsPayment,
            Source = booking.Source
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();
        booking.Status = "Cancelled";
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("available-clients")]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> GetAvailableClients(
        [FromQuery] int scheduleId, [FromQuery] string? search)
    {
        var bookedClientIds = await _db.Bookings
            .Where(b => b.ScheduleId == scheduleId && b.Status != "Cancelled")
            .Select(b => b.ClientId)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _db.Clients
            .Include(c => c.User)
            .Include(c => c.Memberships).ThenInclude(m => m.MembershipType)
            .Where(c => c.User.IsActive && !bookedClientIds.Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(c =>
                c.User.FirstName.ToLower().Contains(search) ||
                c.User.LastName.ToLower().Contains(search) ||
                c.User.Phone!.Contains(search));
        }

        var clients = await query.Select(c => new
        {
            c.Id,
            c.User.FirstName,
            c.User.LastName,
            c.User.Phone,
            ActiveMembership = c.Memberships
                .Where(m => m.Status == "Active" && m.EndDate >= today)
                .Select(m => new
                {
                    m.MembershipType.Name,
                    m.MembershipType.IncludesClasses,
                    m.EndDate
                })
                .FirstOrDefault()
        }).ToListAsync();

        return Ok(clients);
    }
}

public record CreateBookingDto(int ScheduleId, int ClientId, string? PaymentMethod = "Cash");