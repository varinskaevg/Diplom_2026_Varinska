namespace FitnessClub.API.Models
{
    public class ClassType
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public int MaxParticipants { get; set; } = 20;
        public string Color { get; set; } = "#6C63FF";
        public bool IsIndividual { get; set; } = false;
        public ICollection<Schedule> Schedules { get; set; } = [];
    }
}
