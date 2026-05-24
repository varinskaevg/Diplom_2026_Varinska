using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Dialogs;

namespace FitnessClub.Desktop.Views.Pages;

public partial class TrainersPage : Page
{
    private List<TrainerViewModel> _trainers = [];

    public TrainersPage()
    {
        InitializeComponent();
        if (AppSession.IsAdmin || AppSession.IsManager)
            AddTrainerBtn.Visibility = Visibility.Visible;
        Loaded += async (_, _) => await LoadTrainers();
    }

    private async Task LoadTrainers()
    {
        try
        {
            var data = await ApiClient.GetAsync<List<JsonElement>>("api/trainers");
            if (data == null) return;

            _trainers = data.Select(t => new TrainerViewModel
            {
                Id = t.GetProperty("id").GetInt32(),
                FirstName = t.GetProperty("firstName").GetString() ?? "",
                LastName = t.GetProperty("lastName").GetString() ?? "",
                Email = t.GetProperty("email").GetString() ?? "",
                Bio = t.TryGetProperty("bio", out var b) && b.ValueKind != JsonValueKind.Null ? b.GetString() : null,
                Phone = t.TryGetProperty("phone", out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null,
                Specialization = t.TryGetProperty("specialization", out var s) && s.ValueKind != JsonValueKind.Null ? s.GetString() : "Тренер",
                ExperienceYears = t.GetProperty("experienceYears").GetInt32(),
                HourlyRate = t.TryGetProperty("hourlyRate", out var r) && r.ValueKind != JsonValueKind.Null ? r.GetDecimal() : 0,
                GroupRate = t.TryGetProperty("groupRate", out var gr) && gr.ValueKind != JsonValueKind.Null ? gr.GetDecimal() : 0,
                IndividualRate = t.TryGetProperty("individualRate", out var ir) && ir.ValueKind != JsonValueKind.Null ? ir.GetDecimal() : 0,
                MonthlyPlanRate = t.TryGetProperty("monthlyPlanRate", out var mpr) && mpr.ValueKind != JsonValueKind.Null ? mpr.GetDecimal() : 0,
                IsActive = t.GetProperty("isActive").GetBoolean(),
                CreatedAt = t.GetProperty("createdAt").GetDateTime(),
                CompletedSchedules = t.GetProperty("completedSchedules").GetInt32()
            }).ToList();

            TrainersList.ItemsSource = _trainers;
            SalaryTrainerBox.ItemsSource = _trainers.Where(t => t.IsActive).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private void TabCards_Click(object sender, RoutedEventArgs e)
    {
        CardsPanel.Visibility = Visibility.Visible;
        SalaryPanel.Visibility = Visibility.Collapsed;
        SetTabActive(TabCardsBtn, true);
        SetTabActive(TabSalaryBtn, false);
    }

    private void TabSalary_Click(object sender, RoutedEventArgs e)
    {
        CardsPanel.Visibility = Visibility.Collapsed;
        SalaryPanel.Visibility = Visibility.Visible;
        SetTabActive(TabCardsBtn, false);
        SetTabActive(TabSalaryBtn, true);
        SalaryFromDate.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        SalaryToDate.SelectedDate = DateTime.Today;
    }

    private void SetTabActive(Button btn, bool active)
    {
        btn.Background = active
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C63FF"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1f35"));
        btn.Foreground = active ? Brushes.White
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af"));
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var trainer = _trainers.FirstOrDefault(t => t.Id == id);
            if (trainer == null) return;
            var dialog = new TrainerProfileDialog(id, trainer);
            dialog.ShowDialog();
        }
    }

    private void EditTrainer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var trainer = _trainers.FirstOrDefault(t => t.Id == id);
            if (trainer == null) return;
            var dialog = new EditTrainerDialog(id, trainer);
            if (dialog.ShowDialog() == true)
                _ = LoadTrainers();
        }
    }

    private async void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var trainer = _trainers.FirstOrDefault(t => t.Id == id);
            if (trainer == null) return;

            var action = trainer.IsActive ? "звільнити" : "поновити";
            if (MessageBox.Show($"Ви впевнені що хочете {action} {trainer.FullName}?",
                "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    var endpoint = trainer.IsActive
                        ? $"api/trainers/{id}/deactivate"
                        : $"api/trainers/{id}/activate";
                    await ApiClient.PutAsync(endpoint, new { });
                    await LoadTrainers();
                }
                catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
            }
        }
    }

    private void AddTrainer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EditTrainerDialog(null, null);
        if (dialog.ShowDialog() == true)
            _ = LoadTrainers();
    }

    private async void CalcSalary_Click(object sender, RoutedEventArgs e)
    {
        if (SalaryTrainerBox.SelectedItem is not TrainerViewModel trainer)
        { MessageBox.Show("Оберіть тренера"); return; }
        if (SalaryFromDate.SelectedDate == null || SalaryToDate.SelectedDate == null)
        { MessageBox.Show("Вкажіть період"); return; }

        try
        {
            var from = SalaryFromDate.SelectedDate.Value;
            var to = SalaryToDate.SelectedDate.Value;
            var data = await ApiClient.GetAsync<JsonElement>(
                $"api/trainers/{trainer.Id}/salary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

            // Загальна статистика
            TotalSessionsText.Text = data.GetProperty("totalSessions").GetInt32().ToString();
            TotalHoursText.Text = data.GetProperty("totalHours").GetDouble().ToString("F1");
            BaseTotalText.Text = $"{data.GetProperty("baseTotal").GetDecimal():N0} ₴";
            BonusTotalText.Text = $"{data.GetProperty("bonusTotal").GetDecimal():N0} ₴";
            GrandTotalText.Text = $"{data.GetProperty("grandTotal").GetDecimal():N0} ₴";

            // Індивідуальні / групові кількість
            var indivCount = data.TryGetProperty("individualSessions", out var iv) ? iv.GetInt32() : 0;
            var groupCount = data.TryGetProperty("groupSessions", out var gv) ? gv.GetInt32() : 0;
            IndividualSessionsText.Text = indivCount.ToString();
            GroupSessionsText.Text = groupCount.ToString();

            // Місячний план бонус
            var monthlyBonus = data.TryGetProperty("monthlyPlanBonus", out var mb) ? mb.GetDecimal() : 0;
            if (monthlyBonus > 0)
            {
                MonthlyBonusPanel.Visibility = Visibility.Visible;
                MonthlyBonusText.Text = $"+{monthlyBonus:N0} ₴";
            }
            else
            {
                MonthlyBonusPanel.Visibility = Visibility.Collapsed;
            }

            SalarySummary.Visibility = Visibility.Visible;

            // Рядки таблиці
            var sessions = data.GetProperty("sessions").EnumerateArray().Select(s => new SalaryRow
            {
                ClassName = s.GetProperty("className").GetString() ?? "",
                Date = s.GetProperty("date").GetDateTime(),
                Hours = s.GetProperty("hours").GetDouble(),
                IsIndividual = s.TryGetProperty("isIndividual", out var isInd) && isInd.GetBoolean(),
                Participants = s.GetProperty("participants").GetInt32(),
                BaseEarning = s.GetProperty("baseEarning").GetDecimal(),
                Bonus = s.GetProperty("bonus").GetDecimal(),
                Total = s.GetProperty("total").GetDecimal()
            }).ToList();

            SalaryList.ItemsSource = sessions;
            NoSalaryText.Visibility = sessions.Any() ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
    }
}

public class TrainerViewModel
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? Specialization { get; set; }
    public int ExperienceYears { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal GroupRate { get; set; }
    public decimal IndividualRate { get; set; }
    public decimal MonthlyPlanRate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CompletedSchedules { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
    public string RateText => HourlyRate > 0 ? $"{HourlyRate:N0}" : "—";
    public string GroupRateText => GroupRate > 0 ? $"{GroupRate:N0} ₴" : "—";
    public string IndividualRateText => IndividualRate > 0 ? $"{IndividualRate:N0} ₴" : "—";
    public string MonthlyPlanRateText => MonthlyPlanRate > 0 ? $"{MonthlyPlanRate:N0} ₴" : "—";
    public string StatusText => IsActive ? "● Активний" : "● Звільнений";
    public string EditVisible => (AppSession.IsAdmin || AppSession.IsManager) ? "Visible" : "Collapsed";
    public string WorkingFor
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            var months = (int)(diff.TotalDays / 30);
            if (months < 1) return "менше місяця";
            if (months < 12) return $"{months} міс.";
            return $"{months / 12} р. {months % 12} міс.";
        }
    }
}

public class SalaryRow
{
    public string ClassName { get; set; } = "";
    public DateTime Date { get; set; }
    public double Hours { get; set; }
    public bool IsIndividual { get; set; }   // ← тепер з ClassType.IsIndividual
    public bool IsGroup => !IsIndividual;    // зворотня сумісність з XAML DataTrigger
    public int Participants { get; set; }
    public decimal BaseEarning { get; set; }
    public decimal Bonus { get; set; }
    public decimal Total { get; set; }
    public string DateStr => Date.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public string TypeLabel => IsIndividual ? "👤 Індив." : "👥 Груп.";
    public string BonusStr => Bonus > 0 ? $"+{Bonus:N0} ₴" : "—";
    public string BaseEarningStr => $"{BaseEarning:N0} ₴";
    public string TotalStr => $"{Total:N0} ₴";
}