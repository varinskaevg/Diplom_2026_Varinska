namespace FitnessClub.API.Models
{
    /// <summary>
    /// Прив'язка клієнта до тренера з типом оплати.
    /// PaymentType: "single" | "weekly" | "monthly"
    /// </summary>
    public class TrainerClient
    {
        public int Id { get; set; }
        public int TrainerId { get; set; }
        public int ClientId { get; set; }

        /// <summary>single | weekly | monthly</summary>
        public string PaymentType { get; set; } = "single";

        /// <summary>Вартість (разова / тижнева / місячна)</summary>
        public decimal Rate { get; set; }

        /// <summary>Дата початку прив'язки</summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        /// <summary>Дата закінчення (null = безстроково)</summary>
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Trainer Trainer { get; set; } = null!;
        public Client Client { get; set; } = null!;

        /// <summary>Платежі, пов'язані з цією прив'язкою</summary>
        public ICollection<TrainerClientPayment> Payments { get; set; } = [];
    }

    /// <summary>
    /// Окремий запис кожного платежу клієнта тренеру
    /// (1 разовий / 1 тиждень / 1 місяць = 1 запис)
    /// </summary>
    public class TrainerClientPayment
    {
        public int Id { get; set; }
        public int TrainerClientId { get; set; }

        /// <summary>Дата платежу</summary>
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        /// <summary>Сума платежу</summary>
        public decimal Amount { get; set; }

        /// <summary>Метод оплати: Cash | Card | Online</summary>
        public string PaymentMethod { get; set; } = "Cash";

        /// <summary>Примітка (наприклад, "Тиждень 12.05–18.05")</summary>
        public string? Note { get; set; }

        public TrainerClient TrainerClient { get; set; } = null!;
    }
}