using FitnessClub.API.Data;
using FitnessClub.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/qr")]
public class QrController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<QrController> _logger;

    public QrController(AppDbContext db, ILogger<QrController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    //  POST /api/qr/scan?token=<GUID>
    //  або
    //  GET  /api/qr/scan?token=<GUID>   (для простого термінала/браузера)
    //
    //  Викликається терміналом на рецепції при скануванні QR.
    //  Повертає JSON з результатом + HTML-сторінку для відображення.
    // ══════════════════════════════════════════════════════════
    [HttpGet("scan")]
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(
        [FromQuery] string token,
        CancellationToken ct)
    {
        _logger.LogInformation("🔍 Спроба входу, токен: {Token}", token);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("❌ Порожній токен");
            return BadRequest(ScanResult.Fail("Токен не вказано"));
        }

        var now = DateTime.UtcNow;

        // ── Шукаємо токен у БД ────────────────────────────────
        var qrToken = await _db.QrTokens
            .Include(q => q.Client)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(q => q.Token == token, ct);

        if (qrToken == null)
        {
            _logger.LogWarning("❌ Токен не знайдено: {Token}", token);
            return Ok(ScanResult.Fail("❌ QR-код недійсний або не існує"));
        }

        // ── Перевірка терміну дії ─────────────────────────────
        if (qrToken.ExpiresAt < now)
        {
            _logger.LogWarning(
                "⏰ Токен прострочено для ClientId={ClientId}, закінчився {Expiry}",
                qrToken.ClientId, qrToken.ExpiresAt);

            return Ok(ScanResult.Fail(
                "⏰ QR-код прострочено. Попросіть клієнта надіслати /qr в боті для оновлення."));
        }

        // ── Перевірка чи вже використаний ────────────────────
        if (qrToken.IsUsed)
        {
            _logger.LogWarning(
                "🔁 Токен вже використано для ClientId={ClientId} о {UsedAt}",
                qrToken.ClientId, qrToken.UsedAt);

            return Ok(ScanResult.Fail(
                $"🔁 Цей QR вже використано сьогодні о {qrToken.UsedAt?.ToLocalTime():HH:mm}. " +
                $"Наступний вхід доступний після {qrToken.ExpiresAt.ToLocalTime():HH:mm dd.MM}."));
        }

        // ── Перевірка активного абонементу ───────────────────
        var today = DateOnly.FromDateTime(DateTime.Today);

        var activeMembership = await _db.Memberships
            .Include(m => m.MembershipType)
            .Where(m => m.ClientId == qrToken.ClientId
                     && m.Status == "Active"
                     && m.EndDate >= today)
            .OrderByDescending(m => m.EndDate)
            .FirstOrDefaultAsync(ct);

        if (activeMembership == null)
        {
            _logger.LogWarning(
                "❌ Немає активного абонементу для ClientId={ClientId}",
                qrToken.ClientId);

            return Ok(ScanResult.Fail(
                "❌ Активного абонементу не знайдено. Зверніться на рецепцію."));
        }

        // ── Все ОК — записуємо візит ──────────────────────────
        var visit = new Visit
        {
            ClientId = qrToken.ClientId,
            CheckIn = now,
            Notes = $"QR вхід, токен: {token[..8]}..."
        };

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync(ct); // Зберігаємо щоб отримати visit.Id

        // Позначаємо токен як використаний
        qrToken.IsUsed = true;
        qrToken.UsedAt = now;
        qrToken.VisitId = visit.Id;

        await _db.SaveChangesAsync(ct);

        var client = qrToken.Client;
        var user = client.User;
        var membershipType = activeMembership.MembershipType?.Name ?? "Абонемент";
        var daysLeft = activeMembership.EndDate.DayNumber - today.DayNumber;

        _logger.LogInformation(
            "✅ ВХІД: {FirstName} {LastName} (ClientId={ClientId}), " +
            "Абонемент: {Type}, VisitId={VisitId}",
            user.FirstName, user.LastName,
            qrToken.ClientId, membershipType, visit.Id);

        return Ok(ScanResult.Success(
            clientName: $"{user.FirstName} {user.LastName}",
            membershipType: membershipType,
            daysLeft: daysLeft,
            visitId: visit.Id,
            checkIn: now.ToLocalTime()));
    }

    // ══════════════════════════════════════════════════════════
    //  GET /api/qr/status/{clientId}
    //  Перевірити статус токену клієнта (для адмін-панелі)
    // ══════════════════════════════════════════════════════════
    [HttpGet("status/{clientId:int}")]
    public async Task<IActionResult> GetStatus(int clientId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var token = await _db.QrTokens
            .Where(q => q.ClientId == clientId && q.ExpiresAt > now)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (token == null)
            return Ok(new { hasActiveToken = false });

        return Ok(new
        {
            hasActiveToken = true,
            isUsed = token.IsUsed,
            expiresAt = token.ExpiresAt.ToLocalTime(),
            usedAt = token.UsedAt?.ToLocalTime()
        });
    }

    // ══════════════════════════════════════════════════════════
    //  DELETE /api/qr/invalidate/{clientId}
    //  Примусово анулювати всі токени клієнта (адмін)
    // ══════════════════════════════════════════════════════════
    [HttpDelete("invalidate/{clientId:int}")]
    public async Task<IActionResult> Invalidate(int clientId, CancellationToken ct)
    {
        var tokens = await _db.QrTokens
            .Where(q => q.ClientId == clientId && !q.IsUsed)
            .ToListAsync(ct);

        if (!tokens.Any())
            return Ok(new { message = "Активних токенів не знайдено" });

        _db.QrTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "🗑️ Анульовано {Count} токенів для ClientId={ClientId}",
            tokens.Count, clientId);

        return Ok(new { message = $"Анульовано {tokens.Count} токенів" });
    }
}

// ══════════════════════════════════════════════════════════════
//  DTO відповіді сканування
// ══════════════════════════════════════════════════════════════
public class ScanResult
{
    public bool Allowed { get; set; }
    public string Message { get; set; } = "";
    public string? ClientName { get; set; }
    public string? MembershipType { get; set; }
    public int? DaysLeft { get; set; }
    public int? VisitId { get; set; }
    public DateTime? CheckIn { get; set; }

    public static ScanResult Success(
        string clientName,
        string membershipType,
        int daysLeft,
        int visitId,
        DateTime checkIn) => new()
        {
            Allowed = true,
            Message = "✅ Вхід дозволено",
            ClientName = clientName,
            MembershipType = membershipType,
            DaysLeft = daysLeft,
            VisitId = visitId,
            CheckIn = checkIn
        };

    public static ScanResult Fail(string reason) => new()
    {
        Allowed = false,
        Message = reason
    };
}