// BroadcastHistory.cs
namespace FitnessClub.API.Data.Entities;

public class BroadcastHistory
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
    public string TargetType { get; set; } = "all";
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int AdminId { get; set; }
    public DateTime SentAt { get; set; }
}