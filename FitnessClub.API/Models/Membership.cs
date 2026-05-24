namespace FitnessClub.API.Models
{
    public class Membership
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int MembershipTypeId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int VisitsUsed { get; set; }
        public string Status { get; set; } = "Active"; // Active, Expired, Frozen, Cancelled
        public DateOnly? FrozenFrom { get; set; }
        public DateOnly? FrozenTo { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsExpired => EndDate < DateOnly.FromDateTime(DateTime.Today);

        public Client Client { get; set; } = null!;
        public MembershipType MembershipType { get; set; } = null!;
    }
}
