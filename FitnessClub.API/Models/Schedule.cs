namespace FitnessClub.API.Models
{
    public class Schedule
    {
        public int Id { get; set; }
        public int ClassTypeId { get; set; }
        public int TrainerId { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string? Room { get; set; }
        public string Status { get; set; } = "Scheduled";
        public string? Notes { get; set; }
        public int MaxCapacity { get; set; } = 20;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ClassType ClassType { get; set; } = null!;
        public Trainer Trainer { get; set; } = null!;
        public ICollection<Booking> Bookings { get; set; } = [];
    }
}