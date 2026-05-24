namespace FitnessClub.API.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int? MembershipId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string? PaymentMethod { get; set; } // Cash, Card, Online
        public string Status { get; set; } = "Completed";
        public string? Description { get; set; }
        public int? CreatedBy { get; set; }

        public Client Client { get; set; } = null!;
        public Membership? Membership { get; set; }
    }
}
