using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;
using FitnessClub.API.Models;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/trainer-clients")]
[Authorize]
public class TrainerClientsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TrainerClientsController(AppDbContext db) => _db = db;

    // ═══════════════════════════════════════════════════
    //  GET /api/trainer-clients?trainerId=X
    //  Список клієнтів тренера (активні прив'язки)
    // ═══════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> GetByTrainer([FromQuery] int trainerId)
    {
        var links = await _db.TrainerClients
            .Include(tc => tc.Client).ThenInclude(c => c.User)
            .Include(tc => tc.Payments)
            .Where(tc => tc.TrainerId == trainerId)
            .OrderByDescending(tc => tc.IsActive)
            .ThenByDescending(tc => tc.StartDate)
            .ToListAsync();

        var result = links.Select(tc =>
        {
            var totalPaid = tc.Payments.Sum(p => p.Amount);
            var lastPayment = tc.Payments.OrderByDescending(p => p.PaidAt).FirstOrDefault();
            var paymentCount = tc.Payments.Count;

            // Розрахунок "наступного платежу"
            DateTime? nextPayment = tc.PaymentType switch
            {
                "weekly" => lastPayment != null
                    ? lastPayment.PaidAt.AddDays(7)
                    : tc.StartDate.AddDays(7),
                "monthly" => lastPayment != null
                    ? new DateTime(lastPayment.PaidAt.Year, lastPayment.PaidAt.Month, 1).AddMonths(1)
                    : new DateTime(tc.StartDate.Year, tc.StartDate.Month, 1).AddMonths(1),
                _ => null  // разова — немає наступного
            };

            var isOverdue = nextPayment.HasValue && nextPayment.Value.Date < DateTime.UtcNow.Date;

            return new
            {
                tc.Id,
                tc.TrainerId,
                ClientId = tc.ClientId,
                ClientName = tc.Client.User.FirstName + " " + tc.Client.User.LastName,
                ClientPhone = tc.Client.User.Phone ?? "",
                ClientEmail = tc.Client.User.Email,
                tc.PaymentType,
                tc.Rate,
                tc.StartDate,
                tc.EndDate,
                tc.IsActive,
                tc.Notes,
                TotalPaid = totalPaid,
                PaymentCount = paymentCount,
                LastPaymentDate = lastPayment?.PaidAt,
                LastPaymentAmount = lastPayment?.Amount,
                NextPaymentDate = nextPayment,
                IsOverdue = isOverdue,
                Initials = GetInitials(tc.Client.User.FirstName, tc.Client.User.LastName),
            };
        });

        return Ok(result);
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/trainer-clients/{id}/payments
    //  Платежі по конкретній прив'язці
    // ═══════════════════════════════════════════════════
    [HttpGet("{id}/payments")]
    public async Task<IActionResult> GetPayments(int id)
    {
        var tc = await _db.TrainerClients
            .Include(x => x.Client).ThenInclude(c => c.User)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (tc == null) return NotFound();

        var payments = tc.Payments
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new
            {
                p.Id,
                p.PaidAt,
                p.Amount,
                p.PaymentMethod,
                p.Note,
            });

        return Ok(new
        {
            TrainerClientId = tc.Id,
            ClientName = tc.Client.User.FirstName + " " + tc.Client.User.LastName,
            tc.PaymentType,
            tc.Rate,
            TotalPaid = tc.Payments.Sum(p => p.Amount),
            Payments = payments,
        });
    }

    // ═══════════════════════════════════════════════════
    //  POST /api/trainer-clients
    //  Прив'язати клієнта до тренера
    // ═══════════════════════════════════════════════════
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTrainerClientDto dto)
    {
        // Перевірка: чи існує вже активна прив'язка цього клієнта до цього тренера
        var existing = await _db.TrainerClients
            .FirstOrDefaultAsync(tc => tc.TrainerId == dto.TrainerId
                                    && tc.ClientId == dto.ClientId
                                    && tc.IsActive);
        if (existing != null)
            return BadRequest(new { error = "Клієнт вже прив'язаний до цього тренера" });

        var trainer = await _db.Trainers.FindAsync(dto.TrainerId);
        if (trainer == null) return NotFound(new { error = "Тренер не знайдений" });

        var client = await _db.Clients.FindAsync(dto.ClientId);
        if (client == null) return NotFound(new { error = "Клієнт не знайдений" });

        var tc = new TrainerClient
        {
            TrainerId = dto.TrainerId,
            ClientId = dto.ClientId,
            PaymentType = dto.PaymentType,
            Rate = dto.Rate,
            StartDate = DateTime.UtcNow,
            EndDate = dto.EndDate,
            Notes = dto.Notes,
            IsActive = true,
        };

        _db.TrainerClients.Add(tc);
        await _db.SaveChangesAsync();

        // Якщо разовий платіж і одразу оплачено
        if (dto.PaymentType == "single" && dto.PaidNow)
        {
            await AddPaymentInternal(tc.Id, dto.Rate, dto.PaymentMethod ?? "Cash",
                $"Разова оплата тренування", DateTime.UtcNow);
        }

        return Ok(new { id = tc.Id });
    }

    // ═══════════════════════════════════════════════════
    //  POST /api/trainer-clients/{id}/pay
    //  Додати платіж по прив'язці
    // ═══════════════════════════════════════════════════
    [HttpPost("{id}/pay")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] AddPaymentDto dto)
    {
        var tc = await _db.TrainerClients
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (tc == null) return NotFound();

        var note = dto.Note ?? tc.PaymentType switch
        {
            "weekly" => $"Тижнева оплата ({dto.PaidAt:dd.MM.yyyy})",
            "monthly" => $"Місячна оплата ({dto.PaidAt:MM.yyyy})",
            _ => "Оплата тренування"
        };

        var payment = await AddPaymentInternal(id, dto.Amount > 0 ? dto.Amount : tc.Rate,
            dto.PaymentMethod, note, dto.PaidAt);

        // Також вносимо в загальну таблицю платежів для аналітики
        var analyticsPayment = new Payment
        {
            ClientId = tc.ClientId,
            Amount = payment.Amount,
            PaymentDate = payment.PaidAt,
            PaymentMethod = payment.PaymentMethod,
            Status = "Completed",
            Description = $"Тренер: {note}",
            CreatedBy = null,
        };
        _db.Payments.Add(analyticsPayment);
        await _db.SaveChangesAsync();

        return Ok(new { paymentId = payment.Id, amount = payment.Amount });
    }

    // ═══════════════════════════════════════════════════
    //  PUT /api/trainer-clients/{id}
    //  Оновити умови прив'язки
    // ═══════════════════════════════════════════════════
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTrainerClientDto dto)
    {
        var tc = await _db.TrainerClients.FindAsync(id);
        if (tc == null) return NotFound();

        tc.PaymentType = dto.PaymentType;
        tc.Rate = dto.Rate;
        tc.EndDate = dto.EndDate;
        tc.Notes = dto.Notes;
        tc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════════════════════════
    //  DELETE /api/trainer-clients/{id}
    //  Деактивувати прив'язку (не видаляємо, зберігаємо історію)
    // ═══════════════════════════════════════════════════
    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var tc = await _db.TrainerClients.FindAsync(id);
        if (tc == null) return NotFound();

        tc.IsActive = false;
        tc.EndDate = DateTime.UtcNow;
        tc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/trainer-clients/all-clients
    //  Всі клієнти (для вибору при прив'язці)
    // ═══════════════════════════════════════════════════
    [HttpGet("all-clients")]
    public async Task<IActionResult> GetAllClients()
    {
        var clients = await _db.Clients
            .Include(c => c.User)
            .Where(c => c.User.IsActive)
            .OrderBy(c => c.User.LastName)
            .Select(c => new
            {
                c.Id,
                Name = c.User.FirstName + " " + c.User.LastName,
                Phone = c.User.Phone ?? "",
                c.User.Email,
            })
            .ToListAsync();
        return Ok(clients);
    }

    // ═══════════════════════════════════════════════════
    //  GET /api/trainer-clients/summary?trainerId=X&from=&to=
    //  Підсумок для аналітики (скільки тренер заробив на клієнтах)
    // ═══════════════════════════════════════════════════
    [HttpGet("summary")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetSummary([FromQuery] int trainerId,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var payments = await _db.TrainerClientPayments
            .Include(p => p.TrainerClient)
                .ThenInclude(tc => tc.Client).ThenInclude(c => c.User)
            .Where(p => p.TrainerClient.TrainerId == trainerId
                     && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .ToListAsync();

        return Ok(new
        {
            TotalEarned = payments.Sum(p => p.Amount),
            PaymentCount = payments.Count,
            ByType = payments
                .GroupBy(p => p.TrainerClient.PaymentType)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(p => p.Amount),
                }),
            Payments = payments.Select(p => new
            {
                p.Id,
                p.PaidAt,
                p.Amount,
                p.PaymentMethod,
                p.Note,
                ClientName = p.TrainerClient.Client.User.FirstName + " "
                           + p.TrainerClient.Client.User.LastName,
                p.TrainerClient.PaymentType,
            }),
        });
    }

    // ── HELPER ────────────────────────────────────────
    private async Task<TrainerClientPayment> AddPaymentInternal(
        int trainerClientId, decimal amount, string method, string note, DateTime paidAt)
    {
        var payment = new TrainerClientPayment
        {
            TrainerClientId = trainerClientId,
            Amount = amount,
            PaymentMethod = method,
            Note = note,
            PaidAt = DateTime.SpecifyKind(paidAt, DateTimeKind.Utc),
        };
        _db.TrainerClientPayments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }

    private static string GetInitials(string first, string last)
    {
        var f = first.Length > 0 ? first[0].ToString() : "";
        var l = last.Length > 0 ? last[0].ToString() : "";
        return (f + l).ToUpper();
    }
}

// ════ DTOs ═══════════════════════════════════════════

public record CreateTrainerClientDto(
    int TrainerId,
    int ClientId,
    string PaymentType,   // single | weekly | monthly
    decimal Rate,
    DateTime? EndDate,
    string? Notes,
    bool PaidNow,
    string? PaymentMethod
);

public record UpdateTrainerClientDto(
    string PaymentType,
    decimal Rate,
    DateTime? EndDate,
    string? Notes
);

public record AddPaymentDto(
    decimal Amount,
    string PaymentMethod,
    string? Note,
    DateTime PaidAt
);