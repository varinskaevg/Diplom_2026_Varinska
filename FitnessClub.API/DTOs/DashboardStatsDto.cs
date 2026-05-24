namespace FitnessClub.API.DTOs;

public class DashboardStatsDto
{
    public int TotalClients { get; set; }
    public int ActiveMembers { get; set; }
    public int ExpiringSoon { get; set; }
    public decimal MonthRevenue { get; set; }
    public int TodayVisits { get; set; }
    public int TotalTrainers { get; set; }
    public int TodaySchedules { get; set; }
    public List<RecentPaymentDto> RecentPayments { get; set; } = new();
}

public class RecentPaymentDto
{
    public int Id { get; set; }
    public string ClientName { get; set; } = "";
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
}