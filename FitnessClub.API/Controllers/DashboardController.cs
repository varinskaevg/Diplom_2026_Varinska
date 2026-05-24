using FitnessClub.API.Data;
using FitnessClub.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        // ✅ ВАЖЛИВО — тільки UTC
        var utcNow = DateTime.UtcNow;

        var today = DateOnly.FromDateTime(utcNow);
        var todayDate = utcNow.Date;

        var thisMonth = new DateTime(
            utcNow.Year,
            utcNow.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc); // ⭐ КЛЮЧ

        var stats = new DashboardStatsDto
        {
            TotalClients = await _db.Clients.CountAsync(),

            ActiveMembers = await _db.Memberships
                .CountAsync(m => m.Status == "Active" && m.EndDate >= today),

            ExpiringSoon = await _db.Memberships
                .CountAsync(m =>
                    m.Status == "Active" &&
                    m.EndDate >= today &&
                    m.EndDate <= today.AddDays(7)),

            MonthRevenue = await _db.Payments
                .Where(p =>
                    p.PaymentDate >= thisMonth &&
                    p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0,

            TodayVisits = await _db.Visits
                .CountAsync(v => v.CheckIn.Date == todayDate),

            TotalTrainers = await _db.Trainers.CountAsync(),

            TodaySchedules = await _db.Schedules
                .CountAsync(s =>
                    s.StartDatetime.Date == todayDate &&
                    s.Status == "Scheduled"),

            RecentPayments = await _db.Payments
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .OrderByDescending(p => p.PaymentDate)
                .Take(5)
                .Select(p => new RecentPaymentDto
                {
                    Id = p.Id,
                    ClientName = p.Client.User.FullName,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    PaymentDate = p.PaymentDate
                })
                .ToListAsync()
        };

        return Ok(stats);
    }
}