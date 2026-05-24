using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Pages;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class ScheduleDetailDialog : Window
{
    private readonly int _id;
    private readonly ScheduleItem _item;

    public ScheduleDetailDialog(int id, ScheduleItem item)
    {
        InitializeComponent();
        _id = id;
        _item = item;

        if (AppSession.IsAdmin || AppSession.IsManager || AppSession.IsTrainer)
            EditBtn.Visibility = Visibility.Visible;

        Loaded += async (_, _) => await LoadDetail();
    }

    private async Task LoadDetail()
    {
        try
        {
            var data = await ApiClient.GetAsync<JsonElement>($"api/schedules/{_id}");

            var className = data.GetProperty("className").GetString() ?? "";
            TitleText.Text = className;

            var start = data.GetProperty("startDatetime").GetDateTime().ToLocalTime();
            var end = data.GetProperty("endDatetime").GetDateTime().ToLocalTime();
            TimeText.Text = $"{start:HH:mm} – {end:HH:mm}";
            DurationText.Text = $"{(int)(end - start).TotalMinutes} хв  •  {start:dd.MM.yyyy}";
            TrainerText.Text = data.GetProperty("trainerName").GetString();
            RoomText.Text = data.TryGetProperty("room", out var room) && room.ValueKind != JsonValueKind.Null
                ? room.GetString() : "—";

            var status = data.GetProperty("status").GetString() ?? "";
            StatusText.Text = status switch
            {
                "Scheduled" => "🟣 Заплановано",
                "Completed" => "🟢 Завершено",
                "Cancelled" => "🔴 Скасовано",
                _ => status
            };
            StatusText.Foreground = status switch
            {
                "Completed" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34d399")),
                "Cancelled" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f87171")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a78bfa"))
            };

            var bookings = data.GetProperty("bookingsCount").GetInt32();
            var capacity = data.GetProperty("maxCapacity").GetInt32();
            CapacityText.Text = $"{bookings} / {capacity}";

            // Прогрес бар
            if (capacity > 0)
                LoadBar.Width = Math.Min(440.0 * bookings / capacity, 440.0);
            else
                LoadBar.Width = 0;

            // Нотатки
            if (data.TryGetProperty("notes", out var notes) && notes.ValueKind != JsonValueKind.Null
                && !string.IsNullOrEmpty(notes.GetString()))
            {
                NotesText.Text = notes.GetString();
                NotesPanel.Visibility = Visibility.Visible;
            }

            // Записані клієнти
            var bookingsList = data.GetProperty("bookings");
            var list = bookingsList.EnumerateArray().Select(b => new BookingRow
            {
                ClientName = b.GetProperty("clientName").GetString() ?? "",
                Status = b.GetProperty("status").GetString() ?? "",
                Initials = GetInitials(b.GetProperty("clientName").GetString() ?? "")
            }).ToList();

            if (list.Any())
            {
                BookingsList.ItemsSource = list;
                NoBookingsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoBookingsText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private string GetInitials(string name)
    {
        var parts = name.Split(' ');
        return parts.Length >= 2
            ? $"{parts[0].FirstOrDefault()}{parts[1].FirstOrDefault()}".ToUpper()
            : name.FirstOrDefault().ToString().ToUpper();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EditScheduleDialog(_id, _item);
        if (dialog.ShowDialog() == true)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public class BookingRow
{
    public string ClientName { get; set; } = "";
    public string Status { get; set; } = "";
    public string Initials { get; set; } = "";
}