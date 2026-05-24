using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Services;
using FitnessClub.API.Data;
using FitnessClub.API.Models;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AppDbContext _db;

    public AuthController(AuthService authService, AppDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var (success, token, role, fullName, error) =
            await _authService.LoginAsync(dto.Email, dto.Password);

        if (!success) return Unauthorized(new { error });

        var nameParts = (fullName ?? "").Split(' ');
        var firstName = nameParts.FirstOrDefault() ?? "";

        int? clientId = null;
        int? trainerId = null;

        if (role == "Client")
        {
            clientId = await _db.Clients
                .Include(c => c.User)
                .Where(c => c.User.Email == dto.Email)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
        }
        else if (role == "Trainer")
        {
            trainerId = await _db.Trainers
                .Include(t => t.User)
                .Where(t => t.User.Email == dto.Email)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();
        }

        return Ok(new
        {
            token,
            role,
            fullName,
            firstName,
            clientId,
            trainerId
        });
    }

    // ← Реєстрація клієнта з мобільного застосунку
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return Conflict(new { error = "Email вже використовується" });

        var clientRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Client");
        if (clientRole == null)
            return StatusCode(500, new { error = "Роль Client не знайдена" });

        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 11);

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = hash,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Phone = dto.Phone,
            RoleId = clientRole.Id,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var client = new Client { UserId = user.Id };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        // Одразу логінимо і повертаємо токен
        var (success, token, role, fullName, loginError) =
            await _authService.LoginAsync(dto.Email, dto.Password);

        if (!success)
            return StatusCode(500, new { error = loginError });

        return Ok(new
        {
            token,
            role,
            fullName,
            firstName = dto.FirstName,
            clientId = client.Id
        });
    }
}

public record LoginDto(string Email, string Password);
public record RegisterDto(
    string Email, string Password,
    string FirstName, string LastName,
    string? Phone
);