// SupportMessage.cs
namespace FitnessClub.API.Data.Entities;

public class SupportMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Text { get; set; } = "";
    public bool IsFromAdmin { get; set; }
    public DateTime CreatedAt { get; set; }

    public SupportSession Session { get; set; } = null!;
}