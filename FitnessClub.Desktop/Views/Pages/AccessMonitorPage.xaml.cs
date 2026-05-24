using AForge.Video;
using AForge.Video.DirectShow;
using FitnessClub.Desktop.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZXing;
using ZXing.Windows.Compatibility;

namespace FitnessClub.Desktop.Views.Pages;

public partial class AccessMonitorPage : Page
{
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _autoResetTimer;
    private FilterInfoCollection? _cameras;
    private VideoCaptureDevice? _camera;
    private volatile bool _isProcessing = false;
    private bool _cameraActive = false;

    // Throttle: оновлюємо картинку не частіше ніж раз на 50мс (~20fps)
    private DateTime _lastFrameRender = DateTime.MinValue;
    private const int FrameRenderIntervalMs = 50;

    public AccessMonitorPage()
    {
        InitializeComponent();

        // Refresh timer
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refreshTimer.Tick += async (_, _) => await LoadStats();

        // Auto reset timer — один екземпляр, перевикористовується
        _autoResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autoResetTimer.Tick += (_, _) =>
        {
            _autoResetTimer.Stop();
            ResetToIdle();
        };

        Loaded += async (_, _) =>
        {
            await LoadTodayCount();
            StartCamera();
        };

        Unloaded += (_, _) =>
        {
            _refreshTimer.Stop();
            _autoResetTimer.Stop();
            StopCameraAsync(); // не блокуємо UI thread
        };
    }

    // ══════════════════════════════════════════════════════════
    //  ВКЛАДКИ
    // ══════════════════════════════════════════════════════════
    private void TabScan_Click(object sender, RoutedEventArgs e)
    {
        ScanTab.Visibility = Visibility.Visible;
        StatsTab.Visibility = Visibility.Collapsed;
        SetTabActive(TabScanBtn, true);
        SetTabActive(TabStatsBtn, false);
        if (!_cameraActive) StartCamera();
    }

    private async void TabStats_Click(object sender, RoutedEventArgs e)
    {
        ScanTab.Visibility = Visibility.Collapsed;
        StatsTab.Visibility = Visibility.Visible;
        SetTabActive(TabScanBtn, false);
        SetTabActive(TabStatsBtn, true);
        _refreshTimer.Start();
        await LoadStats();
    }

    private void SetTabActive(Button btn, bool active)
    {
        btn.Opacity = active ? 1.0 : 0.5;
    }

    // ══════════════════════════════════════════════════════════
    //  КНОПКА — СКАНУВАТИ ЗНОВУ
    // ══════════════════════════════════════════════════════════
    private void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        ResetToIdle();
    }

    // ══════════════════════════════════════════════════════════
    //  КАМЕРА
    // ══════════════════════════════════════════════════════════
    private void StartCamera()
    {
        try
        {
            _cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (_cameras.Count == 0)
            {
                ScanStatusText.Text = "Камеру не знайдено";
                ScanIndicator.Fill = new SolidColorBrush(Colors.Red);
                return;
            }

            _camera = new VideoCaptureDevice(_cameras[0].MonikerString);
            _camera.NewFrame += Camera_NewFrame;
            _camera.Start();
            _cameraActive = true;
        }
        catch (Exception ex)
        {
            ScanStatusText.Text = $"Помилка камери: {ex.Message}";
            ScanIndicator.Fill = new SolidColorBrush(Colors.Red);
        }
    }

    // ВИПРАВЛЕННЯ 1: не блокуємо UI thread через WaitForStop()
    private void StopCameraAsync()
    {
        if (_camera == null) return;

        _camera.NewFrame -= Camera_NewFrame;

        if (_camera.IsRunning)
            _camera.SignalToStop(); // тільки сигнал, без очікування

        _camera = null;
        _cameraActive = false;
    }

    private void Camera_NewFrame(object sender, NewFrameEventArgs e)
    {
        if (_isProcessing) return;

        var frame = (Bitmap)e.Frame.Clone();

        // ВИПРАВЛЕННЯ 2: оновлюємо UI не частіше ніж 20fps через BeginInvoke (не блокуємо!)
        var now = DateTime.UtcNow;
        if ((now - _lastFrameRender).TotalMilliseconds >= FrameRenderIntervalMs)
        {
            _lastFrameRender = now;

            BitmapImage? imgSource = null;
            try { imgSource = BitmapToImageSource(frame); }
            catch { }

            if (imgSource != null)
            {
                // BeginInvoke — асинхронний, не блокує camera thread
                Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
                {
                    CameraImage.Source = imgSource;
                });
            }
        }

        // ВИПРАВЛЕННЯ 3: декодуємо QR на camera thread, не вантажимо UI
        var reader = new BarcodeReader
        {
            AutoRotate = true,
            TryInverted = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[] { BarcodeFormat.QR_CODE }
            }
        };

        var result = reader.Decode(frame);
        frame.Dispose();

        if (result == null) return;

        var token = ExtractToken(result.Text);
        if (token == null) return;

        _isProcessing = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            ScanStatusText.Text = "QR знайдено, перевірка...";
            ScanIndicator.Fill = new SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 159, 10));
        });

        _ = Task.Run(async () => await ProcessQr(token));
    }

    private static string? ExtractToken(string qrText)
    {
        if (string.IsNullOrWhiteSpace(qrText))
            return null;

        if (qrText.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(qrText);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var t = query["token"];
                return string.IsNullOrEmpty(t) ? null : t;
            }
            catch
            {
                var idx = qrText.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var raw = qrText[(idx + 6)..];
                    var end = raw.IndexOf('&');
                    return end >= 0 ? raw[..end] : raw;
                }
            }
        }

        if (qrText.StartsWith("GYMCLUB-", StringComparison.OrdinalIgnoreCase))
            return qrText["GYMCLUB-".Length..];

        return null;
    }

    private async Task ProcessQr(string token)
    {
        try
        {
            var result = await ApiClient.PostAsync<ScanResultDto>(
                "api/access/scan",
                new { QrCode = token });

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                if (result != null) ShowResult(result);
                _ = LoadTodayCount();
                ScheduleAutoReset();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                ShowDenied("Помилка", "?", $"Помилка: {ex.Message}");
                ScheduleAutoReset();
            });
        }
    }

    // ВИПРАВЛЕННЯ 4: один таймер, перезапускаємо замість створення нового
    private void ScheduleAutoReset()
    {
        _autoResetTimer.Stop();
        _autoResetTimer.Start();
    }

    private static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze(); // обов'язково — дозволяє використовувати між потоками
        return img;
    }

    // ══════════════════════════════════════════════════════════
    //  ВІДОБРАЖЕННЯ РЕЗУЛЬТАТУ
    // ══════════════════════════════════════════════════════════
    private void ShowResult(ScanResultDto result)
    {
        if (result.Allowed)
            ShowAllowed(result);
        else
            ShowDenied(result.ClientName, MakeInitials(result.ClientName), result.Message);
    }

    private void ShowAllowed(ScanResultDto result)
    {
        IdlePanel.Visibility = Visibility.Collapsed;
        DeniedPanel.Visibility = Visibility.Collapsed;
        AllowedPanel.Visibility = Visibility.Visible;

        AllowedInitials.Text = MakeInitials(result.ClientName);
        AllowedName.Text = result.ClientName;
        AllowedMembership.Text = result.MembershipType;
        AllowedExpiry.Text = result.MembershipExpiry?.ToString("dd.MM.yyyy") ?? "";
        AllowedDaysLeft.Text = $"{result.DaysLeft} дн.";

        AllowedSubMsg.Text = result.AlreadyVisitedToday
            ? "Повторний вхід сьогодні — вже відсканований"
            : "Вхід зафіксовано";

        AllowedSubMsg.Foreground = result.AlreadyVisitedToday
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 159, 10))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 100));

        AllowedDaysLeft.Foreground = result.DaysLeft <= 3
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 69, 58))
            : result.DaysLeft <= 7
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 159, 10))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 245, 255));

        ScanStatusText.Text = $"{result.ClientName} — відсканований";
        ScanIndicator.Fill = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0, 255, 136));
    }

    private void ShowDenied(string clientName, string initials, string message)
    {
        IdlePanel.Visibility = Visibility.Collapsed;
        AllowedPanel.Visibility = Visibility.Collapsed;
        DeniedPanel.Visibility = Visibility.Visible;

        DeniedInitials.Text = string.IsNullOrEmpty(initials) ? "?" : initials;
        DeniedName.Text = clientName;
        DeniedMessage.Text = message;

        ScanStatusText.Text = "Доступ заборонено";
        ScanIndicator.Fill = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(255, 69, 58));
    }

    private void ResetToIdle()
    {
        _isProcessing = false;
        IdlePanel.Visibility = Visibility.Visible;
        AllowedPanel.Visibility = Visibility.Collapsed;
        DeniedPanel.Visibility = Visibility.Collapsed;
        ScanStatusText.Text = "Очікування QR-коду...";
        ScanIndicator.Fill = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0, 255, 136));
    }

    // ══════════════════════════════════════════════════════════
    //  ДАНІ
    // ══════════════════════════════════════════════════════════
    private async Task LoadTodayCount()
    {
        try
        {
            var data = await ApiClient.GetAsync<TodayDto>("api/access/today");
            TodayCountText.Text = data?.Count.ToString() ?? "0";
        }
        catch { }
    }

    private async Task LoadStats()
    {
        try
        {
            var stats = await ApiClient.GetAsync<AccessStatsDto>("api/access/stats");
            if (stats == null) return;

            TodayCountText.Text = stats.TodayCount.ToString();
            NowInClubText.Text = stats.NowInClub.ToString();
            WeekCountText.Text = stats.WeekCount.ToString();
            MonthCountText.Text = stats.MonthCount.ToString();
            LastUpdateText.Text = $"оновлено {DateTime.Now:HH:mm:ss}";

            RecentVisitsList.ItemsSource = stats.RecentVisits
                .Take(20)
                .Select(v => new
                {
                    Initials = MakeInitials(v.ClientName),
                    ClientName = v.ClientName,
                    MembershipType = v.MembershipType,
                    TimeStr = v.Time.ToLocalTime().ToString("HH:mm")
                })
                .ToList();

            BuildHourlyChart(stats.HourlyBreakdown);
        }
        catch (Exception ex)
        {
            LastUpdateText.Text = $"Помилка: {ex.Message}";
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ГРАФІК
    // ══════════════════════════════════════════════════════════
    private void BuildHourlyChart(List<HourlyCountDto> data)
    {
        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(156, 163, 175),
            PlotAreaBorderColor = OxyColor.FromArgb(30, 100, 120, 180),
            PlotAreaBorderThickness = new OxyThickness(1),
        };

        model.Axes.Add(new CategoryAxis
        {
            Position = AxisPosition.Left,
            ItemsSource = data.Select(d => $"{d.Hour:D2}:00").ToArray(),
            TextColor = OxyColor.FromRgb(100, 110, 130),
            TicklineColor = OxyColors.Transparent,
            FontSize = 9,
            GapWidth = 0.2,
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            TextColor = OxyColor.FromRgb(100, 110, 130),
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromArgb(20, 100, 150, 200),
            TicklineColor = OxyColors.Transparent,
            FontSize = 10,
            StringFormat = "0",
            MinimumMajorStep = 1,
        });

        var series = new BarSeries
        {
            FillColor = OxyColor.FromRgb(99, 102, 241),
            StrokeThickness = 0,
            BarWidth = 0.7,
        };

        var currentHour = DateTime.Now.Hour;
        foreach (var item in data)
        {
            series.Items.Add(new BarItem(item.Count)
            {
                Color = item.Hour == currentHour
                    ? OxyColor.FromRgb(0, 245, 255)
                    : OxyColor.FromRgb(99, 102, 241)
            });
        }

        model.Series.Add(series);
        HourlyChart.Model = model;
    }

    private static string MakeInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
            : name[0].ToString().ToUpper();
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────
public class ScanResultDto
{
    public bool Allowed { get; set; }
    public string ClientName { get; set; } = "";
    public string MembershipType { get; set; } = "";
    public DateTime? MembershipExpiry { get; set; }
    public int DaysLeft { get; set; }
    public bool AlreadyVisitedToday { get; set; }
    public string Message { get; set; } = "";
}

public class TodayDto
{
    public int Count { get; set; }
    public List<VisitEntryDto> Visits { get; set; } = new();
}

public class VisitEntryDto
{
    public string ClientName { get; set; } = "";
    public DateTime Time { get; set; }
    public string EntryMethod { get; set; } = "";
}

public class AccessStatsDto
{
    public int TodayCount { get; set; }
    public int WeekCount { get; set; }
    public int MonthCount { get; set; }
    public int NowInClub { get; set; }
    public List<RecentVisitDto> RecentVisits { get; set; } = new();
    public List<HourlyCountDto> HourlyBreakdown { get; set; } = new();
}

public class RecentVisitDto
{
    public string ClientName { get; set; } = "";
    public string MembershipType { get; set; } = "";
    public DateTime Time { get; set; }
}

public class HourlyCountDto
{
    public int Hour { get; set; }
    public int Count { get; set; }
}