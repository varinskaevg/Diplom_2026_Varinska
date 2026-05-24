using FitnessClub.Desktop.Models;
using FitnessClub.Desktop.Services;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FitnessClub.Desktop.Views.Pages;

public partial class DashboardPage : Page
{
    private DispatcherTimer _clockTimer = new();

    private static readonly Color Accent = Color.FromRgb(0x00, 0xE5, 0xA0);
    private static readonly Color AccentDim = Color.FromRgb(0x00, 0xC8, 0x80);
    private static readonly Color GridLine = Color.FromRgb(0x0C, 0x14, 0x22);

    public DashboardPage()
    {
        InitializeComponent();
        GreetingText.Text = $"Ласкаво просимо, {AppSession.FullName}";

        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        Loaded += async (_, _) => await LoadDashboard();
    }

    private void UpdateClock()
        => CurrentTimeText.Text = DateTime.Now.ToString("HH:mm  dd.MM.yyyy");

    private async Task LoadDashboard()
    {
        try
        {
            var stats = await ApiClient.GetAsync<DashboardStats>("api/dashboard");
            if (stats == null) return;

            TotalClientsText.Text = stats.TotalClients.ToString();
            ActiveMembersText.Text = stats.ActiveMembers.ToString();
            RevenueText.Text = $"{stats.MonthRevenue:N0} ₴";
            TodayVisitsText.Text = stats.TodayVisits.ToString();
            TotalTrainersText.Text = stats.TotalTrainers.ToString();
            TodaySchedulesText.Text = stats.TodaySchedules.ToString();
            ExpiringSoonText.Text = stats.ExpiringSoon.ToString();
            RecentPaymentsList.ItemsSource = stats.RecentPayments;

            Dispatcher.InvokeAsync(() =>
            {
                DrawRevenueChart(stats);
                DrawVisitsBar(stats);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            await LoadExpiringMemberships();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження: {ex.Message}");
        }
    }

    private void DrawRevenueChart(DashboardStats stats)
    {
        var canvas = RevenueChartCanvas;
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 10 || h < 10) { w = 400; h = 160; }

        double[] values = GetMonthlyRevenue(stats);
        string[] months = GetMonthLabels();

        var labels = new[] { MonthLabel0, MonthLabel1, MonthLabel2,
                              MonthLabel3, MonthLabel4, MonthLabel5 };
        for (int i = 0; i < 6; i++)
            labels[i].Text = months[i];

        double max = values.Max() > 0 ? values.Max() : 1;
        RevenueChartTotal.Text = $"{values.Sum():N0} ₴";

        int count = values.Length;
        double padL = 4, padR = 4, padT = 8, padB = 4;
        double stepX = (w - padL - padR) / Math.Max(count - 1, 1);

        var points = new PointCollection();
        for (int i = 0; i < count; i++)
        {
            double x = padL + i * stepX;
            double y = padT + (1 - values[i] / max) * (h - padT - padB);
            points.Add(new Point(x, y));
        }

        var areaPoints = new PointCollection(points);
        areaPoints.Add(new Point(padL + (count - 1) * stepX, h));
        areaPoints.Add(new Point(padL, h));

        var area = new Polygon
        {
            Points = areaPoints,
            Fill = new LinearGradientBrush(
                Color.FromArgb(40, 0, 229, 160),
                Color.FromArgb(0, 0, 229, 160),
                new Point(0, 0), new Point(0, 1)),
            StrokeThickness = 0
        };

        var line = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Accent),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        line.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Accent,
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.6
        };

        while (canvas.Children.Count > 5)
            canvas.Children.RemoveAt(canvas.Children.Count - 1);

        canvas.Children.Add(area);
        canvas.Children.Add(line);

        for (int i = 0; i < count; i++)
        {
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(Accent),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Accent,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.9
                }
            };
            Canvas.SetLeft(dot, points[i].X - 3.5);
            Canvas.SetTop(dot, points[i].Y - 3.5);
            canvas.Children.Add(dot);
        }
    }

    private void DrawVisitsBar(DashboardStats stats)
    {
        var canvas = VisitsBarCanvas;
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 10 || h < 10) { w = 400; h = 160; }

        int[] visits = GetWeekVisits(stats);
        int max = visits.Max() > 0 ? visits.Max() : 1;

        int today = (int)DateTime.Today.DayOfWeek;
        int todayIdx = today == 0 ? 6 : today - 1;

        double totalW = w;
        double barW = (totalW / 7) * 0.55;
        double gap = totalW / 7;
        double padB = 4;

        while (canvas.Children.Count > 5)
            canvas.Children.RemoveAt(canvas.Children.Count - 1);

        for (int i = 0; i < 7; i++)
        {
            double barH = visits[i] > 0 ? ((double)visits[i] / max) * (h - padB - 10) : 2;
            double x = gap * i + (gap - barW) / 2;
            double y = h - padB - barH;

            bool isToday = (i == todayIdx);
            var clr = isToday ? Accent : Color.FromRgb(0x0E, 0x1C, 0x2C);

            var bar = new Rectangle
            {
                Width = barW,
                Height = Math.Max(barH, 2),
                Fill = new SolidColorBrush(clr),
                RadiusX = 4,
                RadiusY = 4
            };

            if (isToday)
                bar.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Accent,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            canvas.Children.Add(bar);
        }
    }

    // ── Генеруємо дані для графіків на основі доступних полів ──

    private double[] GetMonthlyRevenue(DashboardStats stats)
    {
        // API не повертає помісячну розбивку — симулюємо тренд на основі поточного місяця
        double current = (double)stats.MonthRevenue;
        var rnd = new Random(DateTime.Today.Month);
        return new double[]
        {
            current * (0.50 + rnd.NextDouble() * 0.20),
            current * (0.60 + rnd.NextDouble() * 0.20),
            current * (0.55 + rnd.NextDouble() * 0.20),
            current * (0.70 + rnd.NextDouble() * 0.20),
            current * (0.80 + rnd.NextDouble() * 0.15),
            current
        };
    }

    private int[] GetWeekVisits(DashboardStats stats)
    {
        // API не повертає тижневу розбивку — симулюємо на основі сьогоднішніх відвідувань
        int todayVisits = stats.TodayVisits;
        var rnd = new Random(DateTime.Today.DayOfYear);
        int[] result = new int[7];
        int todayIdx = (int)DateTime.Today.DayOfWeek;
        todayIdx = todayIdx == 0 ? 6 : todayIdx - 1;

        for (int i = 0; i < 7; i++)
        {
            if (i == todayIdx)
                result[i] = todayVisits;
            else if (i > todayIdx)
                result[i] = 0; // майбутні дні
            else
                result[i] = (int)(todayVisits * (0.6 + rnd.NextDouble() * 0.6));
        }
        return result;
    }

    private string[] GetMonthLabels()
    {
        var now = DateTime.Now;
        var labels = new string[6];
        var ua = new[] { "Січ","Лют","Бер","Кві","Тра","Чер",
                         "Лип","Сер","Вер","Жов","Лис","Гру" };
        for (int i = 5; i >= 0; i--)
        {
            var m = now.AddMonths(-i);
            labels[5 - i] = ua[m.Month - 1];
        }
        return labels;
    }

    private async Task LoadExpiringMemberships()
    {
        try
        {
            var clients = await ApiClient.GetAsync<List<ClientItem>>("api/clients");
            if (clients == null) return;

            var today = DateTime.Today;
            var in7Days = today.AddDays(7);

            var expiring = clients
                .Where(c => c.ActiveMembership != null)
                .Select(c =>
                {
                    var m = (JsonElement)c.ActiveMembership!;
                    var endDate = m.GetProperty("endDate").GetDateTime();
                    return new ExpiringItem
                    {
                        ClientName = c.FullName,
                        MembershipType = m.GetProperty("name").GetString() ?? "",
                        EndDate = endDate.ToString("dd.MM.yyyy"),
                        DaysLeft = $"{(endDate.Date - today).Days} дн."
                    };
                })
                .Where(x =>
                {
                    if (DateTime.TryParseExact(x.EndDate, "dd.MM.yyyy",
                        null, System.Globalization.DateTimeStyles.None, out var d))
                        return d >= today && d <= in7Days;
                    return false;
                })
                .OrderBy(x => x.EndDate)
                .ToList();

            ExpiringMembershipsList.ItemsSource = expiring;
            ExpiringCountBadge.Text = $"{expiring.Count} клієнтів";
        }
        catch { }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await LoadDashboard();

    private void AddClient_Click(object sender, RoutedEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.NavigateToPage("Clients");

    private void SellMembership_Click(object sender, RoutedEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.NavigateToPage("Memberships");

    private void CheckIn_Click(object sender, RoutedEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.NavigateToPage("Clients");

    private void ViewSchedule_Click(object sender, RoutedEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.NavigateToPage("Schedule");

    private void ViewAllPayments_Click(object sender, MouseButtonEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.NavigateToPage("Payments");
}

public class ExpiringItem
{
    public string ClientName { get; set; } = "";
    public string MembershipType { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string DaysLeft { get; set; } = "";
}