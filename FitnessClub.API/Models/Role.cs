using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessClub.API.Models
{
    public class Role
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";

        [Column("description")]
        public string? Description { get; set; }

        public ICollection<User> Users { get; set; } = [];
    }
}