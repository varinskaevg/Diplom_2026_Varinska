using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Dialogs;

namespace FitnessClub.Desktop.Views.Pages;

public partial class SchedulePage : Page
{
    private DateTime _currentDate = DateTime.Today;
    private bool _weekView = false;
    private List<ScheduleItem> _items = [];

    public SchedulePage()
    {
        InitializeComponent();
        if (AppSession.IsAdmin || AppSession.IsManager || AppSession.IsTrainer)
            AddBtn.Visibility = Visibility.Visible;

        Loaded += async (_, _) => await LoadSchedule();
    }

    private async Task LoadSchedule()
    {
        try
        {
            if (_weekView)
            {
                var monday = GetMonday(_currentDate);
                SubtitleText.Text = $"Тиждень: {monday:dd.MM} — {monday.AddDays(6):dd.MM.yyyy}";
                CurrentDateText.Text = $"{monday:dd MMM} — {monday.AddDays(6):dd MMM yyyy}";
                var items = await ApiClient.GetAsync<List<ScheduleItem>>(
                    $"api/schedules?weekStart={monday:yyyy-MM-dd}");
                _items = items ?? [];
                BuildWeekView(monday);
            }
            else
            {
                SubtitleText.Text = _currentDate.ToString("dddd, dd MMMM yyyy",
                    new System.Globalization.CultureInfo("uk-UA"));
                CurrentDateText.Text = _currentDate.ToString("dd MMMM yyyy",
                    new System.Globalization.CultureInfo("uk-UA"));
                var items = await ApiClient.GetAsync<List<ScheduleItem>>(
                    $"api/schedules?date={_currentDate:yyyy-MM-dd}");
                _items = items ?? [];
                ScheduleList.ItemsSource = _items;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    // ── Кольори з хекс-рядка ─────────────────────────────────────
    private static Color Hex(string h) =>
        (Color)ColorConverter.ConvertFromString(h);

    private static SolidColorBrush Brush(string h) =>
        new(Hex(h));

    // ── Тижневий вигляд ──────────────────────────────────────────
    private void BuildWeekView(DateTime monday)
    {
        WeekHeader.Children.Clear();
        WeekGrid.Children.Clear();

        string[] days = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд"];

        for (int i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            bool isToday = day.Date == DateTime.Today;

            // ── Заголовок дня ────────────────────────────────────
            var headerBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(8, 12, 8, 12),
                BorderThickness = new Thickness(1)
            };

            if (isToday)
            {
                var gb = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1)
                };
                gb.GradientStops.Add(new GradientStop(Hex("#221E5C"), 0));
                gb.GradientStops.Add(new GradientStop(Hex("#161340"), 1));
                headerBorder.Background = gb;
                headerBorder.BorderBrush = Brush("#4A3FC0");
                headerBorder.Effect = new DropShadowEffect
                {
                    Color = Hex("#6C63FF"),
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.3
                };
            }
            else
            {
                headerBorder.Background = Brush("#0D1022");
                headerBorder.BorderBrush = Brush("#1A1F38");
            }

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = days[i],
                Foreground = isToday ? Brush("#9B8EE8") : Brush("#3D4468"),
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = day.Day.ToString(),
                Foreground = isToday ? Brush("#E8E4FF") : Brush("#5A6080"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            headerBorder.Child = sp;
            WeekHeader.Children.Add(headerBorder);

            // ── Колонка із заняттями ─────────────────────────────
            var col = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            var dayItems = _items.Where(x => x.StartDatetime.Date == day.Date).ToList();

            if (!dayItems.Any())
            {
                col.Children.Add(new Border
                {
                    Background = Brush("#0A0D1A"),
                    BorderBrush = Brush("#141828"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 20, 8, 20),
                    Child = new TextBlock
                    {
                        Text = "· · ·",
                        Foreground = Brush("#1E2340"),
                        FontSize = 12,
                        FontFamily = new FontFamily("Segoe UI"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                });
            }
            else
            {
                foreach (var item in dayItems)
                    col.Children.Add(BuildWeekCard(item));
            }

            WeekGrid.Children.Add(col);
        }
    }

    private Border BuildWeekCard(ScheduleItem item)
    {
        var accent = Hex(item.AccentColorHex);
        var typeFgHex = item.IsIndividual ? "#60A5FA" : "#C084FC";
        var typeIcon = item.IsIndividual ? "👤" : "👥";
        var typeLabel = item.IsIndividual ? "Індив." : "Групове";

        // Фон картки — трохи світліший ніж раніше, щоб виділявся
        var (cardBg1, cardBg2, borderHex, glowColor) = item.Status switch
        {
            "Completed" => ("#0D1E17", "#091410", "#1A4030", "#34D399"),
            "Cancelled" => ("#1E0D0D", "#140909", "#401A1A", "#F87171"),
            _ => ("#14183A", "#0E1028", "#2A2F5A", "#6C63FF")
        };

        var cardGradient = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0.5, 1)
        };
        cardGradient.GradientStops.Add(new GradientStop(Hex(cardBg1), 0));
        cardGradient.GradientStops.Add(new GradientStop(Hex(cardBg2), 1));

        var card = new Border
        {
            Background = cardGradient,
            BorderBrush = Brush(borderHex),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 0, 7),
            Cursor = Cursors.Hand,
            Tag = item.Id,
            Effect = new DropShadowEffect
            {
                Color = Hex(glowColor),
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.18
            }
        };

        var outer = new Grid();

        // Ліва акцентна смужка
        var leftBar = new Border
        {
            Width = 3,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(12, 0, 0, 12),
            Background = new SolidColorBrush(accent),
            Effect = new DropShadowEffect
            {
                Color = accent,
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };
        outer.Children.Add(leftBar);

        var content = new StackPanel { Margin = new Thickness(12, 10, 10, 10) };

        // Час — яскравіший
        content.Children.Add(new TextBlock
        {
            Text = $"{item.StartTime} – {item.EndTime}",
            Foreground = new SolidColorBrush(
                Color.FromArgb(200, accent.R, accent.G, accent.B)),
            FontSize = 10,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Назва — білий, великий
        content.Children.Add(new TextBlock
        {
            Text = item.ClassName,
            Foreground = Brush("#EEEAFF"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 7)
        });

        // Тип бейдж — чіткіший
        var typeBgColor = item.IsIndividual
            ? Color.FromArgb(40, 96, 165, 250)
            : Color.FromArgb(40, 192, 132, 252);
        var badge = new Border
        {
            Background = new SolidColorBrush(typeBgColor),
            BorderBrush = Brush(item.IsIndividual ? "#1E4070" : "#3A1A60"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 7)
        };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal };
        badgeRow.Children.Add(new TextBlock
        {
            Text = typeIcon,
            FontSize = 9,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        badgeRow.Children.Add(new TextBlock
        {
            Text = typeLabel,
            Foreground = Brush(typeFgHex),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center
        });
        badge.Child = badgeRow;
        content.Children.Add(badge);

        // Тренер — видимий
        content.Children.Add(new TextBlock
        {
            Text = item.TrainerName,
            Foreground = Brush("#6068A0"),
            FontSize = 10,
            FontFamily = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(item.Room) ? 8 : 4)
        });

        // Зал
        if (!string.IsNullOrEmpty(item.Room))
        {
            var roomRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            roomRow.Children.Add(new TextBlock { Text = "📍", FontSize = 9, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
            roomRow.Children.Add(new TextBlock { Text = item.Room, Foreground = Brush("#4A5080"), FontSize = 10, FontFamily = new FontFamily("Segoe UI"), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(roomRow);
        }

        // Нижній рядок: лічильник
        var bottomRow = new Grid();
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var peopleBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3)
        };
        peopleBadge.Child = new TextBlock
        {
            Text = $"👥 {item.BookingsCount}",
            Foreground = new SolidColorBrush(
                Color.FromArgb(220, accent.R, accent.G, accent.B)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10
        };
        Grid.SetColumn(peopleBadge, 1);
        bottomRow.Children.Add(peopleBadge);
        content.Children.Add(bottomRow);

        outer.Children.Add(content);
        card.Child = outer;

        card.MouseLeftButtonUp += (s, _) =>
        {
            if (s is Border b && b.Tag is int id)
                OpenDetail(id);
        };

        return card;
    }

    // ── Решта методів (без змін) ──────────────────────────────────
    private void OpenDetail(int id)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item == null) return;
        var dialog = new ScheduleDetailDialog(id, item);
        if (dialog.ShowDialog() == true)
            _ = LoadSchedule();
    }

    private void ScheduleItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is ScheduleItem item)
            OpenDetail(item.Id);
    }

    private async void EditSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item == null) return;
            var dialog = new EditScheduleDialog(id, item);
            if (dialog.ShowDialog() == true)
                await LoadSchedule();
        }
    }

    private async void DeleteSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            if (MessageBox.Show("Видалити заняття?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    await ApiClient.DeleteAsync($"api/schedules/{id}");
                    await LoadSchedule();
                }
                catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
            }
        }
    }

    private async void AddSchedule_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EditScheduleDialog(null, null);
        if (dialog.ShowDialog() == true)
            await LoadSchedule();
    }

    private async void Prev_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _weekView ? _currentDate.AddDays(-7) : _currentDate.AddDays(-1);
        await LoadSchedule();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _weekView ? _currentDate.AddDays(7) : _currentDate.AddDays(1);
        await LoadSchedule();
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        await LoadSchedule();
    }

    private async void DayView_Click(object sender, RoutedEventArgs e)
    {
        _weekView = false;
        DayPanel.Visibility = Visibility.Visible;
        WeekPanel.Visibility = Visibility.Collapsed;
        SetViewBtn(DayViewBtn, true);
        SetViewBtn(WeekViewBtn, false);
        await LoadSchedule();
    }

    private async void WeekView_Click(object sender, RoutedEventArgs e)
    {
        _weekView = true;
        DayPanel.Visibility = Visibility.Collapsed;
        WeekPanel.Visibility = Visibility.Visible;
        SetViewBtn(DayViewBtn, false);
        SetViewBtn(WeekViewBtn, true);
        await LoadSchedule();
    }

    private void SetViewBtn(Button btn, bool active)
    {
        btn.Style = active
            ? (Style)FindResource("ViewToggleActive")
            : (Style)FindResource("ViewToggleIdle");
    }

    private static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}

public class ScheduleItem
{
    public int Id { get; set; }
    public string ClassName { get; set; } = "";
    public string TrainerName { get; set; } = "";
    public DateTime StartDatetime { get; set; }
    public DateTime EndDatetime { get; set; }
    public string? Room { get; set; }
    public string Status { get; set; } = "";
    public int BookingsCount { get; set; }
    public int MaxCapacity { get; set; }
    public int TrainerId { get; set; }
    public int ClassTypeId { get; set; }
    public bool IsIndividual { get; set; }
    public string? Notes { get; set; }

    // Бекенд повертає timestamptz → JSON з суфіксом Z → Kind=Utc на клієнті
    // ToLocalTime() конвертує UTC→local для відображення
    public string StartTime => StartDatetime.ToLocalTime().ToString("HH:mm");
    public string EndTime => EndDatetime.ToLocalTime().ToString("HH:mm");
    public string Duration
    {
        get
        {
            var d = EndDatetime - StartDatetime;
            return $"{(int)d.TotalMinutes} хв";
        }
    }
    public string CapacityText => MaxCapacity > 0 ? $"/ {MaxCapacity}" : "";

    public string AccentColorHex => Status switch
    {
        "Completed" => "#34D399",
        "Cancelled" => "#F87171",
        _ => "#6C63FF"
    };
    public string StatusUa => Status switch
    {
        "Scheduled" => "Заплановано",
        "Completed" => "Завершено",
        "Cancelled" => "Скасовано",
        _ => Status
    };

    public string ClassTypeLabel => IsIndividual ? "Індивідуальне" : "Групове";
    public string ClassTypeIcon => IsIndividual ? "👤" : "👥";
    public Color ClassTypeBadgeBg => (Color)ColorConverter.ConvertFromString(
                                           IsIndividual ? "#0C1828" : "#120C28");
    public Color ClassTypeBadgeFg => (Color)ColorConverter.ConvertFromString(
                                           IsIndividual ? "#60A5FA" : "#C084FC");

    public Color AccentColor => (Color)ColorConverter.ConvertFromString(AccentColorHex);
    public Color CardBg => (Color)ColorConverter.ConvertFromString(
        Status == "Cancelled" ? "#0F0909" : Status == "Completed" ? "#090F0C" : "#0A0D1A");
    public Color CardBorder => (Color)ColorConverter.ConvertFromString(
        Status == "Cancelled" ? "#2A0D0D" : Status == "Completed" ? "#0D2A1A" : "#141828");
    public Color StatusBg => (Color)ColorConverter.ConvertFromString(
        Status == "Cancelled" ? "#2A0D0D" : Status == "Completed" ? "#0D2A1A" : "#10122A");
    public Color StatusFg => (Color)ColorConverter.ConvertFromString(
        Status == "Cancelled" ? "#F87171" : Status == "Completed" ? "#34D399" : "#7B6FD0");

    public string EditVisible => (AppSession.IsAdmin || AppSession.IsManager || AppSession.IsTrainer)
        ? "Visible" : "Collapsed";

    public double FillWidth
    {
        get
        {
            if (MaxCapacity <= 0) return 0;
            return Math.Min(80.0 * BookingsCount / MaxCapacity, 80.0);
        }
    }
}