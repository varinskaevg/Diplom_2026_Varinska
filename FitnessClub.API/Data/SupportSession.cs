// SupportSession.cs
using FitnessClub.API.Data.Entities;
using FitnessClub.API.Models;

namespace FitnessClub.API.Data.Entities;

public class SupportSession
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }

    public User User { get; set; } = null!;
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}