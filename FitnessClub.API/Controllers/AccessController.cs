using FitnessClub.API.Data;
using FitnessClub.API.Hubs;
using FitnessClub.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<AccessHub> _hub;

    public AccessController(AppDbContext db, IHubContext<AccessHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // POST api/access/scan
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest request)
    {
        var result = await ProcessQrCode(request.QrCode);
        await _hub.Clients.Group("terminals").SendAsync("ScanResult", result);
        return Ok(result);
    }

    // GET api/access/today
    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var todayUtc = DateTime.UtcNow.Date;

        var visits = await _db.Visits
            .Include(v => v.Client)
                .ThenInclude(c => c.User)
            .Where(v => v.CheckIn >= todayUtc && v.CheckIn < todayUtc.AddDays(1))
            .OrderByDescending(v => v.CheckIn)
            .ToListAsync();

        return Ok(new TodayDto
        {
            Count = visits.Select(v => v.ClientId).Distinct().Count(),
            Visits = visits.Select(v => new VisitEntryDto
            {
                ClientName = $"{v.Client.User.FirstName} {v.Client.User.LastName}",
                Time = v.CheckIn,
                EntryMethod = "qr"
            }).ToList()
        });
    }

    // POST api/access/scan-by-phone
    [HttpPost("scan-by-phone")]
    public async Task<IActionResult> ScanByPhone([FromBody] PhoneScanRequest request)
    {
        var result = await ProcessByPhone(request.Phone);
        await _hub.Clients.Group("terminals").SendAsync("ScanResult", result);
        return Ok(result);
    }

    // GET api/access/stats
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var weekUtc = todayUtc.AddDays(-7);
        var monthUtc = todayUtc.AddDays(-30);

        var todayVisits = await _db.Visits
            .Include(v => v.Client)
                .ThenInclude(c => c.User)
            .Where(v => v.CheckIn >= todayUtc && v.CheckIn < todayUtc.AddDays(1))
            .OrderByDescending(v => v.CheckIn)
            .ToListAsync();

        var weekCount = await _db.Visits.CountAsync(v => v.CheckIn >= weekUtc);
        var monthCount = await _db.Visits.CountAsync(v => v.CheckIn >= monthUtc);
        var nowInClub = await _db.Visits
            .CountAsync(v => v.CheckIn >= todayUtc && v.CheckOut == null);

        var clientIds = todayVisits.Select(v => v.ClientId).Distinct().ToList();
        var membershipByClient = new Dictionary<int, string>();

        if (clientIds.Any())
        {
            var membershipTypes = await _db.Memberships
                .Where(m => clientIds.Contains(m.ClientId) && m.Status == "Active")
                .ToListAsync();

            var typeIds = membershipTypes.Select(m => m.MembershipTypeId).Distinct().ToList();
            var types = await _db.MembershipTypes
                .Where(t => typeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name);

            membershipByClient = membershipTypes
                .GroupBy(m => m.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var best = g.OrderByDescending(m => m.EndDate).First();
                        return types.GetValueOrDefault(best.MembershipTypeId, "");
                    });
        }

        var hourlyRaw = todayVisits
            .GroupBy(v => v.CheckIn.ToLocalTime().Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        var hourlyBreakdown = Enumerable.Range(0, 24)
            .Select(h => new HourlyCountDto
            {
                Hour = h,
                Count = hourlyRaw.GetValueOrDefault(h, 0)
            })
            .ToList();

        var recentVisits = todayVisits
            .Take(20)
            .Select(v => new RecentVisitDto
            {
                ClientName = $"{v.Client.User.FirstName} {v.Client.User.LastName}",
                MembershipType = membershipByClient.GetValueOrDefault(v.ClientId, ""),
                Time = v.CheckIn
            })
            .ToList();

        return Ok(new AccessStatsDto
        {
            TodayCount = todayVisits.Select(v => v.ClientId).Distinct().Count(),
            WeekCount = weekCount,
            MonthCount = monthCount,
            NowInClub = nowInClub,
            RecentVisits = recentVisits,
            HourlyBreakdown = hourlyBreakdown
        });
    }

    // ── Логіка перевірки QR (ВИПРАВЛЕНО) ─────────────────────────────
    private async Task<ScanResultDto> ProcessQrCode(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
            return Deny("", "QR-код порожній");

        var code = qrCode.Trim();
        int? clientId = null;

        // 1. Спочатку шукаємо по токену в таблиці QrTokens
        var qrToken = await _db.QrTokens
            .FirstOrDefaultAsync(t => t.Token == code);

        if (qrToken != null)
        {
            // Перевіряємо термін дії
            if (qrToken.ExpiresAt <= DateTime.UtcNow)
                return Deny("", "QR-код прострочений. Отримайте новий в Telegram боті.");

            // Перевіряємо чи вже використаний
            if (qrToken.IsUsed)
                return Deny("", "QR-код вже використаний сьогодні. Отримайте новий завтра.");

            clientId = qrToken.ClientId;

            // Позначаємо як використаний
            qrToken.IsUsed = true;
            await _db.SaveChangesAsync();
        }
        // 2. Fallback на старий формат GYMCLUB-{id}-{checksum}
        else if (TryParseQr(code, out var parsedClientId))
        {
            clientId = parsedClientId;
        }

        // Якщо жоден формат не підійшов
        if (clientId == null)
            return Deny("", "Недійсний формат QR-коду");

        // Шукаємо клієнта
        var client = await _db.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == clientId);

        if (client == null)
            return Deny("", "Клієнта не знайдено в системі");

        var name = $"{client.User.FirstName} {client.User.LastName}";

        // Перевіряємо активний абонемент
        var (membershipTypeName, endDate) = await GetActiveMembership(clientId.Value);

        if (membershipTypeName == null)
            return Deny(name, "Абонемент відсутній або закінчився");

        // Перевіряємо чи вже був сьогодні
        var todayUtc = DateTime.UtcNow.Date;
        var alreadyToday = await _db.Visits.AnyAsync(v =>
            v.ClientId == clientId &&
            v.CheckIn >= todayUtc &&
            v.CheckIn < todayUtc.AddDays(1));

        // Записуємо візит
        _db.Visits.Add(new Visit { ClientId = clientId.Value, CheckIn = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var daysLeft = (endDate!.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;

        return new ScanResultDto
        {
            Allowed = true,
            ClientName = name,
            MembershipType = membershipTypeName,
            MembershipExpiry = endDate.Value.ToDateTime(TimeOnly.MinValue),
            DaysLeft = daysLeft,
            AlreadyVisitedToday = alreadyToday,
            Message = alreadyToday ? "Повторний вхід сьогодні" : "Ласкаво просимо!"
        };
    }

    // ── Логіка перевірки по телефону ─────────────────────────────────
    private async Task<ScanResultDto> ProcessByPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Deny("", "Номер телефону не вказано");

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var last9 = digits.Length >= 9 ? digits[^9..] : digits;

        var allUsers = await _db.Users
            .Where(u => u.IsActive && u.Phone != null)
            .ToListAsync();

        var user = allUsers.FirstOrDefault(u =>
        {
            var d = new string(u.Phone!.Where(char.IsDigit).ToArray());
            return d.EndsWith(last9);
        });

        if (user == null)
            return Deny("", "Клієнта з таким номером телефону не знайдено");

        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (client == null)
            return Deny(user.FullName, "Клієнта не знайдено в системі");

        var (membershipTypeName, endDate) = await GetActiveMembership(client.Id);

        if (membershipTypeName == null)
            return Deny(user.FullName, "Абонемент відсутній або закінчився");

        var todayUtc = DateTime.UtcNow.Date;
        var alreadyToday = await _db.Visits.AnyAsync(v =>
            v.ClientId == client.Id &&
            v.CheckIn >= todayUtc &&
            v.CheckIn < todayUtc.AddDays(1));

        _db.Visits.Add(new Visit { ClientId = client.Id, CheckIn = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var daysLeft = (endDate!.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;

        return new ScanResultDto
        {
            Allowed = true,
            ClientName = user.FullName,
            MembershipType = membershipTypeName,
            MembershipExpiry = endDate.Value.ToDateTime(TimeOnly.MinValue),
            DaysLeft = daysLeft,
            AlreadyVisitedToday = alreadyToday,
            Message = alreadyToday ? "Повторний вхід сьогодні" : "Ласкаво просимо!"
        };
    }

    // ── Отримати активний абонемент ──────────────────────────────────
    private async Task<(string? TypeName, DateOnly? EndDate)> GetActiveMembership(int clientId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var membership = await _db.Memberships
            .Where(m => m.ClientId == clientId
                     && m.Status == "Active"
                     && m.EndDate >= today)
            .OrderByDescending(m => m.EndDate)
            .FirstOrDefaultAsync();

        if (membership == null)
            return (null, null);

        var membershipType = await _db.MembershipTypes
            .FindAsync(membership.MembershipTypeId);

        return (membershipType?.Name ?? "Абонемент", membership.EndDate);
    }

    // ── Парсинг старого формату GYMCLUB-{id}-{checksum} ──────────────
    private static bool TryParseQr(string qrCode, out int clientId)
    {
        clientId = 0;
        if (string.IsNullOrWhiteSpace(qrCode)) return false;
        var parts = qrCode.Trim().Split('-');
        if (parts.Length != 3 || parts[0] != "GYMCLUB") return false;
        if (!int.TryParse(parts[1], out clientId)) return false;
        var expected = (clientId * 7 + 42) % 1000;
        if (!int.TryParse(parts[2], out var checksum)) return false;
        return checksum == expected;
    }

    private static ScanResultDto Deny(string clientName, string message) => new()
    {
        Allowed = false,
        ClientName = clientName,
        Message = message
    };
}

// ── DTOs ─────────────────────────────────────────────────────────────
public record ScanRequest(string QrCode);
public record PhoneScanRequest(string Phone);

public class ScanResultDto
{
    public bool Allowed { get; set; }
    public string ClientName { get; set; } = "";
    public string MembershipType { get; set; } = "";
    public DateTime? MembershipExpiry { get; set; }
    public int DaysLeft { get; set; }
    public bool AlreadyVisitedToday { get; set; }
    public string Message { get; set; } = "";
}

public class TodayDto
{
    public int Count { get; set; }
    public List<VisitEntryDto> Visits { get; set; } = new();
}

public class VisitEntryDto
{
    public string ClientName { get; set; } = "";
    public DateTime Time { get; set; }
    public string EntryMethod { get; set; } = "";
}

public class AccessStatsDto
{
    public int TodayCount { get; set; }
    public int WeekCount { get; set; }
    public int MonthCount { get; set; }
    public int NowInClub { get; set; }
    public List<RecentVisitDto> RecentVisits { get; set; } = new();
    public List<HourlyCountDto> HourlyBreakdown { get; set; } = new();
}

public class RecentVisitDto
{
    public string ClientName { get; set; } = "";
    public string MembershipType { get; set; } = "";
    public DateTime Time { get; set; }
}

public class HourlyCountDto
{
    public int Hour { get; set; }
    public int Count { get; set; }
}