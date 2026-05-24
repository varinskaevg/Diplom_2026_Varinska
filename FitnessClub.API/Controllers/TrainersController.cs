using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;
using FitnessClub.API.Models;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrainersController : ControllerBase
{
    private readonly AppDbContext _db;
    public TrainersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var trainers = await _db.Trainers
            .Include(t => t.User)
            .Include(t => t.Schedules).ThenInclude(s => s.ClassType)
            .Select(t => new
            {
                t.Id,
                t.User.FirstName,
                t.User.LastName,
                t.User.Phone,
                t.User.Email,
                t.User.IsActive,
                t.Specialization,
                t.ExperienceYears,
                t.HourlyRate,
                t.PhotoUrl,
                t.Bio,
                t.CreatedAt,
                t.GroupRate,
                t.IndividualRate,
                t.MonthlyPlanRate,
                TotalSchedules = t.Schedules.Count,
                CompletedSchedules = t.Schedules.Count(s => s.Status == "Completed")
            })
            .ToListAsync();
        return Ok(trainers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var t = await _db.Trainers
            .Include(t => t.User)
            .Include(t => t.Schedules).ThenInclude(s => s.ClassType)
            .Include(t => t.Schedules).ThenInclude(s => s.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (t == null) return NotFound();

        var completedSchedules = t.Schedules.Where(s => s.Status == "Completed").ToList();
        var totalSessions = completedSchedules.Count;

        var totalIndividualSessions = completedSchedules.Count(s => s.ClassType.IsIndividual);
        var totalGroupSessions = completedSchedules.Count(s => !s.ClassType.IsIndividual);

        var totalEarnings = completedSchedules.Sum(s =>
        {
            if (s.ClassType.IsIndividual)
                return t.IndividualRate ?? t.HourlyRate ?? 0;
            else
                return t.GroupRate ?? t.HourlyRate ?? 0;
        });

        return Ok(new
        {
            t.Id,
            t.User.FirstName,
            t.User.LastName,
            t.User.Phone,
            t.User.Email,
            t.User.IsActive,
            t.Specialization,
            t.ExperienceYears,
            t.HourlyRate,
            t.PhotoUrl,
            t.Bio,
            t.CreatedAt,
            t.GroupRate,
            t.IndividualRate,
            t.MonthlyPlanRate,
            TotalSchedules = t.Schedules.Count,
            CompletedSchedules = totalSessions,
            TotalGroupSessions = totalGroupSessions,
            TotalIndividualSessions = totalIndividualSessions,
            TotalEarnings = totalEarnings,
            RecentSchedules = t.Schedules
                .OrderByDescending(s => s.StartDatetime)
                .Take(10)
                .Select(s => new
                {
                    s.Id,
                    ClassName = s.ClassType.Name,
                    IsIndividual = s.ClassType.IsIndividual,
                    s.StartDatetime,
                    s.EndDatetime,
                    s.Status,
                    s.Room,
                    BookingsCount = s.Bookings.Count
                })
        });
    }

    [HttpGet("{id}/salary")]
    public async Task<IActionResult> GetSalary(int id,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var trainer = await _db.Trainers
            .Include(t => t.User)
            .Include(t => t.Schedules).ThenInclude(s => s.Bookings)
            .Include(t => t.Schedules).ThenInclude(s => s.ClassType)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trainer == null) return NotFound();

        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Utc);

        var schedules = trainer.Schedules
            .Where(s => s.StartDatetime >= fromUtc && s.StartDatetime < toUtc
                        && s.Status == "Completed")
            .ToList();

        // Групуємо заняття по днях для розрахунку бонусу за заняття
        var sessionsByDay = schedules
            .GroupBy(s => s.StartDatetime.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var sessions = schedules.Select(s =>
        {
            var isIndividual = s.ClassType.IsIndividual;
            var hours = Math.Round((s.EndDatetime - s.StartDatetime).TotalHours, 2);
            var participants = s.Bookings.Count;

            decimal baseEarning;
            string rateType;

            if (isIndividual)
            {
                baseEarning = trainer.IndividualRate.HasValue && trainer.IndividualRate > 0
                    ? trainer.IndividualRate.Value
                    : (decimal)hours * (trainer.HourlyRate ?? 0);
                rateType = "individual";
            }
            else
            {
                baseEarning = trainer.GroupRate.HasValue && trainer.GroupRate > 0
                    ? trainer.GroupRate.Value
                    : (decimal)hours * (trainer.HourlyRate ?? 0);
                rateType = "group";
            }

            // Бонус за заняття: 100 грн/урок, або 150 якщо в цей день ≥7 уроків
            var dayCount = sessionsByDay[s.StartDatetime.Date];
            var sessionBonus = dayCount >= 7 ? 150m : 100m;

            // Бонус за години: 100 грн за кожну годину заняття
            var hourlyBonus = (decimal)hours * 100m;

            var bonus = sessionBonus + hourlyBonus;
            var total = baseEarning + bonus;

            return new
            {
                s.Id,
                ClassName = s.ClassType.Name,
                IsIndividual = isIndividual,
                Date = s.StartDatetime,
                Hours = hours,
                RateType = rateType,
                Participants = participants,
                DaySessionCount = dayCount,
                BaseEarning = Math.Round(baseEarning, 2),
                SessionBonus = Math.Round(sessionBonus, 2),
                HourlyBonus = Math.Round(hourlyBonus, 2),
                Bonus = Math.Round(bonus, 2),
                Total = Math.Round(total, 2)
            };
        }).ToList();

        var isFullMonth = from.Day == 1 && to == new DateTime(to.Year, to.Month,
            DateTime.DaysInMonth(to.Year, to.Month));
        var monthlyBonus = (isFullMonth && trainer.MonthlyPlanRate.HasValue && trainer.MonthlyPlanRate > 0)
            ? trainer.MonthlyPlanRate.Value
            : 0m;

        return Ok(new
        {
            TrainerName = trainer.User.FirstName + " " + trainer.User.LastName,
            HourlyRate = trainer.HourlyRate ?? 0,
            IndividualRate = trainer.IndividualRate ?? 0,
            GroupRate = trainer.GroupRate ?? 0,
            MonthlyPlanRate = trainer.MonthlyPlanRate ?? 0,
            From = from,
            To = to,
            TotalSessions = sessions.Count,
            IndividualSessions = sessions.Count(s => s.IsIndividual),
            GroupSessions = sessions.Count(s => !s.IsIndividual),
            TotalHours = sessions.Sum(s => s.Hours),
            BaseTotal = sessions.Sum(s => s.BaseEarning),
            SessionBonusTotal = sessions.Sum(s => s.SessionBonus),
            HourlyBonusTotal = sessions.Sum(s => s.HourlyBonus),
            BonusTotal = sessions.Sum(s => s.Bonus) + monthlyBonus,
            MonthlyPlanBonus = monthlyBonus,
            GrandTotal = sessions.Sum(s => s.Total) + monthlyBonus,
            Sessions = sessions
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateTrainerDto dto)
    {
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (existingUser != null)
            return BadRequest(new { error = "Email вже використовується" });

        var trainerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Trainer");
        if (trainerRole == null) return BadRequest(new { error = "Роль Trainer не знайдена" });

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            RoleId = trainerRole.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var trainer = new Trainer
        {
            UserId = user.Id,
            Specialization = dto.Specialization,
            ExperienceYears = dto.ExperienceYears,
            HourlyRate = dto.HourlyRate,
            GroupRate = dto.GroupRate,
            IndividualRate = dto.IndividualRate,
            MonthlyPlanRate = dto.MonthlyPlanRate,
            Bio = dto.Bio
        };
        _db.Trainers.Add(trainer);
        await _db.SaveChangesAsync();
        return Ok(new { trainerId = trainer.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTrainerDto dto)
    {
        var trainer = await _db.Trainers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
        if (trainer == null) return NotFound();

        trainer.User.FirstName = dto.FirstName;
        trainer.User.LastName = dto.LastName;
        trainer.User.Phone = dto.Phone;
        trainer.Specialization = dto.Specialization;
        trainer.ExperienceYears = dto.ExperienceYears;
        trainer.HourlyRate = dto.HourlyRate;
        trainer.GroupRate = dto.GroupRate;
        trainer.IndividualRate = dto.IndividualRate;
        trainer.MonthlyPlanRate = dto.MonthlyPlanRate;
        trainer.Bio = dto.Bio;
        trainer.User.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var trainer = await _db.Trainers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
        if (trainer == null) return NotFound();
        trainer.User.IsActive = false;
        trainer.User.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        var trainer = await _db.Trainers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
        if (trainer == null) return NotFound();
        trainer.User.IsActive = true;
        trainer.User.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Roles = "Trainer")]
    public async Task<IActionResult> GetMe()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var trainer = await _db.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.User.Email == email);

        if (trainer == null) return NotFound();

        return Ok(new { trainer.Id, trainer.User.Email });
    }
}

public record CreateTrainerDto(
    string FirstName, string LastName, string Email,
    string Password, string? Phone, string? Specialization,
    int ExperienceYears, decimal? HourlyRate,
    decimal? GroupRate, decimal? IndividualRate,
    decimal? MonthlyPlanRate, string? Bio
);

public record UpdateTrainerDto(
    string FirstName, string LastName, string? Phone,
    string? Specialization, int ExperienceYears,
    decimal? HourlyRate, decimal? GroupRate,
    decimal? IndividualRate, decimal? MonthlyPlanRate,
    string? Bio
);