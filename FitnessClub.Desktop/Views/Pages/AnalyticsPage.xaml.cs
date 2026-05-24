using FitnessClub.Desktop.Services;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using OxyAreaSeries = OxyPlot.Series.AreaSeries;
using OxyBarSeries = OxyPlot.Series.BarSeries;
using OxyBarItem = OxyPlot.Series.BarItem;
using OxyPieSeries = OxyPlot.Series.PieSeries;
using OxyPieSlice = OxyPlot.Series.PieSlice;
using OxyLineSeries = OxyPlot.Series.LineSeries;
using OxyDataPoint = OxyPlot.DataPoint;

namespace FitnessClub.Desktop.Views.Pages;

public partial class AnalyticsPage : Page
{
    private JsonElement _data;
    private bool _hasData = false;

    // ──────────────────────────────────────────
    //  Period helpers
    // ──────────────────────────────────────────
    private (DateTime from, DateTime to) GetRevPeriod() => ResolvePeriod(RevPeriodWeek, RevPeriodMonth, RevPeriodQ, RevPeriodYear, RevPeriodAll, RevDateFrom.SelectedDate, RevDateTo.SelectedDate);
    private (DateTime from, DateTime to) GetCliPeriod() => ResolvePeriod(CliPeriodWeek, CliPeriodMonth, CliPeriodQ, CliPeriodYear, null, CliDateFrom.SelectedDate, CliDateTo.SelectedDate);
    private (DateTime from, DateTime to) GetSesPeriod() => ResolvePeriod(SesPeriodWeek, SesPeriodMonth, SesPeriodQ, SesPeriodYear, null, SesDateFrom.SelectedDate, SesDateTo.SelectedDate);
    private (DateTime from, DateTime to) GetMemPeriod() => ResolvePeriod(MemPeriodWeek, MemPeriodMonth, MemPeriodQ, MemPeriodYear, null, MemDateFrom.SelectedDate, MemDateTo.SelectedDate);
    private (DateTime from, DateTime to) GetLdPeriod() => ResolvePeriod(LdPeriodWeek, LdPeriodMonth, LdPeriodQ, LdPeriodYear, null, LdDateFrom.SelectedDate, LdDateTo.SelectedDate);
    private (DateTime from, DateTime to) GetCnlPeriod() => ResolvePeriod(CnlPeriodWeek, CnlPeriodMonth, CnlPeriodQ, CnlPeriodYear, null, CnlDateFrom.SelectedDate, CnlDateTo.SelectedDate);

    private static (DateTime from, DateTime to) ResolvePeriod(
        RadioButton week, RadioButton month, RadioButton q, RadioButton year,
        RadioButton? all, DateTime? customFrom, DateTime? customTo)
    {
        var now = DateTime.Now;
        if (week.IsChecked == true) return (now.AddDays(-(int)now.DayOfWeek + 1), now);
        if (month.IsChecked == true) return (new DateTime(now.Year, now.Month, 1), now);
        if (q.IsChecked == true) return (new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1), now);
        if (year.IsChecked == true) return (new DateTime(now.Year, 1, 1), now);
        if (all?.IsChecked == true) return (new DateTime(2000, 1, 1), now);
        if (customFrom.HasValue && customTo.HasValue) return (customFrom.Value, customTo.Value);
        return (new DateTime(now.Year, now.Month, 1), now);
    }

    private static string FmtPeriod(DateTime f, DateTime t)
        => $"{f:dd.MM.yyyy} — {t:dd.MM.yyyy}";

    // ──────────────────────────────────────────
    //  Init
    // ──────────────────────────────────────────
    public AnalyticsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAnalytics();
    }

    private async Task LoadAnalytics()
    {
        try
        {
            var result = await ApiClient.GetAsync<JsonElement>("api/analytics");
            _data = result;
            _hasData = true;

            // Tab 1 — always
            UpdateKpi();
            BuildRevenueChart();
            BuildPaymentMethodsChart();
            BuildClientsChart();
            BuildVisitsMonthChart();
            BuildDowChart();
            BuildMembershipPieChart();
            BuildTopLists();
            BuildPayrollTable();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження аналітики: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────
    //  Tab switching
    // ──────────────────────────────────────────
    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        PanelOverview.Visibility = Visibility.Collapsed;
        PanelRevenue.Visibility = Visibility.Collapsed;
        PanelClients.Visibility = Visibility.Collapsed;
        PanelAllTime.Visibility = Visibility.Collapsed;
        PanelSessions.Visibility = Visibility.Collapsed;
        PanelMemberships.Visibility = Visibility.Collapsed;
        PanelAvgLoad.Visibility = Visibility.Collapsed;
        PanelCancels.Visibility = Visibility.Collapsed;

        if (TabOverview.IsChecked == true) { PanelOverview.Visibility = Visibility.Visible; }
        else if (TabRevenue.IsChecked == true) { PanelRevenue.Visibility = Visibility.Visible; BuildRevenueTab(); }
        else if (TabClients.IsChecked == true) { PanelClients.Visibility = Visibility.Visible; BuildClientsTab(); }
        else if (TabAllTime.IsChecked == true) { PanelAllTime.Visibility = Visibility.Visible; BuildAllTimeTab(); }
        else if (TabSessions.IsChecked == true) { PanelSessions.Visibility = Visibility.Visible; BuildSessionsTab(); }
        else if (TabMemberships.IsChecked == true) { PanelMemberships.Visibility = Visibility.Visible; BuildMembershipsTab(); }
        else if (TabAvgLoad.IsChecked == true) { PanelAvgLoad.Visibility = Visibility.Visible; BuildAvgLoadTab(); }
        else if (TabCancels.IsChecked == true) { PanelCancels.Visibility = Visibility.Visible; BuildCancelsTab(); }
    }

    // Period-change handlers
    private void RevPeriod_Checked(object s, RoutedEventArgs e) { if (_hasData) BuildRevenueTab(); }
    private void CliPeriod_Checked(object s, RoutedEventArgs e) { if (_hasData) BuildClientsTab(); }
    private void SesPeriod_Checked(object s, RoutedEventArgs e) { if (_hasData) BuildSessionsTab(); }
    private void MemPeriod_Checked(object s, RoutedEventArgs e) { if (_hasData) BuildMembershipsTab(); }
    private void LdPeriod_Checked(object s, RoutedEventArgs e) { if (_hasData) BuildAvgLoadTab(); }
    private void CnlPeriod_Checked(object s, RoutedEventArgs e) { if (_hasData) BuildCancelsTab(); }

    private void RevCustomDate_Changed(object s, SelectionChangedEventArgs e) { if (_hasData && RevDateFrom.SelectedDate.HasValue && RevDateTo.SelectedDate.HasValue) BuildRevenueTab(); }
    private void CliCustomDate_Changed(object s, SelectionChangedEventArgs e) { if (_hasData && CliDateFrom.SelectedDate.HasValue && CliDateTo.SelectedDate.HasValue) BuildClientsTab(); }
    private void SesCustomDate_Changed(object s, SelectionChangedEventArgs e) { if (_hasData && SesDateFrom.SelectedDate.HasValue && SesDateTo.SelectedDate.HasValue) BuildSessionsTab(); }
    private void MemCustomDate_Changed(object s, SelectionChangedEventArgs e) { if (_hasData && MemDateFrom.SelectedDate.HasValue && MemDateTo.SelectedDate.HasValue) BuildMembershipsTab(); }
    private void LdCustomDate_Changed(object s, SelectionChangedEventArgs e) { if (_hasData && LdDateFrom.SelectedDate.HasValue && LdDateTo.SelectedDate.HasValue) BuildAvgLoadTab(); }
    private void CnlCustomDate_Changed(object s, SelectionChangedEventArgs e) { if (_hasData && CnlDateFrom.SelectedDate.HasValue && CnlDateTo.SelectedDate.HasValue) BuildCancelsTab(); }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAnalytics();

    // ══════════════════════════════════════════════════════════
    //  HELPERS - ПРЕМІУМ ПАЛІТРА КОЛЬОРІВ
    // ══════════════════════════════════════════════════════════

    // Neon Cyberpunk палітра - яскраві, насичені кольори
    private readonly OxyColor[] _palette =
    [
        OxyColor.FromRgb(0, 245, 255),     // Neon Cyan (#00f5ff)
        OxyColor.FromRgb(191, 90, 242),    // Neon Purple (#bf5af2)
        OxyColor.FromRgb(0, 255, 136),     // Neon Green (#00ff88)
        OxyColor.FromRgb(255, 45, 146),    // Neon Pink (#ff2d92)
        OxyColor.FromRgb(255, 159, 10),    // Neon Orange (#ff9f0a)
        OxyColor.FromRgb(10, 132, 255),    // Neon Blue (#0a84ff)
        OxyColor.FromRgb(255, 69, 58),     // Neon Red (#ff453a)
        OxyColor.FromRgb(48, 209, 88),     // Bright Green (#30d158)
    ];

    // Градієнтні пари для Area/Line графіків
    private static readonly (OxyColor Stroke, OxyColor Fill)[] _gradientPairs =
    [
        (OxyColor.FromRgb(0, 245, 255), OxyColor.FromArgb(80, 0, 245, 255)),      // Cyan glow
        (OxyColor.FromRgb(191, 90, 242), OxyColor.FromArgb(70, 191, 90, 242)),    // Purple glow
        (OxyColor.FromRgb(0, 255, 136), OxyColor.FromArgb(75, 0, 255, 136)),      // Green glow
        (OxyColor.FromRgb(255, 45, 146), OxyColor.FromArgb(65, 255, 45, 146)),    // Pink glow
        (OxyColor.FromRgb(255, 159, 10), OxyColor.FromArgb(70, 255, 159, 10)),    // Orange glow
    ];

    private static PlotModel BaseModel() => new()
    {
        Background = OxyColors.Transparent,
        TextColor = OxyColor.FromRgb(180, 190, 210),  // Світліший текст
        PlotAreaBorderColor = OxyColor.FromArgb(40, 100, 120, 180),  // Тонка рамка з підсвіткою
        PlotAreaBorderThickness = new OxyThickness(1),
    };

    private static Legend MkLegend() => new()
    {
        LegendTextColor = OxyColor.FromRgb(180, 190, 210),
        LegendBackground = OxyColor.FromArgb(30, 20, 30, 50),  // Напівпрозорий фон
        LegendBorder = OxyColor.FromArgb(50, 0, 245, 255),     // Cyan border
        LegendBorderThickness = 1,
        LegendPosition = LegendPosition.TopRight,
        LegendPadding = 8,
        LegendMargin = 10,
    };

    private static CategoryAxis CatAxisBottom(string[] labels) => new()
    {
        Position = AxisPosition.Bottom,
        ItemsSource = labels,
        TextColor = OxyColor.FromRgb(140, 155, 180),
        TicklineColor = OxyColor.FromArgb(30, 100, 120, 180),
        MajorGridlineStyle = LineStyle.None,
        MinorGridlineStyle = LineStyle.None,
        AxislineColor = OxyColor.FromArgb(50, 0, 245, 255),  // Cyan підсвітка осі
        AxislineStyle = LineStyle.Solid,
        AxislineThickness = 1,
        FontSize = 11,
        FontWeight = OxyPlot.FontWeights.Normal,
        Angle = 0,
        GapWidth = 0.2,
    };

    private static CategoryAxis CatAxisLeft(string[] labels) => new()
    {
        Position = AxisPosition.Left,
        ItemsSource = labels,
        TextColor = OxyColor.FromRgb(140, 155, 180),
        TicklineColor = OxyColor.FromArgb(30, 100, 120, 180),
        MajorGridlineStyle = LineStyle.None,
        MinorGridlineStyle = LineStyle.None,
        AxislineColor = OxyColor.FromArgb(50, 191, 90, 242),  // Purple підсвітка осі
        AxislineStyle = LineStyle.Solid,
        AxislineThickness = 1,
        FontSize = 11,
        GapWidth = 0.15,
    };

    private static LinearAxis LinAxisLeft() => new()
    {
        Position = AxisPosition.Left,
        TextColor = OxyColor.FromRgb(140, 155, 180),
        MajorGridlineStyle = LineStyle.Dot,
        MajorGridlineColor = OxyColor.FromArgb(25, 100, 150, 200),  // Тонка сітка
        MinorGridlineStyle = LineStyle.None,
        TicklineColor = OxyColors.Transparent,
        AxislineColor = OxyColor.FromArgb(40, 191, 90, 242),
        AxislineStyle = LineStyle.Solid,
        Minimum = 0,
        FontSize = 11,
        StringFormat = "#,0",  // Форматування чисел з комами
    };

    private static LinearAxis LinAxisBottom() => new()
    {
        Position = AxisPosition.Bottom,
        TextColor = OxyColor.FromRgb(140, 155, 180),
        MajorGridlineStyle = LineStyle.Dot,
        MajorGridlineColor = OxyColor.FromArgb(25, 100, 150, 200),
        MinorGridlineStyle = LineStyle.None,
        TicklineColor = OxyColors.Transparent,
        AxislineColor = OxyColor.FromArgb(40, 0, 245, 255),
        AxislineStyle = LineStyle.Solid,
        Minimum = 0,
        FontSize = 11,
    };

    // Filter JsonElement array by date range (expects "date" field as ISO string)
    private static List<JsonElement> FilterByDate(JsonElement arr, DateTime from, DateTime to)
    {
        var list = new List<JsonElement>();
        foreach (var el in arr.EnumerateArray())
        {
            if (el.TryGetProperty("date", out var dateProp) &&
                DateTime.TryParse(dateProp.GetString(), out var d) &&
                d >= from && d <= to)
                list.Add(el);
        }
        return list;
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 1 — OVERVIEW (unchanged logic)
    // ══════════════════════════════════════════════════════════
    private void UpdateKpi()
    {
        TotalRevenueText.Text = $"{_data.GetProperty("totalRevenue").GetDecimal():N0} ₴";
        MonthRevenueText.Text = $"{_data.GetProperty("monthRevenue").GetDecimal():N0} ₴";
        AvgPaymentText.Text = $"{_data.GetProperty("avgPayment").GetDecimal():N0} ₴";
        TotalRefundsText.Text = $"{_data.GetProperty("totalRefunds").GetDecimal():N0} ₴";

        var growth = _data.GetProperty("monthGrowthPercent").GetDecimal();
        MonthGrowthText.Text = growth >= 0
            ? $"▲ +{growth}% vs минулий місяць"
            : $"▼ {growth}% vs минулий місяць";
        NetRevenueText.Text = $"Чистий дохід: {_data.GetProperty("netRevenue").GetDecimal():N0} ₴";

        var expiringSoon = _data.GetProperty("expiringSoon").GetInt32();
        NewClientsText.Text = _data.GetProperty("newClientsThisMonth").GetInt32().ToString();
        NewClientsSubText.Text = $"{_data.GetProperty("newClientsLastMonth").GetInt32()} минулого місяця";
        ActiveMembersText.Text = _data.GetProperty("activeMembers").GetInt32().ToString();
        ExpiringSoonText.Text = expiringSoon > 0 ? $"⚠ {expiringSoon} закінчується за 7 днів" : "Все добре";
        VisitsWeekText.Text = _data.GetProperty("visitsThisWeek").GetInt32().ToString();
        ExpiringSoonBigText.Text = expiringSoon.ToString();

        TotalPayrollText.Text = $"{_data.GetProperty("totalPayroll").GetDecimal():N0} ₴";
        NetAfterPayrollText.Text = $"{_data.GetProperty("netRevenueAfterPayroll").GetDecimal():N0} ₴";
        TotalBonusText.Text = $"{_data.GetProperty("totalBonus").GetDecimal():N0} ₴";
    }

    private void BuildRevenueChart()
    {
        var model = BaseModel();
        model.Title = null; // Без заголовка - він є в XAML

        var labels = _data.GetProperty("monthLabels").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var revenues = _data.GetProperty("revenueByMonth").EnumerateArray().Select(x => (double)x.GetDecimal()).ToArray();
        var refunds = _data.GetProperty("refundsByMonth").EnumerateArray().Select(x => (double)Math.Abs(x.GetDecimal())).ToArray();

        model.Axes.Add(CatAxisBottom(labels));
        model.Axes.Add(LinAxisLeft());

        // Головна Area серія з градієнтним заливанням - Neon Cyan
        var rev = new OxyAreaSeries
        {
            Color = OxyColor.FromRgb(0, 245, 255),  // Neon cyan stroke
            Fill = OxyColor.FromArgb(100, 0, 200, 220),  // Насичене заливання
            Color2 = OxyColor.FromArgb(0, 0, 245, 255),  // Прозоре внизу для градієнту
            StrokeThickness = 3,
            Title = "Дохід",
            
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerFill = OxyColor.FromRgb(0, 245, 255),
            MarkerStroke = OxyColor.FromRgb(255, 255, 255),
            MarkerStrokeThickness = 2,
        };

        // Серія повернень - Neon Red/Pink
        var ref_ = new OxyAreaSeries
        {
            Color = OxyColor.FromRgb(255, 69, 58),  // Neon red stroke
            Fill = OxyColor.FromArgb(70, 255, 69, 58),  // Напівпрозоре заливання
            StrokeThickness = 2,
            Title = "Повернення",
            
            MarkerType = MarkerType.Diamond,
            MarkerSize = 4,
            MarkerFill = OxyColor.FromRgb(255, 69, 58),
            MarkerStroke = OxyColors.White,
            MarkerStrokeThickness = 1,
        };

        for (int i = 0; i < revenues.Length; i++)
        {
            rev.Points.Add(new OxyDataPoint(i, revenues[i]));
            ref_.Points.Add(new OxyDataPoint(i, refunds[i]));
        }

        model.Series.Add(rev);
        model.Series.Add(ref_);
        model.Legends.Add(MkLegend());
        RevenueChart.Model = model;
    }

    private void BuildPaymentMethodsChart()
    {
        var model = BaseModel();

        // ПРЕМІУМ Donut/Pie з неоновими кольорами
        var series = new OxyPieSeries
        {
            StrokeThickness = 3,
            Stroke = OxyColor.FromArgb(100, 20, 25, 45),  // Темна обводка між секторами
            InsideLabelPosition = 0.65,
            InsideLabelFormat = "{1:0}%",
            InsideLabelColor = OxyColors.White,
            OutsideLabelFormat = "{0}",
            FontSize = 12,
            AngleSpan = 360,
            StartAngle = 90,
            InnerDiameter = 0.5,  // Donut style!
            ExplodedDistance = 0.03,  // Легке розділення секторів
            TickDistance = 0,
            TickHorizontalLength = 0,
            TickRadialLength = 0,
        };

        int ci = 0;
        foreach (var m in _data.GetProperty("paymentMethods").EnumerateArray())
        {
            var name = m.GetProperty("method").GetString() switch
            {
                "Cash" => "Готівка",
                "Card" => "Картка",
                "Online" => "Онлайн",
                _ => "Інше"
            };
            series.Slices.Add(new OxyPieSlice(name, m.GetProperty("count").GetInt32())
            {
                Fill = _palette[ci++ % _palette.Length],
                IsExploded = ci == 1,  // Перший сектор трохи виділений
            });
        }
        model.Series.Add(series);
        PaymentMethodsChart.Model = model;
    }

    private void BuildClientsChart()
    {
        var model = BaseModel();
        var labels = _data.GetProperty("monthLabels").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var values = _data.GetProperty("clientsByMonth").EnumerateArray().Select(x => (double)x.GetInt32()).ToArray();

        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());

        // Градієнтний Bar з Neon Green
        var series = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(0, 255, 136),  // Neon green
            StrokeColor = OxyColor.FromRgb(0, 200, 100),
            StrokeThickness = 1,
            BarWidth = 0.7,
            Title = "Клієнти",
        };

        foreach (var v in values) series.Items.Add(new OxyBarItem(v));
        model.Series.Add(series);
        ClientsChart.Model = model;
    }

    private void BuildVisitsMonthChart()
    {
        var model = BaseModel();
        var labels = _data.GetProperty("monthLabels").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var values = _data.GetProperty("visitsByMonth").EnumerateArray().Select(x => (double)x.GetInt32()).ToArray();

        model.Axes.Add(CatAxisBottom(labels));
        model.Axes.Add(LinAxisLeft());

        // Плавна Area з Neon Blue/Purple градієнтом
        var s = new OxyAreaSeries
        {
            Color = OxyColor.FromRgb(10, 132, 255),  // Neon blue
            Fill = OxyColor.FromArgb(90, 10, 100, 220),  // Насичене заливання
            StrokeThickness = 3,
            
            MarkerType = MarkerType.Circle,
            MarkerSize = 6,
            MarkerFill = OxyColor.FromRgb(10, 132, 255),
            MarkerStroke = OxyColors.White,
            MarkerStrokeThickness = 2,
            Title = "Відвідування",
        };

        for (int i = 0; i < values.Length; i++)
            s.Points.Add(new OxyDataPoint(i, values[i]));

        model.Series.Add(s);
        VisitsMonthChart.Model = model;
    }

    private void BuildDowChart()
    {
        var model = BaseModel();
        var labels = _data.GetProperty("dowLabels").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var visits = _data.GetProperty("visitsByDow").EnumerateArray().Select(x => (double)x.GetInt32()).ToArray();
        var schedules = _data.GetProperty("schedulesByDow").EnumerateArray().Select(x => (double)x.GetInt32()).ToArray();

        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());

        // Два яскравих бари поряд - Cyan і Purple
        var vs = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(0, 245, 255),  // Neon cyan
            StrokeColor = OxyColor.FromRgb(0, 200, 220),
            StrokeThickness = 1,
            Title = "Відвідування",
            BarWidth = 0.35,
        };
        var ss = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(191, 90, 242),  // Neon purple
            StrokeColor = OxyColor.FromRgb(160, 70, 200),
            StrokeThickness = 1,
            Title = "Заняття",
            BarWidth = 0.35,
        };

        foreach (var v in visits) vs.Items.Add(new OxyBarItem(v));
        foreach (var v in schedules) ss.Items.Add(new OxyBarItem(v));

        model.Series.Add(vs);
        model.Series.Add(ss);
        model.Legends.Add(MkLegend());
        DowChart.Model = model;
    }

    private void BuildMembershipPieChart()
    {
        var model = BaseModel();

        // ПРЕМІУМ Donut для абонементів
        var series = new OxyPieSeries
        {
            StrokeThickness = 3,
            Stroke = OxyColor.FromArgb(100, 20, 25, 45),
            InsideLabelPosition = 0.7,
            InsideLabelFormat = "{1:0}%",
            InsideLabelColor = OxyColors.White,
            OutsideLabelFormat = "{0}",
            FontSize = 11,
            AngleSpan = 360,
            StartAngle = 45,  // Інший кут для різноманітності
            InnerDiameter = 0.45,  // Donut
            ExplodedDistance = 0.02,
        };

        int ci = 0;
        foreach (var item in _data.GetProperty("membershipsByType").EnumerateArray())
        {
            series.Slices.Add(new OxyPieSlice(
                item.GetProperty("name").GetString() ?? "",
                item.GetProperty("count").GetInt32())
            {
                Fill = _palette[ci++ % _palette.Length]
            });
        }
        model.Series.Add(series);
        MembershipPieChart.Model = model;
    }

    private void BuildTopLists()
    {
        var trainers = _data.GetProperty("topTrainers").EnumerateArray().ToList();
        var maxT = trainers.Count > 0 ? trainers.Max(t => t.GetProperty("count").GetInt32()) : 1;
        TopTrainersList.ItemsSource = trainers.Select((t, i) => new TopItem
        {
            Rank = (i + 1).ToString(),
            Name = t.GetProperty("name").GetString() ?? "",
            Count = t.GetProperty("count").GetInt32(),
            CountText = $"{t.GetProperty("count").GetInt32()} занять",
            BarWidth = maxT > 0 ? (double)t.GetProperty("count").GetInt32() / maxT * 160 : 0,
        }).ToList();

        var classes = _data.GetProperty("topClasses").EnumerateArray().ToList();
        var maxC = classes.Count > 0 ? classes.Max(c => c.GetProperty("count").GetInt32()) : 1;
        TopClassesList.ItemsSource = classes.Select((c, i) => new TopItem
        {
            Rank = (i + 1).ToString(),
            Name = c.GetProperty("name").GetString() ?? "",
            Count = c.GetProperty("count").GetInt32(),
            CountText = $"{c.GetProperty("count").GetInt32()} занять",
            BarWidth = maxC > 0 ? (double)c.GetProperty("count").GetInt32() / maxC * 160 : 0,
        }).ToList();
    }

    private void BuildPayrollTable()
    {
        var perSession = _data.GetProperty("perSessionRate").GetDecimal();
        var weeklyBonus = _data.GetProperty("weeklyBonusRate").GetDecimal();
        var payroll = _data.GetProperty("trainerPayroll").EnumerateArray().ToList();
        PayrollList.ItemsSource = payroll.Select(t =>
        {
            var name = t.GetProperty("name").GetString() ?? "";
            var sessionPay = t.GetProperty("sessionPay").GetDecimal();
            var bonus = t.GetProperty("bonus").GetDecimal();
            var totalPay = t.GetProperty("totalPay").GetDecimal();
            var schedules = t.GetProperty("totalSchedules").GetInt32();
            var weeksBonus = t.GetProperty("weeksWithBonus").GetInt32();
            var totalWeeks = t.GetProperty("totalWeeks").GetInt32();
            var initials = name.Split(' ') is { Length: >= 2 } p ? $"{p[0][0]}{p[1][0]}" : name.Length > 0 ? name[0].ToString() : "?";
            var hourlyRate = t.GetProperty("hourlyRate").GetDecimal();
            var rateInfo = hourlyRate > 0 ? $"{hourlyRate:N0} ₴/год · +100₴/учасник (150₴ якщо тиждень ≥7)" : "ставка не вказана · +100₴/учасник";
            return new PayrollItem
            {
                Name = name,
                Initials = initials.ToUpper(),
                TotalSchedules = schedules,
                SessionPayText = $"{sessionPay:N0} ₴",
                BonusText = bonus > 0 ? $"+{bonus:N0} ₴" : "—",
                TotalPayText = $"{totalPay:N0} ₴",
                HasBonus = bonus > 0,
                WeeksWithBonusText = weeksBonus > 0 ? $"{weeksBonus}/{totalWeeks} тиж." : $"{totalWeeks} тиж.",
                RateInfo = rateInfo,
            };
        }).ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 2 — REVENUE
    // ══════════════════════════════════════════════════════════
    private void BuildRevenueTab()
    {
        if (!_hasData) return;
        var (from, to) = GetRevPeriod();

        // KPI — filter transactions
        var txAll = _data.TryGetProperty("transactions", out var txProp) ? FilterByDate(txProp, from, to) : new();
        var revenue = txAll.Where(t => !IsRefund(t)).Sum(t => GetAmount(t));
        var refunds = txAll.Where(t => IsRefund(t)).Sum(t => GetAmount(t));
        var count = txAll.Count;
        var avg = count > 0 ? revenue / count : 0;

        RevKpiTotal.Text = $"{revenue:N0} ₴";
        RevKpiRefunds.Text = $"{refunds:N0} ₴";
        RevKpiAvg.Text = $"{avg:N0} ₴";
        RevKpiNet.Text = $"{revenue - refunds:N0} ₴";
        RevKpiPeriodLabel.Text = FmtPeriod(from, to);

        // Revenue chart — group by day/week/month depending on span
        BuildRevChartFromTx(txAll, from, to);

        // Payment methods pie
        BuildRevPaymentPie(txAll);

        // Transactions list
        RevTransactionCount.Text = $"{count} транзакцій";
        RevTransactionsList.ItemsSource = txAll
            .OrderByDescending(t => GetDate(t))
            .Take(200)
            .Select(t => new TransactionItem
            {
                DateStr = GetDate(t).ToString("dd.MM.yyyy HH:mm"),
                ClientName = GetStr(t, "clientName"),
                Description = GetStr(t, "description"),
                Method = GetStr(t, "method") switch { "Cash" => "Готівка", "Card" => "Картка", "Online" => "Онлайн", _ => "Інше" },
                StatusText = IsRefund(t) ? "Повернення" : "Успішно",
                StatusBg = IsRefund(t) ? new SolidColorBrush(Color.FromRgb(61, 10, 10)) : new SolidColorBrush(Color.FromRgb(5, 61, 46)),
                StatusFg = IsRefund(t) ? new SolidColorBrush(Color.FromRgb(248, 113, 113)) : new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                AmountStr = $"{GetAmount(t):N0} ₴",
                AmountColor = IsRefund(t) ? new SolidColorBrush(Color.FromRgb(248, 113, 113)) : new SolidColorBrush(Color.FromRgb(52, 211, 153)),
            }).ToList();
    }

    private void BuildRevChartFromTx(List<JsonElement> txAll, DateTime from, DateTime to)
    {
        var model = BaseModel();
        var span = to - from;

        if (span.TotalDays <= 31)
        {
            // Group by day - Line chart з маркерами
            var groups = txAll.Where(t => !IsRefund(t))
                .GroupBy(t => GetDate(t).Date)
                .OrderBy(g => g.Key)
                .ToList();
            var labels = groups.Select(g => g.Key.ToString("dd.MM")).ToArray();
            model.Axes.Add(CatAxisBottom(labels));
            model.Axes.Add(LinAxisLeft());

            // Преміум Area з gradient fill
            var s = new OxyAreaSeries
            {
                Color = OxyColor.FromRgb(0, 245, 255),  // Neon cyan
                Fill = OxyColor.FromArgb(90, 0, 200, 220),
                StrokeThickness = 3,
                Title = "Дохід",
                
                MarkerType = MarkerType.Circle,
                MarkerSize = 5,
                MarkerFill = OxyColor.FromRgb(0, 245, 255),
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 2,
            };
            for (int i = 0; i < groups.Count; i++)
                s.Points.Add(new OxyDataPoint(i, (double)groups[i].Sum(t => GetAmount(t))));
            model.Series.Add(s);
        }
        else
        {
            // Group by month - комбінований графік
            var groups = txAll.Where(t => !IsRefund(t))
                .GroupBy(t => new { GetDate(t).Year, GetDate(t).Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ToList();
            var labels = groups.Select(g => $"{g.Key.Month:D2}/{g.Key.Year % 100:D2}").ToArray();
            model.Axes.Add(CatAxisBottom(labels));
            model.Axes.Add(LinAxisLeft());

            var s = new OxyAreaSeries
            {
                Color = OxyColor.FromRgb(0, 255, 136),  // Neon green
                Fill = OxyColor.FromArgb(80, 0, 220, 110),
                StrokeThickness = 3,
                Title = "Дохід",
                
                MarkerType = MarkerType.Circle,
                MarkerSize = 6,
                MarkerFill = OxyColor.FromRgb(0, 255, 136),
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 2,
            };
            var r = new OxyAreaSeries
            {
                Color = OxyColor.FromRgb(255, 69, 58),  // Neon red
                Fill = OxyColor.FromArgb(60, 255, 69, 58),
                StrokeThickness = 2,
                Title = "Повернення",
                MarkerType = MarkerType.Diamond,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(255, 69, 58),
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 1,
            };

            var refGroups = txAll.Where(t => IsRefund(t))
                .GroupBy(t => new { GetDate(t).Year, GetDate(t).Month })
                .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Sum(t => GetAmount(t)));
            for (int i = 0; i < groups.Count; i++)
            {
                s.Points.Add(new OxyDataPoint(i, (double)groups[i].Sum(t => GetAmount(t))));
                var key = (groups[i].Key.Year, groups[i].Key.Month);
                r.Points.Add(new OxyDataPoint(i, refGroups.TryGetValue(key, out var rv) ? (double)rv : 0));
            }
            model.Series.Add(s);
            model.Series.Add(r);
            model.Legends.Add(MkLegend());
        }
        RevRevenueChart.Model = model;
    }

    private void BuildRevPaymentPie(List<JsonElement> txAll)
    {
        var model = BaseModel();

        // Преміум Donut
        var series = new OxyPieSeries
        {
            StrokeThickness = 3,
            Stroke = OxyColor.FromArgb(100, 20, 25, 45),
            InsideLabelPosition = 0.65,
            InsideLabelFormat = "{1:0}%",
            InsideLabelColor = OxyColors.White,
            OutsideLabelFormat = "{0}",
            FontSize = 11,
            AngleSpan = 360,
            StartAngle = 90,
            InnerDiameter = 0.5,
            ExplodedDistance = 0.02,
        };

        int ci = 0;
        foreach (var g in txAll.GroupBy(t => GetStr(t, "method")).OrderByDescending(g => g.Count()))
        {
            var label = g.Key switch { "Cash" => "Готівка", "Card" => "Картка", "Online" => "Онлайн", _ => "Інше" };
            series.Slices.Add(new OxyPieSlice(label, g.Count())
            {
                Fill = _palette[ci++ % _palette.Length],
                IsExploded = ci == 1,
            });
        }
        model.Series.Add(series);
        RevPaymentPie.Model = model;
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 3 — ACTIVE CLIENTS (period)
    // ══════════════════════════════════════════════════════════
    private void BuildClientsTab()
    {
        if (!_hasData) return;
        var (from, to) = GetCliPeriod();

        var visits = _data.TryGetProperty("visitLog", out var vp) ? FilterByDate(vp, from, to) : new();
        var unique = visits.Select(v => GetStr(v, "clientId")).Distinct().Count();
        var newCli = visits.Select(v => GetStr(v, "clientId")).Distinct()
            .Count(cid =>
            {
                // Check if first visit is within period
                if (!_data.TryGetProperty("clientFirstVisit", out var cf)) return false;
                foreach (var el in cf.EnumerateArray())
                    if (GetStr(el, "clientId") == cid && DateTime.TryParse(GetStr(el, "date"), out var d) && d >= from && d <= to)
                        return true;
                return false;
            });
        var avgV = unique > 0 ? (double)visits.Count / unique : 0;

        CliKpiUnique.Text = unique.ToString();
        CliKpiNew.Text = newCli.ToString();
        CliKpiAvgVisits.Text = $"{avgV:F1}";

        // Activity chart
        BuildCliActivityChart(visits, from, to);
        BuildCliNewVsReturn(visits, newCli, unique - newCli);

        // Top clients
        var topCli = visits
            .GroupBy(v => GetStr(v, "clientId"))
            .Select(g => new
            {
                ClientId = g.Key,
                Count = g.Count(),
                Last = g.Max(v => GetDate(v)),
                Total = g.Sum(v => GetDecimalSafe(v, "paid")),
            })
            .OrderByDescending(c => c.Count)
            .Take(50)
            .Select((c, i) =>
            {
                var name = GetClientName(c.ClientId);
                var phone = GetClientPhone(c.ClientId);
                return new ActiveClientItem
                {
                    Rank = (i + 1).ToString(),
                    Name = name,
                    Phone = phone,
                    Initials = MkInitials(name),
                    VisitCount = c.Count.ToString(),
                    LastVisit = c.Last.ToString("dd.MM.yyyy"),
                    TotalPaid = $"{c.Total:N0} ₴",
                };
            }).ToList();

        CliActiveList.ItemsSource = topCli;
    }

    private void BuildCliActivityChart(List<JsonElement> visits, DateTime from, DateTime to)
    {
        var model = BaseModel();
        var groups = visits.GroupBy(v => GetDate(v).Date).OrderBy(g => g.Key).ToList();
        if (groups.Count == 0) { CliActivityChart.Model = model; return; }
        var labels = groups.Select(g => g.Key.ToString("dd.MM")).ToArray();
        model.Axes.Add(CatAxisBottom(labels));
        model.Axes.Add(LinAxisLeft());

        // Преміум Area з Neon Purple градієнтом
        var s = new OxyAreaSeries
        {
            Color = OxyColor.FromRgb(191, 90, 242),  // Neon purple
            Fill = OxyColor.FromArgb(85, 160, 70, 210),
            StrokeThickness = 3,
            
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerFill = OxyColor.FromRgb(191, 90, 242),
            MarkerStroke = OxyColors.White,
            MarkerStrokeThickness = 2,
            Title = "Активність",
        };

        for (int i = 0; i < groups.Count; i++)
            s.Points.Add(new OxyDataPoint(i, groups[i].Count()));
        model.Series.Add(s);
        CliActivityChart.Model = model;
    }

    private void BuildCliNewVsReturn(List<JsonElement> visits, int newCount, int returnCount)
    {
        var model = BaseModel();

        // Semi-donut для нових vs повторних
        var series = new OxyPieSeries
        {
            StrokeThickness = 3,
            Stroke = OxyColor.FromArgb(100, 20, 25, 45),
            InsideLabelPosition = 0.6,
            InsideLabelFormat = "{1:0}%",
            InsideLabelColor = OxyColors.White,
            OutsideLabelFormat = "{0}",
            FontSize = 12,
            AngleSpan = 270,  // 3/4 кола - незвичайний вигляд!
            StartAngle = 135,
            InnerDiameter = 0.55,
            ExplodedDistance = 0.03,
        };

        if (newCount > 0)
            series.Slices.Add(new OxyPieSlice("Нові", newCount)
            {
                Fill = OxyColor.FromRgb(0, 255, 136),  // Neon green
                IsExploded = true
            });
        if (returnCount > 0)
            series.Slices.Add(new OxyPieSlice("Повторні", returnCount)
            {
                Fill = OxyColor.FromRgb(0, 245, 255)  // Neon cyan
            });

        model.Series.Add(series);
        CliNewVsReturn.Model = model;
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 4 — ALL TIME
    // ══════════════════════════════════════════════════════════
    private void BuildAllTimeTab()
    {
        if (!_hasData) return;

        var allVisits = _data.TryGetProperty("visitLog", out var vp) ? vp.EnumerateArray().ToList() : new();

        var topByVisit = allVisits
            .GroupBy(v => GetStr(v, "clientId"))
            .Select(g => new { Id = g.Key, Visits = g.Count(), Total = g.Sum(v => GetDecimalSafe(v, "paid")), First = g.Min(v => GetDate(v)) })
            .OrderByDescending(c => c.Visits).Take(20).ToList();

        // Charts
        BuildAllTimeVisitsChart(topByVisit.Take(10).Select(c => (GetClientName(c.Id), c.Visits)).ToList());
        BuildAllTimeRevenueChart(topByVisit.OrderByDescending(c => c.Total).Take(10).Select(c => (GetClientName(c.Id), (double)c.Total)).ToList());

        AllTimeList.ItemsSource = topByVisit.Select((c, i) =>
        {
            var name = GetClientName(c.Id);
            var mCount = _data.TryGetProperty("membershipsByClient", out var mp)
                ? mp.EnumerateArray().Count(m => GetStr(m, "clientId") == c.Id)
                : 0;
            return new AllTimeClientItem
            {
                Rank = (i + 1).ToString(),
                Name = name,
                Phone = GetClientPhone(c.Id),
                Initials = MkInitials(name),
                VisitCount = c.Visits.ToString(),
                MembershipCount = mCount.ToString(),
                FirstVisit = c.First.ToString("dd.MM.yyyy"),
                TotalPaid = $"{c.Total:N0} ₴",
            };
        }).ToList();
    }

    private void BuildAllTimeVisitsChart(List<(string Name, int Visits)> data)
    {
        var model = BaseModel();
        var labels = data.Select(d => d.Name.Split(' ').FirstOrDefault() ?? d.Name).ToArray();
        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());

        // Горизонтальний Bar з Neon Purple
        var s = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(191, 90, 242),  // Neon purple
            StrokeColor = OxyColor.FromRgb(160, 70, 200),
            StrokeThickness = 1,
            BarWidth = 0.65,
            Title = "Візити",
        };
        foreach (var d in data) s.Items.Add(new OxyBarItem(d.Visits));
        model.Series.Add(s);
        AllTimeVisitsChart.Model = model;
    }

    private void BuildAllTimeRevenueChart(List<(string Name, double Total)> data)
    {
        var model = BaseModel();
        var labels = data.Select(d => d.Name.Split(' ').FirstOrDefault() ?? d.Name).ToArray();
        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());

        // Горизонтальний Bar з Neon Green
        var s = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(0, 255, 136),  // Neon green
            StrokeColor = OxyColor.FromRgb(0, 200, 100),
            StrokeThickness = 1,
            BarWidth = 0.65,
            Title = "Дохід",
        };
        foreach (var d in data) s.Items.Add(new OxyBarItem(d.Total));
        model.Series.Add(s);
        AllTimeRevenueChart.Model = model;
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 5 — SESSIONS
    // ══════════════════════════════════════════════════════════
    private void BuildSessionsTab()
    {
        if (!_hasData) return;
        var (from, to) = GetSesPeriod();

        var schedules = _data.TryGetProperty("schedules", out var sp)
            ? FilterByDate(sp, from, to) : new();

        var byClass = schedules
            .GroupBy(s => GetStr(s, "className"))
            .Select(g => new
            {
                Name = g.Key,
                TrainerName = GetStr(g.First(), "trainerName"),
                SessionCount = g.Count(),
                TotalPart = g.Sum(s => GetIntSafe(s, "participantCount")),
                MaxCap = GetIntSafe(g.First(), "maxCapacity"),
            })
            .OrderByDescending(c => c.TotalPart)
            .ToList();

        // Popular chart - Neon Cyan бари
        var model = BaseModel();
        var labels = byClass.Take(10).Select(c => c.Name).ToArray();
        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());
        var s = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(0, 245, 255),  // Neon cyan
            StrokeColor = OxyColor.FromRgb(0, 200, 220),
            StrokeThickness = 1,
            BarWidth = 0.7,
            Title = "Учасники",
        };
        foreach (var c in byClass.Take(10)) s.Items.Add(new OxyBarItem(c.TotalPart));
        model.Series.Add(s);
        SesPopularChart.Model = model;

        // Fill chart - динамічні кольори в залежності від %
        var model2 = BaseModel();
        var fillLabels = byClass.Take(8).Select(c => c.Name).ToArray();
        model2.Axes.Add(CatAxisLeft(fillLabels));
        model2.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = 100,
            TextColor = OxyColor.FromRgb(140, 155, 180),
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromArgb(25, 100, 150, 200),
            TicklineColor = OxyColors.Transparent,
            StringFormat = "0'%'",
        });

        // Використовуємо градієнт від червоного до зеленого через оранжевий
        var sf = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(0, 255, 136),  // Default green
            StrokeThickness = 1,
            StrokeColor = OxyColor.FromRgb(0, 200, 100),
            BarWidth = 0.65,
            Title = "Заповненість",
        };
        foreach (var c in byClass.Take(8))
        {
            var fill = c.MaxCap > 0 ? Math.Min(100.0 * c.TotalPart / c.MaxCap / Math.Max(c.SessionCount, 1), 100) : 0;
            sf.Items.Add(new OxyBarItem(fill));
        }
        model2.Series.Add(sf);
        SesFillChart.Model = model2;

        SesDetailList.ItemsSource = byClass.Select(c =>
        {
            var fill = c.MaxCap > 0 ? Math.Min(100.0 * c.TotalPart / c.MaxCap / Math.Max(c.SessionCount, 1), 100) : 0;
            return new SessionDetailItem
            {
                Name = c.Name,
                TrainerName = c.TrainerName,
                SessionCount = c.SessionCount.ToString(),
                TotalParticipants = c.TotalPart.ToString(),
                FillRateStr = $"{fill:F0}%",
                FillColor = new SolidColorBrush(fill >= 80 ? Color.FromRgb(52, 211, 153) : fill >= 50 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(248, 113, 113)),
                FillBarWidth = fill * 60 / 100,
                Trend = "+",
                TrendColor = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
            };
        }).ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 6 — MEMBERSHIPS
    // ══════════════════════════════════════════════════════════
    private void BuildMembershipsTab()
    {
        if (!_hasData) return;
        var (from, to) = GetMemPeriod();

        var mems = _data.TryGetProperty("memberships", out var mp) ? FilterByDate(mp, from, to) : new();

        var byType = mems
            .GroupBy(m => GetStr(m, "typeName"))
            .Select(g => new { Name = g.Key, Count = g.Count(), Revenue = g.Sum(m => GetDecimalSafe(m, "price")), Price = GetDecimalSafe(g.First(), "price") })
            .OrderByDescending(t => t.Count)
            .ToList();
        var totalRev = byType.Sum(t => t.Revenue);

        // Sales chart
        var model = BaseModel();
        var labels = byType.Select(t => t.Name).ToArray();
        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());
        int ci = 0;
        var sb = new OxyBarSeries { StrokeThickness = 0, FillColor = _palette[0] };
        foreach (var t in byType) sb.Items.Add(new OxyBarItem(t.Count));
        model.Series.Add(sb);
        MemSalesChart.Model = model;

        // Pie
        var model2 = BaseModel();
        var pie = new OxyPieSeries { StrokeThickness = 2, InsideLabelPosition = 0.6, InsideLabelFormat = "{1:0}%", OutsideLabelFormat = "{0}", AngleSpan = 360, StartAngle = 90 };
        ci = 0;
        foreach (var t in byType) pie.Slices.Add(new OxyPieSlice(t.Name, t.Count) { Fill = _palette[ci++ % _palette.Length] });
        model2.Series.Add(pie);
        MemPieChart.Model = model2;

        string[] colors = ["#6C63FF", "#34d399", "#fb923c", "#60a5fa", "#f87171", "#a78bfa"];
        MemDetailList.ItemsSource = byType.Select((t, i) => new MembershipDetailItem
        {
            Name = t.Name,
            PriceStr = $"від {t.Price:N0} ₴",
            SoldCount = t.Count.ToString(),
            Revenue = $"{t.Revenue:N0} ₴",
            ShareStr = totalRev > 0 ? $"{100m * t.Revenue / totalRev:F1}% доходу" : "—",
            AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i % colors.Length])!),
        }).ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 7 — AVG LOAD
    // ══════════════════════════════════════════════════════════
    private void BuildAvgLoadTab()
    {
        if (!_hasData) return;
        var (from, to) = GetLdPeriod();

        var schedules = _data.TryGetProperty("schedules", out var sp) ? FilterByDate(sp, from, to) : new();
        if (schedules.Count == 0) { LdKpiAvg.Text = "0"; LdKpiPeak.Text = "0"; LdKpiFill.Text = "0%"; return; }

        var avgPeople = schedules.Average(s => GetIntSafe(s, "participantCount"));
        var maxEntry = schedules.OrderByDescending(s => GetIntSafe(s, "participantCount")).First();
        var allCap = schedules.Where(s => GetIntSafe(s, "maxCapacity") > 0).ToList();
        var avgFill = allCap.Count > 0 ? allCap.Average(s => 100.0 * GetIntSafe(s, "participantCount") / GetIntSafe(s, "maxCapacity")) : 0;

        LdKpiAvg.Text = $"{avgPeople:F1}";
        LdKpiPeak.Text = GetIntSafe(maxEntry, "participantCount").ToString();
        LdKpiPeakDay.Text = GetDate(maxEntry).ToString("dddd, dd.MM.yyyy");
        LdKpiFill.Text = $"{avgFill:F0}%";

        // DoW chart
        var model = BaseModel();
        var dowLabels = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
        var dowAvg = Enumerable.Range(0, 7).Select(d => schedules.Where(s => (int)GetDate(s).DayOfWeek == (d + 1) % 7).DefaultIfEmpty().Average(s => s.ValueKind != JsonValueKind.Undefined ? GetIntSafe(s, "participantCount") : 0)).ToArray();
        model.Axes.Add(CatAxisLeft(dowLabels));
        model.Axes.Add(LinAxisBottom());
        var sd = new OxyBarSeries { FillColor = OxyColor.FromRgb(96, 165, 250), StrokeThickness = 0 };
        foreach (var v in dowAvg) sd.Items.Add(new OxyBarItem(v));
        model.Series.Add(sd);
        LdDowChart.Model = model;

        // Monthly trend
        var model2 = BaseModel();
        var monthGroups = schedules.GroupBy(s => new { GetDate(s).Year, GetDate(s).Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ToList();
        var ml = monthGroups.Select(g => $"{g.Key.Month:D2}/{g.Key.Year % 100:D2}").ToArray();
        model2.Axes.Add(CatAxisBottom(ml));
        model2.Axes.Add(LinAxisLeft());
        var sm = new OxyAreaSeries { Color = OxyColor.FromRgb(167, 139, 250), Fill = OxyColor.FromArgb(50, 167, 139, 250), StrokeThickness = 2 };
        for (int i = 0; i < monthGroups.Count; i++) sm.Points.Add(new OxyDataPoint(i, monthGroups[i].Average(s => GetIntSafe(s, "participantCount"))));
        model2.Series.Add(sm);
        LdMonthChart.Model = model2;
    }

    // ══════════════════════════════════════════════════════════
    //  TAB 8 — CANCELLATIONS
    // ══════════════════════════════════════════════════════════
    private void BuildCancelsTab()
    {
        if (!_hasData) return;
        var (from, to) = GetCnlPeriod();

        var canceledMem = _data.TryGetProperty("canceledMemberships", out var cmp) ? FilterByDate(cmp, from, to) : new();
        var canceledSes = _data.TryGetProperty("canceledSchedules", out var csp) ? FilterByDate(csp, from, to) : new();
        var allMem = _data.TryGetProperty("memberships", out var mp) ? FilterByDate(mp, from, to).Count : 1;

        var lostRevenue = canceledMem.Sum(m => GetDecimalSafe(m, "price")) + canceledSes.Sum(s => GetDecimalSafe(s, "refundAmount"));
        var total = canceledMem.Count + canceledSes.Count;
        var rate = allMem > 0 ? 100.0 * canceledMem.Count / allMem : 0;

        CnlKpiMemberships.Text = canceledMem.Count.ToString();
        CnlKpiSessions.Text = canceledSes.Count.ToString();
        CnlKpiLost.Text = $"{lostRevenue:N0} ₴";
        CnlKpiRate.Text = $"{rate:F1}%";

        // Trend chart
        BuildCancelTrend(canceledMem, canceledSes, from, to);
        BuildCancelReasons(canceledMem, canceledSes);

        // Detail list
        var items = canceledMem.Select(m => new CancelItem
        {
            DateStr = GetDate(m).ToString("dd.MM.yyyy"),
            ClientName = GetStr(m, "clientName"),
            TypeName = GetStr(m, "typeName"),
            Category = "Абонемент",
            CategoryBg = new SolidColorBrush(Color.FromRgb(26, 23, 68)),
            CategoryFg = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            LostAmount = $"{GetDecimalSafe(m, "price"):N0} ₴",
        }).Concat(canceledSes.Select(s => new CancelItem
        {
            DateStr = GetDate(s).ToString("dd.MM.yyyy"),
            ClientName = GetStr(s, "clientName"),
            TypeName = GetStr(s, "className"),
            Category = "Сеанс",
            CategoryBg = new SolidColorBrush(Color.FromRgb(61, 42, 3)),
            CategoryFg = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            LostAmount = GetDecimalSafe(s, "refundAmount") > 0 ? $"{GetDecimalSafe(s, "refundAmount"):N0} ₴" : "—",
        })).OrderByDescending(c => c.DateStr).ToList();

        CnlDetailList.ItemsSource = items;
    }

    private void BuildCancelTrend(List<JsonElement> mem, List<JsonElement> ses, DateTime from, DateTime to)
    {
        var model = BaseModel();
        var all = mem.Select(m => (GetDate(m), "mem")).Concat(ses.Select(s => (GetDate(s), "ses"))).ToList();
        var groups = all.GroupBy(x => new { x.Item1.Year, x.Item1.Month })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ToList();
        if (groups.Count == 0) { CnlTrendChart.Model = model; return; }

        var labels = groups.Select(g => $"{g.Key.Month:D2}/{g.Key.Year % 100:D2}").ToArray();

        // BarSeries ВИМАГАЄ CategoryAxis на осі Y (горизонтальні бари)
        model.Axes.Add(CatAxisLeft(labels));
        model.Axes.Add(LinAxisBottom());

        var smem = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(248, 113, 113),
            StrokeThickness = 0,
            Title = "Абонементи",
            BarWidth = 0.35,
        };
        var sses = new OxyBarSeries
        {
            FillColor = OxyColor.FromRgb(245, 158, 11),
            StrokeThickness = 0,
            Title = "Сеанси",
            BarWidth = 0.35,
        };

        foreach (var g in groups)
        {
            smem.Items.Add(new OxyBarItem(g.Count(x => x.Item2 == "mem")));
            sses.Items.Add(new OxyBarItem(g.Count(x => x.Item2 == "ses")));
        }

        model.Series.Add(smem);
        model.Series.Add(sses);
        model.Legends.Add(MkLegend());
        CnlTrendChart.Model = model;
    }

    private void BuildCancelReasons(List<JsonElement> mem, List<JsonElement> ses)
    {
        var model = BaseModel();
        var series = new OxyPieSeries { StrokeThickness = 2, InsideLabelPosition = 0.6, InsideLabelFormat = "{1:0}%", OutsideLabelFormat = "{0}", AngleSpan = 360, StartAngle = 90 };
        int ci = 0;
        var reasons = mem.Select(m => GetStr(m, "cancelReason")).Concat(ses.Select(s => GetStr(s, "cancelReason")))
            .Where(r => !string.IsNullOrEmpty(r))
            .GroupBy(r => r).OrderByDescending(g => g.Count()).ToList();
        if (reasons.Count == 0)
        {
            series.Slices.Add(new OxyPieSlice("Абонементи", mem.Count) { Fill = _palette[0] });
            series.Slices.Add(new OxyPieSlice("Сеанси", ses.Count) { Fill = _palette[1] });
        }
        else
        {
            foreach (var g in reasons) series.Slices.Add(new OxyPieSlice(g.Key, g.Count()) { Fill = _palette[ci++ % _palette.Length] });
        }
        model.Series.Add(series);
        CnlReasonsChart.Model = model;
    }

    // ══════════════════════════════════════════════════════════
    //  EXPORTS — CSV
    // ══════════════════════════════════════════════════════════
    private void ExportRevenueCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetRevPeriod();
        var txAll = _data.TryGetProperty("transactions", out var tp) ? FilterByDate(tp, from, to) : new();
        var path = PickSavePath("revenue_report", "csv");
        if (path == null) return;

        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine($"Звіт по доходах за період: {FmtPeriod(from, to)}");
        sw.WriteLine($"Загальний дохід;{txAll.Where(t => !IsRefund(t)).Sum(t => GetAmount(t)):N0} ₴");
        sw.WriteLine($"Повернення;{txAll.Where(t => IsRefund(t)).Sum(t => GetAmount(t)):N0} ₴");
        sw.WriteLine($"Кількість транзакцій;{txAll.Count}");
        sw.WriteLine();
        sw.WriteLine("Дата;Клієнт;Опис;Метод;Статус;Сума (₴)");
        foreach (var t in txAll.OrderByDescending(t => GetDate(t)))
            sw.WriteLine($"{GetDate(t):dd.MM.yyyy HH:mm};{GetStr(t, "clientName")};{GetStr(t, "description")};{GetStr(t, "method")};{(IsRefund(t) ? "Повернення" : "Успішно")};{GetAmount(t):N0}");
        Done(path);
    }

    private void ExportClientsCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetCliPeriod();
        var visits = _data.TryGetProperty("visitLog", out var vp) ? FilterByDate(vp, from, to) : new();
        var path = PickSavePath("clients_period", "csv");
        if (path == null) return;

        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine($"Звіт по активних клієнтах за: {FmtPeriod(from, to)}");
        sw.WriteLine();
        sw.WriteLine("Клієнт;Кількість відвідань;Остання візита;Оплачено (₴)");
        foreach (var g in visits.GroupBy(v => GetStr(v, "clientId")).OrderByDescending(g => g.Count()))
            sw.WriteLine($"{GetClientName(g.Key)};{g.Count()};{g.Max(v => GetDate(v)):dd.MM.yyyy};{g.Sum(v => GetDecimalSafe(v, "paid")):N0}");
        Done(path);
    }

    private void ExportAllTimeCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var path = PickSavePath("all_time_clients", "csv");
        if (path == null) return;
        var allVisits = _data.TryGetProperty("visitLog", out var vp) ? vp.EnumerateArray().ToList() : new();
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine("Рейтинг клієнтів за весь час");
        sw.WriteLine();
        sw.WriteLine("Місце;Клієнт;Візитів;Абонементів;Перша візита;Сума (₴)");
        int i = 1;
        foreach (var g in allVisits.GroupBy(v => GetStr(v, "clientId")).OrderByDescending(g => g.Count()).Take(100))
        {
            var mCount = _data.TryGetProperty("membershipsByClient", out var mp) ? mp.EnumerateArray().Count(m => GetStr(m, "clientId") == g.Key) : 0;
            sw.WriteLine($"{i++};{GetClientName(g.Key)};{g.Count()};{mCount};{g.Min(v => GetDate(v)):dd.MM.yyyy};{g.Sum(v => GetDecimalSafe(v, "paid")):N0}");
        }
        Done(path);
    }

    private void ExportSessionsCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetSesPeriod();
        var scheds = _data.TryGetProperty("schedules", out var sp) ? FilterByDate(sp, from, to) : new();
        var path = PickSavePath("sessions_report", "csv");
        if (path == null) return;
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine($"Звіт по популярних сеансах за: {FmtPeriod(from, to)}");
        sw.WriteLine();
        sw.WriteLine("Заняття;Тренер;Кількість проведень;Всього учасників;Середня заповненість (%)");
        foreach (var g in scheds.GroupBy(s => GetStr(s, "className")).OrderByDescending(g => g.Sum(s => GetIntSafe(s, "participantCount"))))
        {
            var fill = g.Where(s => GetIntSafe(s, "maxCapacity") > 0).DefaultIfEmpty().Average(s => s.ValueKind != JsonValueKind.Undefined ? 100.0 * GetIntSafe(s, "participantCount") / GetIntSafe(s, "maxCapacity") : 0);
            sw.WriteLine($"{g.Key};{GetStr(g.First(), "trainerName")};{g.Count()};{g.Sum(s => GetIntSafe(s, "participantCount"))};{fill:F0}");
        }
        Done(path);
    }

    private void ExportMembershipsCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetMemPeriod();
        var mems = _data.TryGetProperty("memberships", out var mp) ? FilterByDate(mp, from, to) : new();
        var path = PickSavePath("memberships_report", "csv");
        if (path == null) return;
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine($"Звіт по абонементах за: {FmtPeriod(from, to)}");
        sw.WriteLine();
        sw.WriteLine("Тип;Продано;Дохід (₴);Частка доходу (%)");
        var total = mems.Sum(m => GetDecimalSafe(m, "price"));
        foreach (var g in mems.GroupBy(m => GetStr(m, "typeName")).OrderByDescending(g => g.Count()))
        {
            var rev = g.Sum(m => GetDecimalSafe(m, "price"));
            sw.WriteLine($"{g.Key};{g.Count()};{rev:N0};{(total > 0 ? 100m * rev / total : 0):F1}");
        }
        Done(path);
    }

    private void ExportLoadCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetLdPeriod();
        var scheds = _data.TryGetProperty("schedules", out var sp) ? FilterByDate(sp, from, to) : new();
        var path = PickSavePath("load_report", "csv");
        if (path == null) return;
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine($"Звіт по завантаженості за: {FmtPeriod(from, to)}");
        sw.WriteLine();
        sw.WriteLine("Дата;Заняття;Тренер;Учасників;Місткість;Заповненість (%)");
        foreach (var s2 in scheds.OrderBy(s => GetDate(s)))
        {
            var cap = GetIntSafe(s2, "maxCapacity");
            var part = GetIntSafe(s2, "participantCount");
            sw.WriteLine($"{GetDate(s2):dd.MM.yyyy HH:mm};{GetStr(s2, "className")};{GetStr(s2, "trainerName")};{part};{cap};{(cap > 0 ? 100.0 * part / cap : 0):F0}");
        }
        Done(path);
    }

    private void ExportCancelsCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetCnlPeriod();
        var canceledMem = _data.TryGetProperty("canceledMemberships", out var cmp) ? FilterByDate(cmp, from, to) : new();
        var canceledSes = _data.TryGetProperty("canceledSchedules", out var csp) ? FilterByDate(csp, from, to) : new();
        var path = PickSavePath("cancels_report", "csv");
        if (path == null) return;
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine($"Звіт по скасуваннях за: {FmtPeriod(from, to)}");
        sw.WriteLine();
        sw.WriteLine("Дата;Клієнт;Тип;Категорія;Втрати (₴)");
        foreach (var m in canceledMem.OrderByDescending(m => GetDate(m)))
            sw.WriteLine($"{GetDate(m):dd.MM.yyyy};{GetStr(m, "clientName")};{GetStr(m, "typeName")};Абонемент;{GetDecimalSafe(m, "price"):N0}");
        foreach (var s2 in canceledSes.OrderByDescending(s => GetDate(s)))
            sw.WriteLine($"{GetDate(s2):dd.MM.yyyy};{GetStr(s2, "clientName")};{GetStr(s2, "className")};Сеанс;{GetDecimalSafe(s2, "refundAmount"):N0}");
        Done(path);
    }

    // ══════════════════════════════════════════════════════════
    //  EXPORTS — WORD
    // ══════════════════════════════════════════════════════════
    private void ExportRevenueWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetRevPeriod();
        var tx = _data.TryGetProperty("transactions", out var tp) ? FilterByDate(tp, from, to) : new();
        var rev = tx.Where(t => !IsRefund(t)).Sum(t => GetAmount(t));
        var refunds = tx.Where(t => IsRefund(t)).Sum(t => GetAmount(t));
        var prevRev = GetPrevPeriodRevenue(from, to);
        var diff = prevRev > 0 ? $"{100m * (rev - prevRev) / prevRev:+0.0;-0.0}%" : "немає даних для порівняння";

        var peakDay = tx.Where(t => !IsRefund(t))
            .GroupBy(t => GetDate(t).Date)
            .OrderByDescending(g => g.Sum(t => GetAmount(t)))
            .FirstOrDefault();
        var peakStr = peakDay != null ? $"{peakDay.Key:dd.MM.yyyy} ({peakDay.Sum(t => GetAmount(t)):N0} ₴)" : "даних недостатньо";

        var path = PickSavePath("revenue_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Звіт по доходах",
            $"Звітний період: {FmtPeriod(from, to)}",
            $"За період з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} загальний дохід фітнес-клубу склав {rev:N0} ₴. " +
            $"У порівнянні з аналогічним попереднім періодом показник змінився на {diff}. " +
            $"Повернення коштів за цей час становили {refunds:N0} ₴, чистий дохід — {rev - refunds:N0} ₴. " +
            $"Найприбутковіший день: {peakStr}. " +
            $"Всього оброблено {tx.Count} транзакцій.",
            [
                ("Загальний дохід",  $"{rev:N0} ₴"),
                ("Повернення",       $"{refunds:N0} ₴"),
                ("Чистий дохід",     $"{rev - refunds:N0} ₴"),
                ("Кількість транзакцій", tx.Count.ToString()),
                ("Найприбутковіший день", peakStr),
            ]);
        Done(path);
    }

    private void ExportClientsWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetCliPeriod();
        var visits = _data.TryGetProperty("visitLog", out var vp) ? FilterByDate(vp, from, to) : new();
        var unique = visits.Select(v => GetStr(v, "clientId")).Distinct().Count();
        var topCli = visits.GroupBy(v => GetStr(v, "clientId")).OrderByDescending(g => g.Count()).FirstOrDefault();
        var topName = topCli != null ? GetClientName(topCli.Key) : "немає даних";
        var topCount = topCli?.Count() ?? 0;

        var path = PickSavePath("clients_period_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Звіт по активних клієнтах",
            $"Звітний період: {FmtPeriod(from, to)}",
            $"За період з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} фітнес-клуб відвідало {unique} унікальних клієнтів. " +
            $"Загальна кількість зафіксованих відвідань склала {visits.Count}. " +
            $"Найактивнішим клієнтом за цей час є {topName} з показником {topCount} відвідань. " +
            $"Середня кількість відвідань на одного клієнта: {(unique > 0 ? (double)visits.Count / unique : 0):F1}.",
            [
                ("Унікальних клієнтів", unique.ToString()),
                ("Всього відвідань",    visits.Count.ToString()),
                ("Найактивніший",       $"{topName} ({topCount} відвідань)"),
            ]);
        Done(path);
    }

    private void ExportAllTimeWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var allVisits = _data.TryGetProperty("visitLog", out var vp) ? vp.EnumerateArray().ToList() : new();
        var top5 = allVisits.GroupBy(v => GetStr(v, "clientId")).OrderByDescending(g => g.Count()).Take(5)
            .Select((g, i) => $"{i + 1}. {GetClientName(g.Key)} — {g.Count()} відвідань").ToList();
        var path = PickSavePath("all_time_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Рейтинг клієнтів за весь час",
            $"Сформовано: {DateTime.Now:dd.MM.yyyy HH:mm}",
            $"За весь час роботи фітнес-клубу було зафіксовано {allVisits.Count} відвідань від " +
            $"{allVisits.Select(v => GetStr(v, "clientId")).Distinct().Count()} унікальних клієнтів. " +
            $"Найвідданіші клієнти: {string.Join("; ", top5)}.",
            top5.Select((t, i) => (t, "")).ToArray());
        Done(path);
    }

    private void ExportSessionsWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetSesPeriod();
        var scheds = _data.TryGetProperty("schedules", out var sp) ? FilterByDate(sp, from, to) : new();
        var top = scheds.GroupBy(s => GetStr(s, "className")).OrderByDescending(g => g.Sum(s => GetIntSafe(s, "participantCount"))).FirstOrDefault();
        var topName = top?.Key ?? "немає даних";
        var topPart = top?.Sum(s => GetIntSafe(s, "participantCount")) ?? 0;
        var path = PickSavePath("sessions_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Звіт по сеансах",
            $"Звітний період: {FmtPeriod(from, to)}",
            $"За період з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} проведено {scheds.Count} занять. " +
            $"Загальна кількість учасників становила {scheds.Sum(s => GetIntSafe(s, "participantCount"))}. " +
            $"Найпопулярніше заняття: {topName} ({topPart} учасників за всі проведення). " +
            $"Середня заповненість залу склала {(scheds.Any(s => GetIntSafe(s, "maxCapacity") > 0) ? scheds.Where(s => GetIntSafe(s, "maxCapacity") > 0).Average(s => 100.0 * GetIntSafe(s, "participantCount") / GetIntSafe(s, "maxCapacity")) : 0):F0}%.",
            [("Проведено занять", scheds.Count.ToString()), ("Найпопулярніше", topName), ("Учасників (лідер)", topPart.ToString())]);
        Done(path);
    }

    private void ExportMembershipsWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetMemPeriod();
        var mems = _data.TryGetProperty("memberships", out var mp) ? FilterByDate(mp, from, to) : new();
        var top = mems.GroupBy(m => GetStr(m, "typeName")).OrderByDescending(g => g.Count()).FirstOrDefault();
        var path = PickSavePath("memberships_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Звіт по абонементах",
            $"Звітний період: {FmtPeriod(from, to)}",
            $"За період з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} продано {mems.Count} абонементів " +
            $"загальною вартістю {mems.Sum(m => GetDecimalSafe(m, "price")):N0} ₴. " +
            $"Найпопулярніший тип: {top?.Key ?? "немає даних"} ({top?.Count() ?? 0} продажів).",
            [("Продано абонементів", mems.Count.ToString()), ("Дохід", $"{mems.Sum(m => GetDecimalSafe(m, "price")):N0} ₴"), ("Лідер продажів", top?.Key ?? "—")]);
        Done(path);
    }

    private void ExportLoadWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetLdPeriod();
        var scheds = _data.TryGetProperty("schedules", out var sp) ? FilterByDate(sp, from, to) : new();
        var avg = scheds.Count > 0 ? scheds.Average(s => GetIntSafe(s, "participantCount")) : 0;
        var peak = scheds.Count > 0 ? scheds.Max(s => GetIntSafe(s, "participantCount")) : 0;
        var fill = scheds.Any(s => GetIntSafe(s, "maxCapacity") > 0) ? scheds.Where(s => GetIntSafe(s, "maxCapacity") > 0).Average(s => 100.0 * GetIntSafe(s, "participantCount") / GetIntSafe(s, "maxCapacity")) : 0;

        var peakEntry = scheds.OrderByDescending(s => GetIntSafe(s, "participantCount")).FirstOrDefault();
        var peakDay = peakEntry.ValueKind != JsonValueKind.Undefined ? GetDate(peakEntry).ToString("dddd, dd.MM.yyyy") : "—";
        var path = PickSavePath("load_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Звіт по завантаженості залу",
            $"Звітний період: {FmtPeriod(from, to)}",
            $"За період з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} середня кількість учасників на одне заняття становила {avg:F1} осіб. " +
            $"Максимальна заповненість залу фіксувалась {peakDay} і склала {peak} осіб. " +
            $"Середня заповненість відносно максимальної місткості: {fill:F0}%.",
            [("Середньо людей", $"{avg:F1}"), ("Пік", $"{peak} ({peakDay})"), ("Середня заповненість", $"{fill:F0}%")]);
        Done(path);
    }

    private void ExportCancelsWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var (from, to) = GetCnlPeriod();
        var canceledMem = _data.TryGetProperty("canceledMemberships", out var cmp) ? FilterByDate(cmp, from, to) : new();
        var canceledSes = _data.TryGetProperty("canceledSchedules", out var csp) ? FilterByDate(csp, from, to) : new();
        var lost = canceledMem.Sum(m => GetDecimalSafe(m, "price")) + canceledSes.Sum(s => GetDecimalSafe(s, "refundAmount"));
        var path = PickSavePath("cancels_report", "docx");
        if (path == null) return;
        WriteWordReport(path, "Звіт по скасуваннях та втратах",
            $"Звітний період: {FmtPeriod(from, to)}",
            $"За період з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} зафіксовано {canceledMem.Count} скасованих абонементів " +
            $"та {canceledSes.Count} скасованих сеансів. " +
            $"Загальні фінансові втрати від скасувань склали {lost:N0} ₴. " +
            $"Рекомендується проаналізувати причини скасувань та впровадити заходи утримання клієнтів.",
            [("Скасованих абонементів", canceledMem.Count.ToString()), ("Скасованих сеансів", canceledSes.Count.ToString()), ("Загальні втрати", $"{lost:N0} ₴")]);
        Done(path);
    }

    // ══════════════════════════════════════════════════════════
    //  WORD WRITER (plain RTF-as-docx alternative via pure text OOXML-lite)
    //  We write a minimal .docx compatible file using Open XML
    // ══════════════════════════════════════════════════════════
    private static void WriteWordReport(string path, string title, string subtitle, string body, (string Label, string Value)[] rows)
    {
        // Build minimal OOXML document.xml content
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.AppendLine("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">");
        sb.AppendLine("<w:body>");

        // Title
        sb.AppendLine("<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:after=\"120\"/></w:pPr>");
        sb.AppendLine($"<w:r><w:rPr><w:b/><w:sz w:val=\"36\"/><w:color w:val=\"1a1f35\"/></w:rPr><w:t>{EscXml(title)}</w:t></w:r></w:p>");

        // Subtitle
        sb.AppendLine($"<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:after=\"240\"/></w:pPr>");
        sb.AppendLine($"<w:r><w:rPr><w:sz w:val=\"22\"/><w:color w:val=\"6b7280\"/></w:rPr><w:t>{EscXml(subtitle)}</w:t></w:r></w:p>");

        // Key metrics table
        if (rows.Length > 0)
        {
            sb.AppendLine("<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"9000\" w:type=\"dxa\"/><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:color=\"e5e7eb\"/><w:left w:val=\"single\" w:sz=\"4\" w:color=\"e5e7eb\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"e5e7eb\"/><w:right w:val=\"single\" w:sz=\"4\" w:color=\"e5e7eb\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"e5e7eb\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"e5e7eb\"/></w:tblBorders></w:tblPr>");
            foreach (var (label, value) in rows)
            {
                sb.AppendLine("<w:tr>");
                sb.AppendLine($"<w:tc><w:tcPr><w:tcW w:w=\"4500\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"f9fafb\"/></w:tcPr><w:p><w:r><w:rPr><w:sz w:val=\"22\"/><w:color w:val=\"374151\"/></w:rPr><w:t>{EscXml(label)}</w:t></w:r></w:p></w:tc>");
                sb.AppendLine($"<w:tc><w:tcPr><w:tcW w:w=\"4500\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:rPr><w:b/><w:sz w:val=\"22\"/><w:color w:val=\"111827\"/></w:rPr><w:t>{EscXml(value)}</w:t></w:r></w:p></w:tc>");
                sb.AppendLine("</w:tr>");
            }
            sb.AppendLine("</w:tbl>");
            sb.AppendLine("<w:p><w:pPr><w:spacing w:before=\"240\"/></w:pPr></w:p>");
        }

        // Body text
        var sentences = body.Split(". ").Where(s => !string.IsNullOrWhiteSpace(s));
        foreach (var sentence in sentences)
        {
            sb.AppendLine($"<w:p><w:pPr><w:spacing w:after=\"80\"/><w:jc w:val=\"both\"/></w:pPr>");
            sb.AppendLine($"<w:r><w:rPr><w:sz w:val=\"22\"/><w:color w:val=\"1f2937\"/></w:rPr><w:t xml:space=\"preserve\">{EscXml(sentence.Trim())}.</w:t></w:r></w:p>");
        }

        // Footer note
        sb.AppendLine($"<w:p><w:pPr><w:spacing w:before=\"480\"/></w:pPr><w:r><w:rPr><w:sz w:val=\"18\"/><w:color w:val=\"9ca3af\"/></w:rPr><w:t>Сформовано автоматично: {DateTime.Now:dd.MM.yyyy HH:mm}</w:t></w:r></w:p>");

        sb.AppendLine("<w:sectPr><w:pgMar w:top=\"1440\" w:right=\"1080\" w:bottom=\"1440\" w:left=\"1080\"/></w:sectPr>");
        sb.AppendLine("</w:body></w:document>");

        // Build minimal .docx zip
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);

        // [Content_Types].xml
        var ct = zip.CreateEntry("[Content_Types].xml");
        using (var w = new StreamWriter(ct.Open()))
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");

        // _rels/.rels
        var rels = zip.CreateEntry("_rels/.rels");
        using (var w = new StreamWriter(rels.Open()))
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");

        // word/document.xml
        var doc = zip.CreateEntry("word/document.xml");
        using (var w = new StreamWriter(doc.Open()))
            w.Write(sb.ToString());
    }

    private static string EscXml(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");

    // ══════════════════════════════════════════════════════════
    //  UTILS
    // ══════════════════════════════════════════════════════════
    private static bool IsRefund(JsonElement t) => t.TryGetProperty("isRefund", out var v) && v.GetBoolean();
    private static decimal GetAmount(JsonElement t) => t.TryGetProperty("amount", out var v) ? v.GetDecimal() : 0;
    private static DateTime GetDate(JsonElement t) => t.TryGetProperty("date", out var v) && DateTime.TryParse(v.GetString(), out var d) ? d : DateTime.MinValue;
    private static string GetStr(JsonElement t, string key) => t.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
    private static decimal GetDecimalSafe(JsonElement t, string key) => t.TryGetProperty(key, out var v) ? v.GetDecimal() : 0;
    private static int GetIntSafe(JsonElement t, string key) => t.TryGetProperty(key, out var v) ? v.GetInt32() : 0;

    private string GetClientName(string clientId)
    {
        if (!_data.TryGetProperty("clients", out var cp)) return clientId;
        foreach (var c in cp.EnumerateArray())
            if (GetStr(c, "id") == clientId) return GetStr(c, "name");
        return clientId;
    }

    private string GetClientPhone(string clientId)
    {
        if (!_data.TryGetProperty("clients", out var cp)) return "";
        foreach (var c in cp.EnumerateArray())
            if (GetStr(c, "id") == clientId) return GetStr(c, "phone");
        return "";
    }

    private decimal GetPrevPeriodRevenue(DateTime from, DateTime to)
    {
        var span = to - from;
        var pFrom = from - span;
        var pTo = from.AddDays(-1);
        if (!_data.TryGetProperty("transactions", out var tp)) return 0;
        return FilterByDate(tp, pFrom, pTo).Where(t => !IsRefund(t)).Sum(t => GetAmount(t));
    }

    private void ExportOverviewCSV_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var path = PickSavePath("overview_report", "csv");
        if (path == null) return;
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.Write('\uFEFF');
        sw.WriteLine("ЗАГАЛЬНА СТАТИСТИКА ФІТНЕС-КЛУБУ");
        sw.WriteLine($"Сформовано;{DateTime.Now:dd.MM.yyyy HH:mm}");
        sw.WriteLine();
        sw.WriteLine("ФІНАНСИ");
        sw.WriteLine($"Загальний дохід;{_data.GetProperty("totalRevenue").GetDecimal():N0} ₴");
        sw.WriteLine($"Повернення;{_data.GetProperty("totalRefunds").GetDecimal():N0} ₴");
        sw.WriteLine($"Чистий дохід;{_data.GetProperty("netRevenue").GetDecimal():N0} ₴");
        sw.WriteLine($"Дохід цього місяця;{_data.GetProperty("monthRevenue").GetDecimal():N0} ₴");
        sw.WriteLine($"Зростання;{_data.GetProperty("monthGrowthPercent").GetDecimal()}%");
        sw.WriteLine($"Середній чек;{_data.GetProperty("avgPayment").GetDecimal():N0} ₴");
        sw.WriteLine();
        sw.WriteLine("КЛІЄНТИ");
        sw.WriteLine($"Активних абонементів;{_data.GetProperty("activeMembers").GetInt32()}");
        sw.WriteLine($"Нових цього місяця;{_data.GetProperty("newClientsThisMonth").GetInt32()}");
        sw.WriteLine($"Відвідувань за тиждень;{_data.GetProperty("visitsThisWeek").GetInt32()}");
        sw.WriteLine($"Закінчується за 7 днів;{_data.GetProperty("expiringSoon").GetInt32()}");
        sw.WriteLine();
        sw.WriteLine("ДИНАМІКА ПО МІСЯЦЯХ");
        sw.WriteLine("Місяць;Дохід (₴);Повернення (₴);Нових клієнтів;Відвідувань");
        var labels = _data.GetProperty("monthLabels").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var revs = _data.GetProperty("revenueByMonth").EnumerateArray().Select(x => x.GetDecimal()).ToArray();
        var refs = _data.GetProperty("refundsByMonth").EnumerateArray().Select(x => x.GetDecimal()).ToArray();
        var clis = _data.GetProperty("clientsByMonth").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var viss = _data.GetProperty("visitsByMonth").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        for (int i = 0; i < labels.Length; i++)
            sw.WriteLine($"{labels[i]};{revs[i]:N0};{Math.Abs(refs[i]):N0};{clis[i]};{viss[i]}");
        sw.WriteLine();
        sw.WriteLine("МЕТОДИ ОПЛАТИ");
        sw.WriteLine("Метод;Кількість;Сума (₴)");
        foreach (var m in _data.GetProperty("paymentMethods").EnumerateArray())
        {
            var mn = m.GetProperty("method").GetString() switch { "Cash" => "Готівка", "Card" => "Картка", "Online" => "Онлайн", _ => "Інше" };
            sw.WriteLine($"{mn};{m.GetProperty("count").GetInt32()};{m.GetProperty("total").GetDecimal():N0}");
        }
        sw.WriteLine();
        sw.WriteLine("ЗАРПЛАТА ТРЕНЕРІВ");
        sw.WriteLine($"Загальні витрати;{_data.GetProperty("totalPayroll").GetDecimal():N0} ₴");
        sw.WriteLine($"Бонуси;{_data.GetProperty("totalBonus").GetDecimal():N0} ₴");
        sw.WriteLine($"Прибуток після ЗП;{_data.GetProperty("netRevenueAfterPayroll").GetDecimal():N0} ₴");
        sw.WriteLine();
        sw.WriteLine("Тренер;Занять;За заняття (₴);Бонус (₴);Тижнів з бонусом;Всього (₴)");
        foreach (var t in _data.GetProperty("trainerPayroll").EnumerateArray())
            sw.WriteLine($"{t.GetProperty("name").GetString()};{t.GetProperty("totalSchedules").GetInt32()};{t.GetProperty("sessionPay").GetDecimal():N0};{t.GetProperty("bonus").GetDecimal():N0};{t.GetProperty("weeksWithBonus").GetInt32()};{t.GetProperty("totalPay").GetDecimal():N0}");
        Done(path);
    }

    private void ExportOverviewWord_Click(object s, RoutedEventArgs e)
    {
        if (!_hasData) return;
        var path = PickSavePath("overview_report", "docx");
        if (path == null) return;
        var rev = _data.GetProperty("totalRevenue").GetDecimal();
        var net = _data.GetProperty("netRevenue").GetDecimal();
        var payroll = _data.GetProperty("totalPayroll").GetDecimal();
        var g = _data.GetProperty("monthGrowthPercent").GetDecimal();
        var top3t = _data.GetProperty("topTrainers").EnumerateArray().Take(3)
                          .Select(t => $"{t.GetProperty("name").GetString()} ({t.GetProperty("count").GetInt32()} занять)");
        var top3c = _data.GetProperty("topClasses").EnumerateArray().Take(3)
                          .Select(c => $"{c.GetProperty("name").GetString()} ({c.GetProperty("count").GetInt32()})");
        WriteWordReport(path, "Загальний звіт аналітики", $"Сформовано: {DateTime.Now:dd.MM.yyyy HH:mm}",
            $"Загальний дохід фітнес-клубу за весь час становить {rev:N0} ₴, чистий дохід — {net:N0} ₴. " +
            $"Дохід поточного місяця {(g >= 0 ? $"зріс на {g}%" : $"знизився на {Math.Abs(g)}%")} порівняно з попереднім. " +
            $"Витрати на оплату праці тренерів: {payroll:N0} ₴, прибуток після ЗП: {net - payroll:N0} ₴. " +
            $"Топ тренери: {string.Join(", ", top3t)}. Топ заняття: {string.Join(", ", top3c)}.",
            [
                ("Загальний дохід",        $"{rev:N0} ₴"),
                ("Чистий дохід",           $"{net:N0} ₴"),
                ("Дохід цього місяця",     $"{_data.GetProperty("monthRevenue").GetDecimal():N0} ₴"),
                ("Зростання",              $"{g}%"),
                ("Витрати на ЗП",          $"{payroll:N0} ₴"),
                ("Активних абонементів",   _data.GetProperty("activeMembers").GetInt32().ToString()),
                ("Відвідувань за тиждень", _data.GetProperty("visitsThisWeek").GetInt32().ToString()),
            ]);
        Done(path);
    }
    private static string MkInitials(string name)
    {
        var parts = name.Split(' ');
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}".ToUpper() : name.Length > 0 ? name[0].ToString().ToUpper() : "?";
    }

    private static string? PickSavePath(string prefix, string ext)
    {
        var dlg = new SaveFileDialog
        {
            Filter = ext == "csv" ? "CSV файл (*.csv)|*.csv" : "Word документ (*.docx)|*.docx",
            FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmm}.{ext}",
            Title = "Зберегти звіт",
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static void Done(string path) =>
        MessageBox.Show($"Файл збережено:\n{path}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
}

// ══════════════════════════════════════════════════════════
//  DATA MODELS
// ══════════════════════════════════════════════════════════
public class TopItem
{
    public string Rank { get; set; } = "";
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public string CountText { get; set; } = "";
    public double BarWidth { get; set; }
}

public class PayrollItem
{
    public string Name { get; set; } = "";
    public string Initials { get; set; } = "";
    public int TotalSchedules { get; set; }
    public string SessionPayText { get; set; } = "";
    public string BonusText { get; set; } = "";
    public string TotalPayText { get; set; } = "";
    public bool HasBonus { get; set; }
    public string WeeksWithBonusText { get; set; } = "";
    public string RateInfo { get; set; } = "";
}

public class TransactionItem
{
    public string DateStr { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Method { get; set; } = "";
    public string StatusText { get; set; } = "";
    public Brush StatusBg { get; set; } = Brushes.Transparent;
    public Brush StatusFg { get; set; } = Brushes.White;
    public string AmountStr { get; set; } = "";
    public Brush AmountColor { get; set; } = Brushes.White;
}

public class ActiveClientItem
{
    public string Rank { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Initials { get; set; } = "";
    public string VisitCount { get; set; } = "";
    public string LastVisit { get; set; } = "";
    public string TotalPaid { get; set; } = "";
}

public class AllTimeClientItem
{
    public string Rank { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Initials { get; set; } = "";
    public string VisitCount { get; set; } = "";
    public string MembershipCount { get; set; } = "";
    public string FirstVisit { get; set; } = "";
    public string TotalPaid { get; set; } = "";
}

public class SessionDetailItem
{
    public string Name { get; set; } = "";
    public string TrainerName { get; set; } = "";
    public string SessionCount { get; set; } = "";
    public string TotalParticipants { get; set; } = "";
    public string FillRateStr { get; set; } = "";
    public Brush FillColor { get; set; } = Brushes.Gray;
    public double FillBarWidth { get; set; }
    public string Trend { get; set; } = "";
    public Brush TrendColor { get; set; } = Brushes.Gray;
}

public class MembershipDetailItem
{
    public string Name { get; set; } = "";
    public string PriceStr { get; set; } = "";
    public string SoldCount { get; set; } = "";
    public string Revenue { get; set; } = "";
    public string ShareStr { get; set; } = "";
    public Brush AccentColor { get; set; } = Brushes.Gray;
}

public class CancelItem
{
    public string DateStr { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Category { get; set; } = "";
    public Brush CategoryBg { get; set; } = Brushes.Transparent;
    public Brush CategoryFg { get; set; } = Brushes.White;
    public string LostAmount { get; set; } = "";
}
