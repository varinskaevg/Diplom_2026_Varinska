namespace FitnessClub.API.Models
{
    public class Client
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContact { get; set; }
        public string? HealthNotes { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public ICollection<Membership> Memberships { get; set; } = [];
        public ICollection<Payment> Payments { get; set; } = [];
        public ICollection<Booking> Bookings { get; set; } = [];
        public ICollection<Visit> Visits { get; set; } = [];
    }
}
