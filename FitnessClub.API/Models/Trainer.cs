namespace FitnessClub.API.Models
{
    public class Trainer
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Specialization { get; set; }
        public int ExperienceYears { get; set; }
        public string? Bio { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal? GroupRate { get; set; }
        public decimal? IndividualRate { get; set; }
        public decimal? MonthlyPlanRate { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public ICollection<Schedule> Schedules { get; set; } = [];
    }
}