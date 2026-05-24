using FitnessClub.API.Data;
using FitnessClub.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AnalyticsController(AppDbContext db) => _db = db;

    private const decimal ParticipantBonusNormal = 100m;
    private const decimal ParticipantBonusHigh = 150m;

    // ═══════════════════════════════════════════════════
    //  GET /api/analytics  — головна (незмінна)
    // ═══════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> GetAnalytics()
    {
        var utcNow = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(utcNow);
        var todayDate = utcNow.Date;

        var months = Enumerable.Range(0, 6)
            .Select(i => utcNow.AddMonths(-5 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .ToList();

        var revenueByMonth = await _db.Payments
            .Where(p => p.PaymentDate >= months[0] && p.Amount > 0 && p.Status == "Completed")
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var refundsByMonth = await _db.Payments
            .Where(p => p.PaymentDate >= months[0] && p.Amount < 0)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var totalRevenue = await _db.Payments.Where(p => p.Amount > 0 && p.Status == "Completed").SumAsync(p => (decimal?)p.Amount) ?? 0;
        var totalRefunds = await _db.Payments.Where(p => p.Amount < 0).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var avgPayment = await _db.Payments.Where(p => p.Amount > 0 && p.Status == "Completed").AverageAsync(p => (decimal?)p.Amount) ?? 0;
        var thisMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);
        var monthRevenue = await _db.Payments.Where(p => p.PaymentDate >= thisMonth && p.Amount > 0 && p.Status == "Completed").SumAsync(p => (decimal?)p.Amount) ?? 0;
        var lastMonthRevenue = await _db.Payments.Where(p => p.PaymentDate >= lastMonth && p.PaymentDate < thisMonth && p.Amount > 0 && p.Status == "Completed").SumAsync(p => (decimal?)p.Amount) ?? 0;

        var clientsByMonth = await _db.Clients
    .Where(c => c.CreatedAt >= months[0])
    .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
    .ToListAsync();

        var newClientsThisMonth = await _db.Clients.CountAsync(c => c.CreatedAt >= thisMonth);
        var newClientsLastMonth = await _db.Clients.CountAsync(c => c.CreatedAt >= lastMonth && c.CreatedAt < thisMonth);
        var activeMembers = await _db.Memberships.CountAsync(m => m.Status == "Active" && m.EndDate >= today);
        var expiringSoon = await _db.Memberships.CountAsync(m => m.Status == "Active" && m.EndDate >= today && m.EndDate <= today.AddDays(7));

        var visitsByMonth = await _db.Visits
            .Where(v => v.CheckIn >= months[0])
            .GroupBy(v => new { v.CheckIn.Year, v.CheckIn.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var visitsByDow = await _db.Visits
            .GroupBy(v => v.CheckIn.DayOfWeek)
            .Select(g => new { Day = (int)g.Key, Count = g.Count() })
            .ToListAsync();

        var weekAgo = todayDate.AddDays(-7);
        var visitsThisWeek = await _db.Visits.CountAsync(v => v.CheckIn.Date >= weekAgo);

        var schedulesByDow = await _db.Schedules
            .GroupBy(s => s.StartDatetime.DayOfWeek)
            .Select(g => new { Day = (int)g.Key, Count = g.Count() })
            .ToListAsync();

        var membershipsByType = await _db.Memberships
            .Include(m => m.MembershipType)
            .GroupBy(m => m.MembershipType.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var topTrainers = await _db.Schedules
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .GroupBy(s => new { s.TrainerId, Name = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName })
            .Select(g => new { g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToListAsync();

        var topClasses = await _db.Schedules
            .Include(s => s.ClassType)
            .GroupBy(s => s.ClassType.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToListAsync();

        var paymentMethods = await _db.Payments
            .Where(p => p.Amount > 0 && p.Status == "Completed")
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new { Method = g.Key ?? "Unknown", Count = g.Count(), Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var trainersData = await _db.Trainers
            .Include(t => t.User)
            .Include(t => t.Schedules).ThenInclude(s => s.ClassType)
            .Include(t => t.Schedules).ThenInclude(s => s.Bookings)
            .ToListAsync();
        // Получаем всех клиентов тренеров
        var trainerClients = await _db.TrainerClients
            .Include(tc => tc.Client)        // подключаем информацию о клиенте
            .ToListAsync();

        // Получаем все платежи от клиентов
        var trainerClientPayments = await _db.TrainerClientPayments
            .Include(p => p.TrainerClient)   // подключаем связь с клиентом тренера
            .ToListAsync();

        var trainerPayroll = trainersData.Select(t =>
        {
            var completed = t.Schedules.Where(s => s.Status == "Completed" || s.Status == "Scheduled").ToList();
            var byWeek = completed
                .GroupBy(s => $"{s.StartDatetime.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(s.StartDatetime)}")
                .ToDictionary(g => g.Key, g => g.Count());

            decimal sessionPay = 0, bonusPay = 0;
            foreach (var s in completed)
            {
                var weekKey = $"{s.StartDatetime.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(s.StartDatetime)}";
                var hours = (decimal)(s.EndDatetime - s.StartDatetime).TotalHours;
                sessionPay += (t.HourlyRate ?? 0) * hours;

                var parts = s.Bookings.Count(b => b.Status != "Cancelled");
                bonusPay += (byWeek[weekKey] >= 7 ? ParticipantBonusHigh : ParticipantBonusNormal) * parts;
            }

            // ====== новые поля ======
            var clients = trainerClients.Where(tc => tc.TrainerId == t.Id).ToList();
            var clientPayments = trainerClientPayments.Where(p => clients.Any(c => c.Id == p.TrainerClientId)).ToList();
            var clientsEarned = clientPayments.Sum(p => p.Amount);
            var activeClientsCount = clients.Count;
            var clientPaymentsCount = clientPayments.Count;

            return new
            {
                TrainerId = t.Id,
                Name = t.User.FirstName + " " + t.User.LastName,
                HourlyRate = t.HourlyRate ?? 0,
                GroupRate = t.GroupRate ?? 0,
                IndividualRate = t.IndividualRate ?? 0,
                TotalSchedules = completed.Count,
                SessionPay = Math.Round(sessionPay, 0),
                Bonus = Math.Round(bonusPay, 0),
                ClientsEarned = Math.Round(clientsEarned, 0),
                ActiveClientsCount = activeClientsCount,
                ClientPaymentsCount = clientPaymentsCount,
                TotalPay = Math.Round(sessionPay + bonusPay + clientsEarned, 0),
                WeeksWithBonus = byWeek.Count(kv => kv.Value >= 7),
                TotalWeeks = byWeek.Count,
            };
        }).OrderByDescending(x => x.TotalPay).ToList();

        var totalPayroll = trainerPayroll.Sum(t => t.TotalPay);
        var totalBonus = trainerPayroll.Sum(t => t.Bonus);

        var monthLabels = months.Select(m => m.ToString("MMM yy")).ToList();
        var revenueArr = months.Select(m => { var x = revenueByMonth.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month); return x != null ? x.Total : 0; }).ToList();
        var refundsArr = months.Select(m => { var x = refundsByMonth.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month); return x != null ? x.Total : 0; }).ToList();
        var clientsArr = months.Select(m => { var x = clientsByMonth.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month); return x != null ? x.Count : 0; }).ToList();
        var visitsArr = months.Select(m => { var x = visitsByMonth.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month); return x != null ? x.Count : 0; }).ToList();
        var dowLabels = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
        var dowOrder = new[] { 1, 2, 3, 4, 5, 6, 0 };
        var visitsDowArr = dowOrder.Select(d => { var x = visitsByDow.FirstOrDefault(v => v.Day == d); return x != null ? x.Count : 0; }).ToList();
        var schedulesDowArr = dowOrder.Select(d => { var x = schedulesByDow.FirstOrDefault(v => v.Day == d); return x != null ? x.Count : 0; }).ToList();

        // ── додаткові дані для вкладок 2–8 ─────────────────────────────

        var allTransactions = await _db.Payments
            .Include(p => p.Client).ThenInclude(c => c.User)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new {
                Date = p.PaymentDate,
                ClientName = p.Client.User.FirstName + " " + p.Client.User.LastName,
                Description = p.Description ?? (p.Amount < 0 ? "Повернення" : "Оплата"),
                Method = p.PaymentMethod ?? "Unknown",
                Amount = p.Amount < 0 ? Math.Abs(p.Amount) : p.Amount,
                IsRefund = p.Amount < 0,
            })
            .ToListAsync();

        var visitLog = await _db.Visits
            .Include(v => v.Client).ThenInclude(c => c.User)
            .Select(v => new {
                Date = v.CheckIn,
                ClientId = v.ClientId.ToString(),
                Paid = 0m,
            })
            .ToListAsync();

        var clientFirstVisit = await _db.Visits
            .GroupBy(v => v.ClientId)
            .Select(g => new { ClientId = g.Key.ToString(), Date = g.Min(v => v.CheckIn) })
            .ToListAsync();

        var clients = await _db.Clients
            .Include(c => c.User)
            .Select(c => new {
                Id = c.Id.ToString(),
                Name = c.User.FirstName + " " + c.User.LastName,
                Phone = c.User.Phone ?? "",
            })
            .ToListAsync();

        var membershipsLog = await _db.Memberships
            .Include(m => m.MembershipType)
            .Include(m => m.Client).ThenInclude(c => c.User)
            .Select(m => new {
                Date = (DateTime?)m.StartDate.ToDateTime(TimeOnly.MinValue),
                ClientId = m.ClientId.ToString(),
                ClientName = m.Client.User.FirstName + " " + m.Client.User.LastName,
                TypeName = m.MembershipType.Name,
                Price = m.MembershipType.Price,
                Status = m.Status,
            })
            .ToListAsync();

        var membershipsByClient = membershipsLog
            .GroupBy(m => m.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToList();

        var scheduleLog = await _db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Bookings)
            .Select(s => new {
                Date = (DateTime?)s.StartDatetime,
                ClassName = s.ClassType.Name,
                TrainerName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ParticipantCount = s.Bookings.Count(b => b.Status != "Cancelled"),
                MaxCapacity = s.MaxCapacity,
                Status = s.Status,
            })
            .ToListAsync();

        var canceledMembershipsLog = await _db.Memberships
            .Include(m => m.MembershipType)
            .Include(m => m.Client).ThenInclude(c => c.User)
            .Where(m => m.Status == "Cancelled")
            .Select(m => new {
                Date = m.UpdatedAt,
                ClientName = m.Client.User.FirstName + " " + m.Client.User.LastName,
                TypeName = m.MembershipType.Name,
                Price = m.MembershipType.Price,
            })
            .ToListAsync();

        var canceledSchedulesLog = await _db.Bookings
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .Include(b => b.Client).ThenInclude(c => c.User)
            .Where(b => b.Status == "Cancelled")
            .Select(b => new {
                Date = b.CancelledAt ?? b.BookedAt,
                ClientName = b.Client.User.FirstName + " " + b.Client.User.LastName,
                ClassName = b.Schedule.ClassType.Name,
                RefundAmount = 0m,
            })
            .ToListAsync();

        return Ok(new
        {
            TotalRevenue = totalRevenue,
            TotalRefunds = Math.Abs(totalRefunds),
            NetRevenue = totalRevenue + totalRefunds,
            MonthRevenue = monthRevenue,
            LastMonthRevenue = lastMonthRevenue,
            MonthGrowthPercent = lastMonthRevenue > 0 ? Math.Round((monthRevenue - lastMonthRevenue) / lastMonthRevenue * 100, 1) : 0,
            AvgPayment = Math.Round(avgPayment, 0),
            ActiveMembers = activeMembers,
            ExpiringSoon = expiringSoon,
            NewClientsThisMonth = newClientsThisMonth,
            NewClientsLastMonth = newClientsLastMonth,
            VisitsThisWeek = visitsThisWeek,
            MonthLabels = monthLabels,
            RevenueByMonth = revenueArr,
            RefundsByMonth = refundsArr,
            ClientsByMonth = clientsArr,
            VisitsByMonth = visitsArr,
            DowLabels = dowLabels,
            VisitsByDow = visitsDowArr,
            SchedulesByDow = schedulesDowArr,
            MembershipsByType = membershipsByType,
            TopTrainers = topTrainers,
            TopClasses = topClasses,
            PaymentMethods = paymentMethods,
            TrainerPayroll = trainerPayroll,
            TotalPayroll = Math.Round(totalPayroll, 0),
            TotalBonus = Math.Round(totalBonus, 0),
            NetRevenueAfterPayroll = Math.Round(totalRevenue + totalRefunds - totalPayroll, 0),
            PerSessionRate = 0,
            WeeklyBonusRate = ParticipantBonusHigh,
            // ── дані для вкладок 2–8 ──
            Transactions = allTransactions,
            VisitLog = visitLog,
            ClientFirstVisit = clientFirstVisit,
            Clients = clients,
            Memberships = membershipsLog,
            MembershipsByClient = membershipsByClient,
            Schedules = scheduleLog,
            CanceledMemberships = canceledMembershipsLog,
            CanceledSchedules = canceledSchedulesLog,
        });
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/analytics/payments?from=&to=
    // ═══════════════════════════════════════════════════
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toEnd = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var payments = await _db.Payments
            .Include(p => p.Client).ThenInclude(c => c.User)
            .Include(p => p.Membership).ThenInclude(m => m!.MembershipType)
            .Where(p => p.PaymentDate >= fromUtc && p.PaymentDate < toEnd)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var revenue = payments.Where(p => p.Amount > 0).Sum(p => p.Amount);
        var refunds = payments.Where(p => p.Amount < 0).Sum(p => Math.Abs(p.Amount));
        var count = payments.Count;
        var avg = payments.Where(p => p.Amount > 0).Any()
                        ? payments.Where(p => p.Amount > 0).Average(p => p.Amount) : 0;

        var span = toEnd - fromUtc;
        var prevRev = await _db.Payments
            .Where(p => p.PaymentDate >= fromUtc - span && p.PaymentDate < fromUtc && p.Amount > 0 && p.Status == "Completed")
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var methodStats = payments
            .GroupBy(p => p.PaymentMethod ?? "Unknown")
            .Select(g => new { Method = g.Key, Count = g.Count(), Total = g.Sum(p => Math.Abs(p.Amount)) })
            .OrderByDescending(x => x.Count).ToList();

        var days = (toEnd - fromUtc).TotalDays;
        object dynamicData = days <= 35
            ? payments.Where(p => p.Amount > 0)
                .GroupBy(p => p.PaymentDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Label = g.Key.ToString("dd.MM"), Total = g.Sum(p => p.Amount) })
                .ToList()
            : (object)payments.Where(p => p.Amount > 0)
                .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new { Label = $"{g.Key.Month:D2}/{g.Key.Year % 100:D2}", Total = g.Sum(p => p.Amount) })
                .ToList();

        return Ok(new
        {
            Revenue = Math.Round(revenue, 0),
            Refunds = Math.Round(refunds, 0),
            Net = Math.Round(revenue - refunds, 0),
            AvgPayment = Math.Round(avg, 0),
            Count = count,
            PrevRevenue = Math.Round(prevRev, 0),
            GrowthPercent = prevRev > 0 ? Math.Round((revenue - prevRev) / prevRev * 100, 1) : 0m,
            Methods = methodStats,
            Dynamic = dynamicData,
            Transactions = payments.Take(300).Select(p => new
            {
                p.Id,
                Date = p.PaymentDate,
                ClientName = p.Client.User.FirstName + " " + p.Client.User.LastName,
                Description = p.Description ?? (p.Membership != null ? p.Membership.MembershipType.Name : "Платіж"),
                Method = p.PaymentMethod ?? "Unknown",
                Amount = p.Amount,
                IsRefund = p.Amount < 0,
                Status = p.Status ?? "Completed",
            }),
        });
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/analytics/clients?from=&to=
    // ═══════════════════════════════════════════════════
    [HttpGet("clients")]
    public async Task<IActionResult> GetClients([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toEnd = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var visits = await _db.Visits
            .Include(v => v.Client).ThenInclude(c => c.User)
            .Where(v => v.CheckIn >= fromUtc && v.CheckIn < toEnd)
            .ToListAsync();

        var newClientIds = await _db.Clients.Include(c => c.User)
            .Where(c => c.User.CreatedAt >= fromUtc && c.User.CreatedAt < toEnd)
            .Select(c => c.Id).ToListAsync();

        var uniqueIds = visits.Select(v => v.ClientId).Distinct().ToList();
        var uniqueCount = uniqueIds.Count;
        var newCount = uniqueIds.Count(id => newClientIds.Contains(id));
        var avgVisits = uniqueCount > 0 ? (double)visits.Count / uniqueCount : 0;

        var payDict = (await _db.Payments
            .Where(p => p.PaymentDate >= fromUtc && p.PaymentDate < toEnd && p.Amount > 0 && p.Status == "Completed")
            .GroupBy(p => p.ClientId)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync()).ToDictionary(x => x.ClientId, x => x.Total);

        var topClients = visits
            .GroupBy(v => v.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                Name = g.First().Client.User.FirstName + " " + g.First().Client.User.LastName,
                Phone = g.First().Client.User.Phone ?? "",
                VisitCount = g.Count(),
                LastVisit = g.Max(v => v.CheckIn),
                TotalPaid = payDict.TryGetValue(g.Key, out var p) ? p : 0m,
            })
            .OrderByDescending(c => c.VisitCount).Take(50).ToList();

        var dailyActivity = visits
            .GroupBy(v => v.CheckIn.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Label = g.Key.ToString("dd.MM"), Count = g.Count() })
            .ToList();

        // Всі часи
        var allVisits = await _db.Visits
            .Include(v => v.Client).ThenInclude(c => c.User)
            .ToListAsync();

        var allPayDict = (await _db.Payments
            .Where(p => p.Amount > 0 && p.Status == "Completed")
            .GroupBy(p => p.ClientId)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync()).ToDictionary(x => x.ClientId, x => x.Total);

        var memCountDict = (await _db.Memberships
            .GroupBy(m => m.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToListAsync()).ToDictionary(x => x.ClientId, x => x.Count);

        var allTimeTop = allVisits
            .GroupBy(v => v.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                Name = g.First().Client.User.FirstName + " " + g.First().Client.User.LastName,
                Phone = g.First().Client.User.Phone ?? "",
                VisitCount = g.Count(),
                FirstVisit = g.Min(v => v.CheckIn),
                TotalPaid = allPayDict.TryGetValue(g.Key, out var ap) ? ap : 0m,
                MembershipCount = memCountDict.TryGetValue(g.Key, out var mc) ? mc : 0,
            })
            .OrderByDescending(c => c.VisitCount).Take(50).ToList();

        return Ok(new
        {
            UniqueClients = uniqueCount,
            NewClients = newCount,
            ReturnClients = uniqueCount - newCount,
            AvgVisits = Math.Round(avgVisits, 1),
            DailyActivity = dailyActivity,
            TopClients = topClients,
            AllTimeTop = allTimeTop,
        });
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/analytics/schedules?from=&to=
    // ═══════════════════════════════════════════════════
    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toEnd = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var schedules = await _db.Schedules
            .Include(s => s.ClassType)
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Bookings)
            .Where(s => s.StartDatetime >= fromUtc && s.StartDatetime < toEnd)
            .ToListAsync();

        var byClass = schedules
            .GroupBy(s => new { s.ClassType.Name, TrainerName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName })
            .Select(g =>
            {
                var totalPart = g.Sum(s => s.Bookings.Count(b => b.Status != "Cancelled"));
                var totalCap = g.Sum(s => s.MaxCapacity);
                var cnt = g.Count();
                return new
                {
                    Name = g.Key.Name,
                    TrainerName = g.Key.TrainerName,
                    SessionCount = cnt,
                    TotalParticipants = totalPart,
                    AvgParticipants = cnt > 0 ? Math.Round((double)totalPart / cnt, 1) : 0,
                    FillRate = totalCap > 0 ? Math.Round(100.0 * totalPart / totalCap, 0) : 0.0,
                };
            })
            .OrderByDescending(x => x.TotalParticipants).ToList();

        var dowLabels = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
        var dowOrder = new[] { 1, 2, 3, 4, 5, 6, 0 };
        var avgByDow = dowOrder.Select(d =>
        {
            var grp = schedules.Where(s => (int)s.StartDatetime.DayOfWeek == d).ToList();
            return grp.Count > 0
                ? Math.Round(grp.Average(s => s.Bookings.Count(b => b.Status != "Cancelled")), 1) : 0.0;
        }).ToList();

        var allPart = schedules.Sum(s => s.Bookings.Count(b => b.Status != "Cancelled"));
        var allCap = schedules.Sum(s => s.MaxCapacity);
        var peakSes = schedules.OrderByDescending(s => s.Bookings.Count(b => b.Status != "Cancelled")).FirstOrDefault();

        var monthlyLoad = schedules
            .GroupBy(s => new { s.StartDatetime.Year, s.StartDatetime.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Label = $"{g.Key.Month:D2}/{g.Key.Year % 100:D2}",
                Avg = Math.Round(g.Average(s => s.Bookings.Count(b => b.Status != "Cancelled")), 1),
            }).ToList();

        return Ok(new
        {
            TotalSessions = schedules.Count,
            TotalParticipants = allPart,
            AvgPerSession = schedules.Count > 0 ? Math.Round((double)allPart / schedules.Count, 1) : 0,
            AvgFillRate = allCap > 0 ? Math.Round(100.0 * allPart / allCap, 0) : 0,
            PeakParticipants = peakSes != null ? peakSes.Bookings.Count(b => b.Status != "Cancelled") : 0,
            PeakDay = peakSes != null ? (DateTime?)peakSes.StartDatetime : null,
            DowLabels = dowLabels,
            AvgByDow = avgByDow,
            MonthlyLoad = monthlyLoad,
            ByClass = byClass,
        });
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/analytics/memberships?from=&to=
    // ═══════════════════════════════════════════════════
    [HttpGet("memberships")]
    public async Task<IActionResult> GetMemberships([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var fromDate = DateOnly.FromDateTime(from.Date);
        var toDate = DateOnly.FromDateTime(to.Date);
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toEnd = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var sold = await _db.Memberships
            .Include(m => m.MembershipType)
            .Include(m => m.Client).ThenInclude(c => c.User)
            .Where(m => m.StartDate >= fromDate && m.StartDate <= toDate)
            .ToListAsync();

        var byType = sold
            .GroupBy(m => new { m.MembershipType.Name, Price = m.MembershipType.Price })
            .Select(g => new
            {
                Name = g.Key.Name,
                Price = g.Key.Price,
                SoldCount = g.Count(),
                Revenue = g.Count() * g.Key.Price,
            })
            .OrderByDescending(x => x.SoldCount).ToList();

        var totalSoldRevenue = byType.Sum(x => x.Revenue);

        var cancelled = await _db.Memberships
            .Include(m => m.MembershipType)
            .Include(m => m.Client).ThenInclude(c => c.User)
            .Where(m => m.Status == "Cancelled" && m.UpdatedAt >= fromUtc && m.UpdatedAt < toEnd)
            .Select(m => new
            {
                Date = m.UpdatedAt,
                ClientName = m.Client.User.FirstName + " " + m.Client.User.LastName,
                TypeName = m.MembershipType.Name,
                Price = m.MembershipType.Price,
            })
            .ToListAsync();

        var cancelledBookings = await _db.Bookings
            .Include(b => b.Schedule).ThenInclude(s => s.ClassType)
            .Include(b => b.Client).ThenInclude(c => c.User)
            .Where(b => b.Status == "Cancelled" && b.CancelledAt >= fromUtc && b.CancelledAt < toEnd)
            .Select(b => new
            {
                Date = b.CancelledAt,
                ClientName = b.Client.User.FirstName + " " + b.Client.User.LastName,
                ClassName = b.Schedule.ClassType.Name,
            })
            .ToListAsync();

        var cancelTrend = cancelled
            .GroupBy(c => new { c.Date.Year, c.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new { Label = $"{g.Key.Month:D2}/{g.Key.Year % 100:D2}", Count = g.Count() })
            .ToList();

        return Ok(new
        {
            TotalSold = sold.Count,
            TotalSoldRevenue = Math.Round(totalSoldRevenue, 0),
            ByType = byType.Select(t => new
            {
                t.Name,
                t.SoldCount,
                t.Price,
                Revenue = Math.Round(t.Revenue, 0),
                Share = totalSoldRevenue > 0 ? Math.Round(100m * t.Revenue / totalSoldRevenue, 1) : 0m,
            }),
            CancelledMemberships = cancelled.Count,
            CancelledSessions = cancelledBookings.Count,
            LostRevenue = Math.Round(cancelled.Sum(c => c.Price), 0),
            CancelRate = sold.Count > 0 ? Math.Round(100.0 * cancelled.Count / sold.Count, 1) : 0,
            CancelTrend = cancelTrend,
            CancelledList = cancelled.OrderByDescending(c => c.Date).Take(100),
            CancelledBookingsList = cancelledBookings.OrderByDescending(b => b.Date).Take(100),
        });
    }
}