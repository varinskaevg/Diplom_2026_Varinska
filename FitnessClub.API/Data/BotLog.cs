// BotLog.cs (для статистики)
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessClub.API.Data.Entities;

public class BotLog
{
    [Column("id")]
    public int Id { get; set; }
    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("message")]
    public string Message { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("command")]
    public string? Command { get; set; }
}