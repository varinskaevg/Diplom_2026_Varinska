using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;
using FitnessClub.API.Models;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly AppDbContext _db;
    public SchedulesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] DateTime? weekStart)
    {
        var query = _db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Bookings)
            .AsQueryable();

        if (weekStart.HasValue)
        {
            var start = DateTime.SpecifyKind(weekStart.Value.Date, DateTimeKind.Utc);
            var end = start.AddDays(7);
            query = query.Where(s => s.StartDatetime >= start && s.StartDatetime < end);
        }
        else if (date.HasValue)
        {
            var dayStart = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(s => s.StartDatetime >= dayStart && s.StartDatetime < dayEnd);
        }

        var result = await query.OrderBy(s => s.StartDatetime).Select(s => new
        {
            s.Id,
            ClassName = s.ClassType.Name,
            ClassTypeId = s.ClassType.Id,
            IsIndividual = s.ClassType.IsIndividual,
            TrainerName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
            TrainerId = s.TrainerId,
            s.StartDatetime,
            s.EndDatetime,
            s.Room,
            s.Status,
            s.MaxCapacity,
            BookingsCount = s.Bookings.Count,
        }).ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await _db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Bookings).ThenInclude(b => b.Client).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (s == null) return NotFound();

        return Ok(new
        {
            s.Id,
            ClassName = s.ClassType.Name,
            ClassTypeId = s.ClassTypeId,
            IsIndividual = s.ClassType.IsIndividual,
            TrainerName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
            TrainerId = s.TrainerId,
            s.StartDatetime,
            s.EndDatetime,
            s.Room,
            s.Status,
            s.MaxCapacity,
            s.Notes,
            BookingsCount = s.Bookings.Count,
            Bookings = s.Bookings.Select(b => new
            {
                b.Id,
                ClientName = b.Client.User.FirstName + " " + b.Client.User.LastName,
                b.Status
            })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> Create([FromBody] CreateScheduleDto dto)
    {
        var schedule = new Schedule
        {
            ClassTypeId = dto.ClassTypeId,
            TrainerId = dto.TrainerId,
            // Клієнт надсилає рядок без timezone → десеріалізується як Unspecified
            // SpecifyKind(Utc) каже Npgsql що це UTC — значення не змінюється
            StartDatetime = DateTime.SpecifyKind(dto.StartDatetime, DateTimeKind.Utc),
            EndDatetime = DateTime.SpecifyKind(dto.EndDatetime, DateTimeKind.Utc),
            Room = dto.Room,
            MaxCapacity = dto.MaxCapacity,
            Status = "Scheduled",
            Notes = dto.Notes
        };
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();
        return Ok(new { schedule.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateScheduleDto dto)
    {
        var schedule = await _db.Schedules.FindAsync(id);
        if (schedule == null) return NotFound();

        schedule.ClassTypeId = dto.ClassTypeId;
        schedule.TrainerId = dto.TrainerId;
        schedule.StartDatetime = DateTime.SpecifyKind(dto.StartDatetime, DateTimeKind.Utc);
        schedule.EndDatetime = DateTime.SpecifyKind(dto.EndDatetime, DateTimeKind.Utc);
        schedule.Room = dto.Room;
        schedule.MaxCapacity = dto.MaxCapacity;
        schedule.Status = dto.Status;
        schedule.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> Delete(int id)
    {
        var schedule = await _db.Schedules.FindAsync(id);
        if (schedule == null) return NotFound();
        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("classtypes")]
    public async Task<IActionResult> GetClassTypes()
    {
        var types = await _db.ClassTypes
            .Select(t => new { t.Id, t.Name, t.IsIndividual })
            .ToListAsync();
        return Ok(types);
    }
}

public record CreateScheduleDto(
    int ClassTypeId, int TrainerId,
    DateTime StartDatetime, DateTime EndDatetime,
    string? Room, int MaxCapacity, string? Notes
);
public record UpdateScheduleDto(
    int ClassTypeId, int TrainerId,
    DateTime StartDatetime, DateTime EndDatetime,
    string? Room, int MaxCapacity, string Status, string? Notes
);