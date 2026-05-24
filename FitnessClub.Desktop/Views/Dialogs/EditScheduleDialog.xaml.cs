using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Pages;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class EditScheduleDialog : Window
{
    private readonly int? _id;
    private readonly ScheduleItem? _item;

    // ── Робочі години закладу ──────────────────────────────────
    private const int GymOpenHour = 9;
    private const int GymCloseHour = 22;

    public EditScheduleDialog(int? id, ScheduleItem? item)
    {
        InitializeComponent();
        _id = id;
        _item = item;
        if (id != null)
        {
            TitleText.Text = "Редагувати заняття";
            StatusPanel.Visibility = Visibility.Visible;
        }
        Loaded += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            var classTypes = await ApiClient.GetAsync<List<ClassTypeItem>>("api/schedules/classtypes");
            if (classTypes != null)
                ClassTypeBox.ItemsSource = classTypes;

            var trainers = await ApiClient.GetAsync<List<TrainerItem>>("api/trainers");
            if (trainers != null)
                TrainerBox.ItemsSource = trainers;

            if (_item != null)
            {
                // Бекенд повертає UTC (timestamptz + Z) → конвертуємо в local для показу
                var local = _item.StartDatetime.ToLocalTime();
                DatePicker.SelectedDate = local.Date;
                StartTimeBox.Text = local.ToString("HH:mm");
                DurationBox.Text = ((int)(_item.EndDatetime - _item.StartDatetime).TotalMinutes).ToString();
                RoomBox.Text = _item.Room ?? "";
                CapacityBox.Text = _item.MaxCapacity.ToString();
                NotesBox.Text = _item.Notes ?? "";

                if (classTypes != null)
                    ClassTypeBox.SelectedItem = classTypes.FirstOrDefault(t => t.Id == _item.ClassTypeId);
                if (trainers != null)
                    TrainerBox.SelectedItem = trainers.FirstOrDefault(t => t.Id == _item.TrainerId);

                foreach (ComboBoxItem si in StatusBox.Items)
                    if (si.Tag?.ToString() == _item.Status)
                    { StatusBox.SelectedItem = si; break; }
            }
            else
            {
                DatePicker.SelectedDate = DateTime.Today;
                StartTimeBox.Text = $"{GymOpenHour:00}:00";
                StatusBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження: {ex.Message}");
        }
    }

    private void ClassTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassTypeBox.SelectedItem is ClassTypeItem ct)
        {
            ClassTypeBadge.Visibility = Visibility.Visible;
            ClassTypeBadgeIcon.Text = ct.IsIndividual ? "👤" : "👥";
            ClassTypeBadgeLabel.Text = ct.IsIndividual ? "Індивідуальне" : "Групове";
            ClassTypeBadge.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(ct.IsIndividual ? "#0f1a30" : "#1a0f30"));
            ClassTypeBadgeLabel.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(ct.IsIndividual ? "#60a5fa" : "#c084fc"));
        }
        else
        {
            ClassTypeBadge.Visibility = Visibility.Collapsed;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        HideError();

        // ── Валідація полів ──────────────────────────────────────
        if (ClassTypeBox.SelectedItem is not ClassTypeItem classType)
        { ShowError("Оберіть тип заняття"); return; }

        if (TrainerBox.SelectedItem is not TrainerItem trainer)
        { ShowError("Оберіть тренера"); return; }

        if (DatePicker.SelectedDate == null)
        { ShowError("Вкажіть дату"); return; }

        if (!TimeSpan.TryParse(StartTimeBox.Text.Trim(), out var startTime))
        { ShowError("Невірний формат часу — введіть у форматі ГГ:хх (наприклад 10:30)"); return; }

        if (!int.TryParse(DurationBox.Text, out var duration) || duration <= 0)
        { ShowError("Невірна тривалість"); return; }

        // ── Перевірка робочих годин ──────────────────────────────
        int startHour = startTime.Hours;
        int startMin = startTime.Minutes;

        if (startHour < GymOpenHour || startHour >= GymCloseHour)
        {
            ShowError($"Заклад зачинений — заняття можна додавати з {GymOpenHour:00}:00 до {GymCloseHour:00}:00");
            return;
        }

        // Перевіряємо що і кінець заняття не виходить за межі
        var endTime = startTime.Add(TimeSpan.FromMinutes(duration));
        if (endTime.Hours > GymCloseHour || (endTime.Hours == GymCloseHour && endTime.Minutes > 0))
        {
            ShowError($"Заняття закінчується після {GymCloseHour:00}:00 — скоротіть тривалість або змініть час початку");
            return;
        }

        // ── Формуємо datetime для API ────────────────────────────
        // ✅ Відправляємо як рядок без timezone-суфікса.
        // Бекенд отримає "2025-03-06T10:30:00" і SpecifyKind(Utc) збереже саме цей час.
        // На клієнті при читанні НЕ викликаємо ToLocalTime() — час вже правильний.
        var localStart = DatePicker.SelectedDate.Value.Date.Add(startTime);
        var localEnd = localStart.AddMinutes(duration);

        // Форматуємо без суфікса 'Z' і без '+03:00' — чистий ISO рядок
        var startStr = localStart.ToString("yyyy-MM-ddTHH:mm:ss");
        var endStr = localEnd.ToString("yyyy-MM-ddTHH:mm:ss");

        var status = (_id != null
            ? (StatusBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            : "Scheduled") ?? "Scheduled";

        try
        {
            var body = new
            {
                classTypeId = classType.Id,
                trainerId = trainer.Id,
                startDatetime = startStr,   // ← рядок, без timezone
                endDatetime = endStr,     // ← рядок, без timezone
                room = RoomBox.Text.Trim(),
                maxCapacity = int.TryParse(CapacityBox.Text, out var cap) ? cap : 20,
                status,
                notes = NotesBox.Text.Trim()
            };

            if (_id == null)
                await ApiClient.PostAsync<object>("api/schedules", body);
            else
                await ApiClient.PutAsync($"api/schedules/{_id}", body);

            DialogResult = true;
            Close();
        }
        catch (Exception ex) { ShowError($"Помилка збереження: {ex.Message}"); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }
}

public class ClassTypeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsIndividual { get; set; }
}

public class TrainerItem
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Specialization { get; set; }
    public string FullName => $"{FirstName} {LastName}";
}