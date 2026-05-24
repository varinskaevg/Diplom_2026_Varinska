namespace FitnessClub.API.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int ScheduleId { get; set; }
        public int ClientId { get; set; }
        public string Status { get; set; } = "Booked"; // Booked, Attended, Cancelled, NoShow
        public DateTime BookedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CancelledAt { get; set; }

        public Schedule Schedule { get; set; } = null!;
        public Client Client { get; set; } = null!;

        // ── НОВІ ПОЛЯ ──
        /// <summary>Звідки прийшло бронювання: Manual (десктоп/адмін) або Telegram (бот)</summary>
        public string Source { get; set; } = "Manual";

        /// <summary>Статус підтвердження тренером: None | Pending | Confirmed | Rejected</summary>
        public string TelegramStatus { get; set; } = "None";
    }
}
