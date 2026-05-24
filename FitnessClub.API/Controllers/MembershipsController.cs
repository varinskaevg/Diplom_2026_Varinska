using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;
using FitnessClub.API.Models;
using FitnessClub.API.Services;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class MembershipsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITelegramService _telegram;

    public MembershipsController(AppDbContext db, ITelegramService telegram)
    {
        _db = db;
        _telegram = telegram;
    }

    // GET api/memberships/my
    [HttpGet("my")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetMy()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var client = await _db.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.User.Email == email);

        if (client == null) return NotFound();

        var memberships = await _db.Memberships
            .Include(m => m.MembershipType)
            .Where(m => m.ClientId == client.Id)
            .OrderByDescending(m => m.StartDate)
            .Select(m => new
            {
                m.Id,
                TypeName = m.MembershipType.Name,
                m.Status,
                m.StartDate,
                m.EndDate,
                m.VisitsUsed,
                IncludesClasses = m.MembershipType.IncludesClasses
            })
            .ToListAsync();

        return Ok(memberships);
    }

    // GET api/memberships
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Client")]
    public async Task<IActionResult> GetAll([FromQuery] int? clientId)
    {
        if (User.IsInRole("Client"))
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? User.FindFirst("email")?.Value;

            var client = await _db.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.User.Email == email);

            if (client == null) return Forbid();
            clientId = client.Id;
        }

        var query = _db.Memberships
            .Include(m => m.Client).ThenInclude(c => c.User)
            .Include(m => m.MembershipType)
            .AsQueryable();

        if (clientId.HasValue)
            query = query.Where(m => m.ClientId == clientId.Value);

        var memberships = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.ClientId,
                ClientName = m.Client.User.FirstName + " " + m.Client.User.LastName,
                Initials = m.Client.User.FirstName.Substring(0, 1) +
                                  m.Client.User.LastName.Substring(0, 1),
                TypeName = m.MembershipType.Name,
                m.MembershipTypeId,
                m.StartDate,
                m.EndDate,
                m.Status,
                m.Notes,
                m.FrozenFrom,
                m.FrozenTo,
                m.VisitsUsed,
                IncludesClasses = m.MembershipType.IncludesClasses
            })
            .ToListAsync();

        return Ok(memberships);
    }

    // GET api/memberships/types
    [HttpGet("types")]
    public async Task<IActionResult> GetTypes()
    {
        var types = await _db.MembershipTypes
            .Where(t => t.IsActive)
            .ToListAsync();
        return Ok(types);
    }

    // POST api/memberships — створення + відправка QR в Telegram
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMembershipDto dto)
    {
        var client = await _db.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == dto.ClientId);

        if (client == null)
            return NotFound(new { error = "Клієнта не знайдено" });

        var type = await _db.MembershipTypes.FindAsync(dto.MembershipTypeId);
        if (type == null)
            return NotFound(new { error = "Тип абонементу не знайдено" });

        // Скасовуємо попередній активний абонемент
        var active = await _db.Memberships
            .Where(m => m.ClientId == dto.ClientId && m.Status == "Active")
            .FirstOrDefaultAsync();
        if (active != null)
            active.Status = "Cancelled";

        var startDate = DateOnly.FromDateTime(DateTime.Today);
        var membership = new Membership
        {
            ClientId = dto.ClientId,
            MembershipTypeId = dto.MembershipTypeId,
            StartDate = startDate,
            EndDate = startDate.AddDays(type.DurationDays),
            Status = "Active",
            Notes = dto.Notes
        };
        _db.Memberships.Add(membership);

        var payment = new Payment
        {
            ClientId = dto.ClientId,
            Amount = type.Price,
            PaymentMethod = dto.PaymentMethod,
            Description = $"Абонемент: {type.Name}",
            Status = "Completed"
        };
        _db.Payments.Add(payment);

        await _db.SaveChangesAsync();

        payment.MembershipId = membership.Id;
        await _db.SaveChangesAsync();

        // ── Відправляємо QR в Telegram якщо клієнт прив'язав акаунт ──
        if (client.User.TelegramChatId.HasValue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _telegram.SendMembershipQrAsync(
                        chatId: client.User.TelegramChatId.Value,
                        clientName: client.User.FullName,
                        clientId: client.Id,
                        membershipType: type.Name,
                        expiryDate: membership.EndDate.ToDateTime(TimeOnly.MinValue));
                }
                catch { /* не ламаємо відповідь якщо Telegram недоступний */ }
            });
        }

        return Ok(new
        {
            membershipId = membership.Id,
            endDate = membership.EndDate,
            telegramSent = client.User.TelegramChatId.HasValue
        });
    }

    // PUT api/memberships/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMembershipDto dto)
    {
        var membership = await _db.Memberships.FindAsync(id);
        if (membership == null) return NotFound();

        membership.MembershipTypeId = dto.MembershipTypeId;
        membership.StartDate = dto.StartDate;
        membership.EndDate = dto.EndDate;
        membership.Status = dto.Status;
        membership.Notes = dto.Notes;
        membership.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/memberships/5/freeze
    [HttpPut("{id}/freeze")]
    public async Task<IActionResult> Freeze(int id, [FromBody] FreezeMembershipDto dto)
    {
        var membership = await _db.Memberships.FindAsync(id);
        if (membership == null) return NotFound();

        membership.Status = "Frozen";
        membership.FrozenFrom = dto.FrozenFrom;
        membership.FrozenTo = dto.FrozenTo;
        membership.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/memberships/5/unfreeze
    [HttpPut("{id}/unfreeze")]
    public async Task<IActionResult> Unfreeze(int id)
    {
        var membership = await _db.Memberships.FindAsync(id);
        if (membership == null) return NotFound();

        membership.Status = "Active";
        membership.FrozenFrom = null;
        membership.FrozenTo = null;
        membership.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/memberships/5/cancel
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var membership = await _db.Memberships.FindAsync(id);
        if (membership == null) return NotFound();

        membership.Status = "Cancelled";
        membership.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/memberships/5/refund
    [HttpPut("{id}/refund")]
    public async Task<IActionResult> Refund(int id, [FromBody] RefundMembershipDto dto)
    {
        var membership = await _db.Memberships
            .Include(m => m.Client).ThenInclude(c => c.User)
            .Include(m => m.MembershipType)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (membership == null) return NotFound();
        if (membership.Status == "Cancelled")
            return BadRequest(new { error = "Абонемент вже скасовано" });

        var originalPayment = await _db.Payments
            .Where(p => p.MembershipId == id && p.Amount > 0)
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();

        var refundAmount = dto.Amount ?? originalPayment?.Amount
                           ?? membership.MembershipType.Price;

        _db.Payments.Add(new Payment
        {
            ClientId = membership.ClientId,
            MembershipId = id,
            Amount = -refundAmount,
            PaymentMethod = originalPayment?.PaymentMethod ?? "Cash",
            Description = $"Повернення: {membership.MembershipType.Name}",
            Status = "Refunded"
        });

        membership.Status = "Cancelled";
        membership.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { refundAmount });
    }

    // DELETE api/memberships/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var membership = await _db.Memberships.FindAsync(id);
        if (membership == null) return NotFound();

        _db.Memberships.Remove(membership);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateMembershipDto(int ClientId, int MembershipTypeId, string? PaymentMethod, string? Notes);
public record FreezeMembershipDto(DateOnly FrozenFrom, DateOnly FrozenTo);
public record UpdateMembershipDto(int MembershipTypeId, DateOnly StartDate, DateOnly EndDate, string Status, string? Notes);
public record RefundMembershipDto(decimal? Amount);