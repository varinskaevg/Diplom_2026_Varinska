namespace FitnessClub.Desktop.Models;

public class DashboardStats
{
    public int TotalClients { get; set; }
    public int ActiveMembers { get; set; }
    public double MonthRevenue { get; set; }
    public int TodayVisits { get; set; }
    public int TotalTrainers { get; set; }
    public int TodaySchedules { get; set; }
    public int ExpiringSoon { get; set; }

    // Для графіків — якщо бекенд їх повертає
    // Якщо не повертає — DashboardPage.cs згенерує тестові дані
    public double[]? MonthlyRevenue { get; set; }
    public int[]? WeekVisits { get; set; }

    public List<PaymentItem>? RecentPayments { get; set; }
}

public class PaymentItem
{
    public string ClientName { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public DateTime PaymentDate { get; set; }
    public double Amount { get; set; }
}