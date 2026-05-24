using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessClub.API.Models
{
    [Table("membership_types")]
    public class MembershipType
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public int? MaxVisits { get; set; }
        public bool IncludesPool { get; set; }
        public bool IncludesGym { get; set; } = true;
        public bool IncludesClasses { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Membership> Memberships { get; set; } = [];
    }
}
