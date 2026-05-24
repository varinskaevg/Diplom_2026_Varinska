using BCrypt.Net;
using FitnessClub.API.Data; // твій DbContext
using FitnessClub.API.Models; // твої моделі Users

public class DbSeeder
{
    public static void SeedPasswords(AppDbContext context)
    {
        foreach (var user in context.Users)
        {
            // якщо пароль ще plain (не починається з $2), то хешуємо
            if (!user.PasswordHash.StartsWith("$2"))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            }
        }

        context.SaveChanges();
    }
}