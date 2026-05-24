namespace FitnessClub.API.Models
{
    public class Visit
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public DateTime CheckIn { get; set; } = DateTime.UtcNow;
        public DateTime? CheckOut { get; set; }
        public string? Notes { get; set; }

        public Client Client { get; set; } = null!;
    }
}
