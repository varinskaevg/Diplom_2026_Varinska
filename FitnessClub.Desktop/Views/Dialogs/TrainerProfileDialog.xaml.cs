using System.Text.Json;
using System.Windows;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Pages;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class TrainerProfileDialog : Window
{
    private readonly int _id;
    private readonly TrainerViewModel _trainer;

    public TrainerProfileDialog(int id, TrainerViewModel trainer)
    {
        InitializeComponent();
        _id = id;
        _trainer = trainer;
        Loaded += async (_, _) => await LoadProfile();
    }

    private async Task LoadProfile()
    {
        try
        {
            InitialsText.Text = _trainer.Initials;
            NameText.Text = _trainer.FullName;
            SpecText.Text = _trainer.Specialization ?? "Тренер";
            EmailText.Text = _trainer.Email;

            var data = await ApiClient.GetAsync<JsonElement>($"api/trainers/{_id}");

            ExpText.Text = data.GetProperty("experienceYears").GetInt32().ToString();
            SessionsText.Text = data.GetProperty("completedSchedules").GetInt32().ToString();

            var rate = data.TryGetProperty("hourlyRate", out var r) && r.ValueKind != JsonValueKind.Null
                ? r.GetDecimal() : 0;
            RateText.Text = rate > 0 ? $"{rate:N0}" : "—";

            TenureText.Text = _trainer.WorkingFor;
            PhoneText.Text = _trainer.Phone ?? "—";
            EmailInfoText.Text = _trainer.Email;

            // Ціни послуг
            GroupRateText.Text = data.TryGetProperty("groupRate", out var gr) && gr.ValueKind != JsonValueKind.Null && gr.GetDecimal() > 0
                ? $"{gr.GetDecimal():N0} ₴" : "—";
            IndividualRateText.Text = data.TryGetProperty("individualRate", out var ir) && ir.ValueKind != JsonValueKind.Null && ir.GetDecimal() > 0
                ? $"{ir.GetDecimal():N0} ₴" : "—";
            MonthlyRateText.Text = data.TryGetProperty("monthlyPlanRate", out var mpr) && mpr.ValueKind != JsonValueKind.Null && mpr.GetDecimal() > 0
                ? $"{mpr.GetDecimal():N0} ₴" : "—";

            if (data.TryGetProperty("bio", out var bio) && bio.ValueKind != JsonValueKind.Null
                && !string.IsNullOrEmpty(bio.GetString()))
            {
                BioText.Text = bio.GetString();
                BioPanel.Visibility = Visibility.Visible;
            }

            var schedules = data.GetProperty("recentSchedules").EnumerateArray()
                .Select(s => new TrainerScheduleRow
                {
                    ClassName = s.GetProperty("className").GetString() ?? "",
                    Date = s.GetProperty("startDatetime").GetDateTime().ToLocalTime(),
                    Status = s.GetProperty("status").GetString() ?? "",
                    BookingsCount = s.GetProperty("bookingsCount").GetInt32()
                }).ToList();

            SchedulesList.ItemsSource = schedules;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public class TrainerScheduleRow
{
    public string ClassName { get; set; } = "";
    public DateTime Date { get; set; }
    public string Status { get; set; } = "";
    public int BookingsCount { get; set; }
    public string DateStr => Date.ToString("dd.MM.yy HH:mm");
    public string StatusUa => Status switch
    {
        "Scheduled" => "План.",
        "Completed" => "Завер.",
        "Cancelled" => "Скас.",
        _ => Status
    };
}