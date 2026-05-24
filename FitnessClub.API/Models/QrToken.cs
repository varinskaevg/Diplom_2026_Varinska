using System.ComponentModel.DataAnnotations.Schema;
namespace FitnessClub.API.Models

{
    /// <summary>
    /// Одноразовий QR-токен для входу до клубу.
    /// Генерується на 24 години, після використання — деактивується.
    /// </summary>
    [Table("qrtokens")]
    public class QrToken
    {
        public int Id { get; set; }

        /// <summary>Клієнт якому належить токен</summary>
        public int ClientId { get; set; }

        /// <summary>Унікальний токен (GUID) — вміст QR-коду</summary>
        public string Token { get; set; } = "";

        /// <summary>Коли згенеровано (UTC)</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Коли закінчується (UTC) — зазвичай CreatedAt + 24h</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>Чи вже використаний для входу</summary>
        public bool IsUsed { get; set; } = false;

        /// <summary>Коли використаний</summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>Visit який було створено при скануванні</summary>
        public int? VisitId { get; set; }

        // Навігаційні властивості
        public Client Client { get; set; } = null!;
        public Visit? Visit { get; set; }
    }
}