using FitnessClub.API.Data;
using FitnessClub.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.API.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;

    public AuthService(AppDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<(bool success, string token, string role, string fullName, string error)>
        LoginAsync(string email, string password)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        if (user == null)
            return (false, "", "", "", "Користувача не знайдено");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, "", "", "", "Невірний пароль");

        var token = _tokenService.GenerateToken(user);
        return (true, token, user.Role.Name, user.FullName, "");
    }

    public async Task<bool> RegisterUserAsync(
        string email, string password, string firstName,
        string lastName, string phone, int roleId)
    {
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return false;

        var hash = BCrypt.Net.BCrypt.HashPassword(password, 11);
        var user = new User
        {
            Email = email,
            PasswordHash = hash,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            RoleId = roleId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return true;
    }
}
