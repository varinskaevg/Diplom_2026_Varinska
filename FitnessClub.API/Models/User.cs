using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessClub.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        [Column("password_hash")]
        public string PasswordHash { get; set; } = "";
        [Column("first_name")]
        public string FirstName { get; set; } = "";
        [Column("last_name")]
        public string LastName { get; set; } = "";
        public string? Phone { get; set; }

        [Column("role_id")]   // <-- ось тут
        public int RoleId { get; set; }

        public bool IsActive { get; set; } = true;
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}";
        // Telegram Chat ID — зберігається коли клієнт пише боту /start
        [Column("telegram_chat_id")]
        public long? TelegramChatId { get; set; }

        public Role Role { get; set; } = null!;
        public Client? Client { get; set; }
        public Trainer? Trainer { get; set; }
    }
}