using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;
using FitnessClub.API.Models;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ClientsController(AppDbContext db) => _db = db;

    // ← GET api/clients/me — для мобільного застосунку
    [HttpGet("me")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetMe()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var client = await _db.Clients
            .Include(c => c.User)
            .Include(c => c.Memberships).ThenInclude(m => m.MembershipType)
            .FirstOrDefaultAsync(c => c.User.Email == email);

        if (client == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var activeMembership = client.Memberships
            .Where(m => m.Status == "Active" && m.EndDate >= today)
            .OrderByDescending(m => m.EndDate)
            .FirstOrDefault();

        return Ok(new
        {
            client.Id,
            client.User.FirstName,
            client.User.LastName,
            client.User.Email,
            client.User.Phone,
            ActiveMembership = activeMembership == null ? null : new
            {
                activeMembership.Id,
                activeMembership.MembershipType.Name,
                activeMembership.StartDate,
                activeMembership.EndDate,
                activeMembership.Status,
                activeMembership.MembershipType.IncludesClasses,
                DaysLeft = activeMembership.EndDate.DayNumber -
                           DateOnly.FromDateTime(DateTime.Today).DayNumber
            }
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var query = _db.Clients
            .Include(c => c.User)
            .Include(c => c.Memberships).ThenInclude(m => m.MembershipType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(c =>
                c.User.FirstName.ToLower().Contains(search) ||
                c.User.LastName.ToLower().Contains(search) ||
                c.User.Email.ToLower().Contains(search) ||
                (c.User.Phone != null && c.User.Phone.Contains(search)));
        }

        var clients = await query.Select(c => new
        {
            c.Id,
            c.UserId,
            c.User.FirstName,
            c.User.LastName,
            c.User.Email,
            c.User.Phone,
            c.User.IsActive,
            c.DateOfBirth,
            c.Gender,
            c.PhotoUrl,
            ActiveMembership = c.Memberships
                .Where(m => m.Status == "Active")
                .Select(m => new
                {
                    m.Id,
                    m.MembershipType.Name,
                    m.EndDate,
                    m.StartDate,
                    m.Status
                })
                .FirstOrDefault()
        }).ToListAsync();

        return Ok(clients);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,Trainer")]
    public async Task<IActionResult> GetById(int id)
    {
        var client = await _db.Clients
            .Include(c => c.User)
            .Include(c => c.Memberships).ThenInclude(m => m.MembershipType)
            .Include(c => c.Payments)
            .Include(c => c.Visits)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null) return NotFound();

        return Ok(new
        {
            client.Id,
            client.User.FirstName,
            client.User.LastName,
            client.User.Email,
            client.User.Phone,
            client.DateOfBirth,
            client.Gender,
            client.Address,
            client.EmergencyContact,
            client.HealthNotes,
            client.PhotoUrl,
            Memberships = client.Memberships.Select(m => new
            {
                m.Id,
                m.MembershipType.Name,
                m.StartDate,
                m.EndDate,
                m.Status,
                m.VisitsUsed
            }),
            RecentPayments = client.Payments
                .OrderByDescending(p => p.PaymentDate)
                .Take(5)
                .Select(p => new { p.Amount, p.PaymentDate, p.PaymentMethod }),
            TotalVisits = client.Visits.Count
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { error = "Email вже використовується" });

        var clientRoleId = await _db.Roles
            .Where(r => r.Name == "Client")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 11);

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = hash,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Phone = dto.Phone,
            RoleId = clientRoleId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var client = new Client
        {
            UserId = user.Id,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            Address = dto.Address,
            EmergencyContact = dto.EmergencyContact,
            HealthNotes = dto.HealthNotes
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.Id },
            new { client.Id, user.FullName });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClientDto dto)
    {
        var client = await _db.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null) return NotFound();

        client.User.FirstName = dto.FirstName;
        client.User.LastName = dto.LastName;
        client.User.Phone = dto.Phone;
        client.DateOfBirth = dto.DateOfBirth;
        client.Gender = dto.Gender;
        client.Address = dto.Address;
        client.EmergencyContact = dto.EmergencyContact;
        client.HealthNotes = dto.HealthNotes;
        client.User.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null) return NotFound();

        client.User.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/checkin")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        var visit = new Visit { ClientId = id };
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        return Ok(new { visitId = visit.Id, checkIn = visit.CheckIn });
    }
}

public record CreateClientDto(
    string Email, string Password,
    string FirstName, string LastName,
    string? Phone, DateOnly? DateOfBirth,
    string? Gender, string? Address,
    string? EmergencyContact, string? HealthNotes
);

public record UpdateClientDto(
    string FirstName, string LastName,
    string? Phone, DateOnly? DateOfBirth,
    string? Gender, string? Address,
    string? EmergencyContact, string? HealthNotes
);