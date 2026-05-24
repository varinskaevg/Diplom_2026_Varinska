using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Dialogs;

namespace FitnessClub.Desktop.Views.Pages;

public partial class TrainerDashboardPage : Page
{
    private List<ScheduleItem> _items = [];
    private ScheduleItem? _selectedItem;
    private int? _trainerId;
    private bool _weekView = false;
    private readonly DispatcherTimer _pendingTimer;

    // Клієнти тренера
    private List<TrainerClientRow> _allClients = [];
    private bool _showOnlyActive = true;
    private int? _selectedTrainerClientId;

    public TrainerDashboardPage()
    {
        InitializeComponent();
        GreetingText.Text = $"Привіт, {AppSession.FirstName}! 👋";
        DateText.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy",
            new System.Globalization.CultureInfo("uk-UA"));

        _pendingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _pendingTimer.Tick += async (_, _) => await CheckPendingCount();

        Loaded += async (_, _) => await Init();
        Unloaded += (_, _) => _pendingTimer.Stop();
    }

    private async Task Init()
    {
        try
        {
            var me = await ApiClient.GetAsync<JsonElement>("api/trainers/me");
            _trainerId = me.GetProperty("id").GetInt32();
            await LoadStats();
            await LoadSchedule();
            await CheckPendingCount();
            _pendingTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка ініціалізації: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ВКЛАДКИ
    // ══════════════════════════════════════════════════════════
    private void SetTabActive(Button active, params Button[] inactive)
    {
        active.Background = new SolidColorBrush(Color.FromRgb(108, 99, 255));
        active.Foreground = Brushes.White;
        foreach (var b in inactive)
        {
            b.Background = new SolidColorBrush(Color.FromRgb(26, 31, 53));
            b.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
        }
    }

    private void TabSchedule_Click(object sender, RoutedEventArgs e)
    {
        ScheduleTab.Visibility = Visibility.Visible;
        ClientsTab.Visibility = Visibility.Collapsed;
        RequestsTab.Visibility = Visibility.Collapsed;
        SetTabActive(TabScheduleBtn, TabClientsBtn, TabRequestsBtn);
    }

    private async void TabClients_Click(object sender, RoutedEventArgs e)
    {
        ScheduleTab.Visibility = Visibility.Collapsed;
        ClientsTab.Visibility = Visibility.Visible;
        RequestsTab.Visibility = Visibility.Collapsed;
        SetTabActive(TabClientsBtn, TabScheduleBtn, TabRequestsBtn);
        await LoadTrainerClients();
    }

    private async void TabRequests_Click(object sender, RoutedEventArgs e)
    {
        ScheduleTab.Visibility = Visibility.Collapsed;
        ClientsTab.Visibility = Visibility.Collapsed;
        RequestsTab.Visibility = Visibility.Visible;
        SetTabActive(TabRequestsBtn, TabScheduleBtn, TabClientsBtn);
        await LoadPendingRequests();
    }

    private async void PendingBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ScheduleTab.Visibility = Visibility.Collapsed;
        ClientsTab.Visibility = Visibility.Collapsed;
        RequestsTab.Visibility = Visibility.Visible;
        SetTabActive(TabRequestsBtn, TabScheduleBtn, TabClientsBtn);
        await LoadPendingRequests();
    }

    private async void RefreshRequests_Click(object sender, RoutedEventArgs e)
        => await LoadPendingRequests();

    // ══════════════════════════════════════════════════════════
    //  СТАТИСТИКА
    // ══════════════════════════════════════════════════════════
    private async Task LoadStats()
    {
        if (_trainerId == null) return;
        try
        {
            var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var to = DateTime.Today;
            var salary = await ApiClient.GetAsync<JsonElement>(
                $"api/trainers/{_trainerId}/salary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

            MonthCompletedText.Text = salary.GetProperty("totalSessions").GetInt32().ToString();
            MonthEarningsText.Text = $"{salary.GetProperty("grandTotal").GetDecimal():N0} ₴";
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════
    //  МОЇ КЛІЄНТИ — завантаження
    // ══════════════════════════════════════════════════════════
    private async Task LoadTrainerClients()
    {
        if (_trainerId == null) return;
        try
        {
            var data = await ApiClient.GetAsync<List<JsonElement>>(
                $"api/trainer-clients?trainerId={_trainerId}");

            _allClients = (data ?? []).Select(c =>
            {
                var isActive = c.GetProperty("isActive").GetBoolean();
                var paymentType = c.GetProperty("paymentType").GetString() ?? "single";
                var rate = c.GetProperty("rate").GetDecimal();
                var totalPaid = c.GetProperty("totalPaid").GetDecimal();
                var paymentCount = c.GetProperty("paymentCount").GetInt32();
                var isOverdue = c.TryGetProperty("isOverdue", out var od) && od.GetBoolean();
                DateTime? nextPayment = null;
                if (c.TryGetProperty("nextPaymentDate", out var np) && np.ValueKind != JsonValueKind.Null)
                    nextPayment = np.GetDateTime();

                return new TrainerClientRow
                {
                    Id = c.GetProperty("id").GetInt32(),
                    TrainerId = c.GetProperty("trainerId").GetInt32(),
                    ClientId = c.GetProperty("clientId").GetInt32(),
                    ClientName = c.GetProperty("clientName").GetString() ?? "",
                    ClientPhone = c.TryGetProperty("clientPhone", out var ph) ? ph.GetString() ?? "" : "",
                    Initials = c.TryGetProperty("initials", out var ini) ? ini.GetString() ?? "?" : "?",
                    PaymentType = paymentType,
                    Rate = rate,
                    IsActive = isActive,
                    TotalPaid = totalPaid,
                    PaymentCount = paymentCount,
                    NextPaymentDate = nextPayment,
                    IsOverdue = isOverdue,
                };
            }).ToList();

            RefreshClientsList();

            // Оновити лічильник у статистиці
            var activeCount = _allClients.Count(c => c.IsActive);
            TodayClientsText.Text = activeCount.ToString();

            if (activeCount > 0)
            {
                ClientsBadge.Visibility = Visibility.Visible;
                ClientsBadgeCount.Text = activeCount.ToString();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження клієнтів: {ex.Message}");
        }
    }

    private void RefreshClientsList()
    {
        var filtered = _showOnlyActive
            ? _allClients.Where(c => c.IsActive).ToList()
            : _allClients;

        TrainerClientsList.ItemsSource = filtered;
        ClientsSubtitle.Text = _showOnlyActive
            ? $"{filtered.Count} активних"
            : $"{filtered.Count} всього ({_allClients.Count(c => c.IsActive)} активних)";
    }

    private void FilterActive_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyActive = true;
        FilterActiveBtn.Background = new SolidColorBrush(Color.FromRgb(108, 99, 255));
        FilterActiveBtn.Foreground = Brushes.White;
        FilterAllBtn.Background = Brushes.Transparent;
        FilterAllBtn.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
        RefreshClientsList();
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyActive = false;
        FilterAllBtn.Background = new SolidColorBrush(Color.FromRgb(108, 99, 255));
        FilterAllBtn.Foreground = Brushes.White;
        FilterActiveBtn.Background = Brushes.Transparent;
        FilterActiveBtn.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
        RefreshClientsList();
    }

    // ══════════════════════════════════════════════════════════
    //  МОЇ КЛІЄНТИ — вибір клієнта → показати платежі
    // ══════════════════════════════════════════════════════════
    private async void TrainerClient_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is TrainerClientRow row)
            await LoadClientPayments(row);
    }

    private async Task LoadClientPayments(TrainerClientRow row)
    {
        _selectedTrainerClientId = row.Id;
        SelectedClientName.Text = row.ClientName;

        // Заповнити KPI
        DetailPaymentType.Text = row.PaymentTypeLabel;
        DetailRate.Text = row.RateText;
        DetailTotalPaid.Text = row.TotalPaidText;
        DetailPaymentCount.Text = row.PaymentCount.ToString();
        ClientDetailPanel.Visibility = Visibility.Visible;

        try
        {
            var data = await ApiClient.GetAsync<JsonElement>($"api/trainer-clients/{row.Id}/payments");
            var payments = data.GetProperty("payments").EnumerateArray()
                .Select(p => new ClientPaymentRow
                {
                    Id = p.GetProperty("id").GetInt32(),
                    PaidAt = p.GetProperty("paidAt").GetDateTime(),
                    Amount = p.GetProperty("amount").GetDecimal(),
                    PaymentMethod = p.GetProperty("paymentMethod").GetString() ?? "",
                    Note = p.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null
                        ? n.GetString() ?? "" : "",
                }).ToList();

            ClientPaymentsList.ItemsSource = payments;
            NoPaymentsText.Visibility = payments.Any() ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════
    //  МОЇ КЛІЄНТИ — прив'язати
    // ══════════════════════════════════════════════════════════
    private async void AddTrainerClient_Click(object sender, RoutedEventArgs e)
    {
        if (_trainerId == null) return;
        var dialog = new AddTrainerClientDialog(_trainerId.Value);
        if (dialog.ShowDialog() == true)
            await LoadTrainerClients();
    }

    // ══════════════════════════════════════════════════════════
    //  МОЇ КЛІЄНТИ — прийняти оплату
    // ══════════════════════════════════════════════════════════
    private async void PayTrainerClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int tcId) return;
        var row = _allClients.FirstOrDefault(c => c.Id == tcId);
        if (row == null) return;

        var dialog = new TrainerClientPayDialog(tcId, row.ClientName, row.Rate, row.PaymentType);
        if (dialog.ShowDialog() == true)
        {
            await LoadTrainerClients();
            // Оновити деталі якщо це вибраний клієнт
            if (_selectedTrainerClientId == tcId)
                await LoadClientPayments(row);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  МОЇ КЛІЄНТИ — деактивувати
    // ══════════════════════════════════════════════════════════
    private async void DeactivateTrainerClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int tcId) return;
        var row = _allClients.FirstOrDefault(c => c.Id == tcId);
        if (row == null) return;

        if (MessageBox.Show($"Завершити прив'язку клієнта {row.ClientName}?\nІсторія платежів збережеться.",
            "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await ApiClient.DeleteAsync($"api/trainer-clients/{tcId}");
            await LoadTrainerClients();
        }
        catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
    }

    // ══════════════════════════════════════════════════════════
    //  PENDING ЗАПИТИ (без змін)
    // ══════════════════════════════════════════════════════════
    private async Task CheckPendingCount()
    {
        if (_trainerId == null) return;
        try
        {
            var requests = await ApiClient.GetAsync<List<JsonElement>>(
                $"api/bot/pending-bookings?trainerId={_trainerId}");
            var count = requests?.Count ?? 0;

            Dispatcher.Invoke(() =>
            {
                if (count > 0)
                {
                    PendingBadge.Visibility = Visibility.Visible;
                    PendingCountText.Text = count.ToString();
                    RequestsTabBadge.Visibility = Visibility.Visible;
                    RequestsTabCount.Text = count.ToString();
                }
                else
                {
                    PendingBadge.Visibility = Visibility.Collapsed;
                    RequestsTabBadge.Visibility = Visibility.Collapsed;
                }
            });
        }
        catch { }
    }

    private async Task LoadPendingRequests()
    {
        if (_trainerId == null) return;
        try
        {
            var requests = await ApiClient.GetAsync<List<JsonElement>>(
                $"api/bot/pending-bookings?trainerId={_trainerId}");

            var rows = (requests ?? []).Select(r =>
            {
                var clientName = r.GetProperty("clientName").GetString() ?? "";
                var bookedAt = r.GetProperty("bookedAt").GetDateTime();
                var scheduleDate = r.GetProperty("scheduleDate").GetDateTime().ToLocalTime();

                return new PendingBookingRow
                {
                    Id = r.GetProperty("id").GetInt32(),
                    ClientName = clientName,
                    ClientPhone = r.TryGetProperty("clientPhone", out var ph) && ph.ValueKind != JsonValueKind.Null
                        ? ph.GetString() : null,
                    ClassName = r.GetProperty("className").GetString() ?? "",
                    ScheduleDateStr = scheduleDate.ToString("dd.MM.yyyy"),
                    TimeStr = $"{r.GetProperty("startTime").GetString()} – {r.GetProperty("endTime").GetString()}",
                    Room = r.TryGetProperty("room", out var rm) && rm.ValueKind != JsonValueKind.Null
                        ? rm.GetString() : null,
                    BookedAtStr = GetTimeAgo(bookedAt),
                    Initials = GetInitials(clientName)
                };
            }).ToList();

            PendingRequestsList.ItemsSource = rows;
            NoPendingText.Visibility = rows.Any() ? Visibility.Collapsed : Visibility.Visible;

            if (rows.Count > 0)
            {
                PendingBadge.Visibility = Visibility.Visible;
                PendingCountText.Text = rows.Count.ToString();
                RequestsTabBadge.Visibility = Visibility.Visible;
                RequestsTabCount.Text = rows.Count.ToString();
            }
            else
            {
                PendingBadge.Visibility = Visibility.Collapsed;
                RequestsTabBadge.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження запитів: {ex.Message}");
        }
    }

    private async void ApproveBooking_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int bookingId) return;
        var row = ((List<PendingBookingRow>)PendingRequestsList.ItemsSource)
            .FirstOrDefault(r => r.Id == bookingId);
        if (row == null) return;
        var result = MessageBox.Show(
            $"Підтвердити запис клієнта {row.ClientName}\nна {row.ClassName} ({row.ScheduleDateStr} {row.TimeStr})?",
            "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            btn.IsEnabled = false;
            await ApiClient.PostAsync<object>($"api/bot/booking/{bookingId}/approve", null);
            await LoadPendingRequests();
            await LoadSchedule(_weekView);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
            btn.IsEnabled = true;
        }
    }

    private async void RejectBooking_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int bookingId) return;
        var row = ((List<PendingBookingRow>)PendingRequestsList.ItemsSource)
            .FirstOrDefault(r => r.Id == bookingId);
        if (row == null) return;
        var result = MessageBox.Show(
            $"Відхилити запис клієнта {row.ClientName}\nна {row.ClassName}?",
            "Відхилити запис", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            btn.IsEnabled = false;
            await ApiClient.PostAsync<object>($"api/bot/booking/{bookingId}/reject",
                new { Reason = (string?)null });
            await LoadPendingRequests();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
            btn.IsEnabled = true;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  РОЗКЛАД (без змін)
    // ══════════════════════════════════════════════════════════
    private async Task LoadSchedule(bool weekView = false)
    {
        _weekView = weekView;
        try
        {
            string url = weekView
                ? $"api/schedules?weekStart={GetMonday(DateTime.Today):yyyy-MM-dd}"
                : $"api/schedules?date={DateTime.Today:yyyy-MM-dd}";

            var all = await ApiClient.GetAsync<List<ScheduleItem>>(url);
            _items = (all ?? []).Where(s => s.TrainerId == _trainerId)
                .OrderBy(s => s.StartDatetime).ToList();

            ScheduleList.ItemsSource = _items;
            TodayCountText.Text = _items.Count.ToString();

            if (_selectedItem != null)
            {
                var updated = _items.FirstOrDefault(i => i.Id == _selectedItem.Id);
                if (updated != null) await SelectSchedule(updated);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження розкладу: {ex.Message}");
        }
    }

    private async Task SelectSchedule(ScheduleItem item)
    {
        _selectedItem = item;
        SelectedClassText.Text = $"{item.ClassName} · {item.StartTime} – {item.EndTime}";
        CapacityPanel.Visibility = Visibility.Visible;
        UpdateCapacityBar(item);
        await LoadBookings(item.Id);
    }

    private void UpdateCapacityBar(ScheduleItem item)
    {
        BookedCountText.Text = item.BookingsCount.ToString();
        MaxCountText.Text = item.MaxCapacity > 0 ? $" / {item.MaxCapacity}" : "";

        if (item.MaxCapacity > 0)
        {
            var ratio = (double)item.BookingsCount / item.MaxCapacity;
            CapacityBar.Width = Math.Min(ratio * 316, 316);
            if (ratio >= 1)
            {
                CapacityStatusText.Text = "🔴 Зал заповнений";
                CapacityStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f87171"));
            }
            else if (ratio >= 0.8)
            {
                CapacityStatusText.Text = $"🟡 Залишилось {item.MaxCapacity - item.BookingsCount} місць";
                CapacityStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
            }
            else
            {
                CapacityStatusText.Text = $"🟢 Вільно {item.MaxCapacity - item.BookingsCount} місць";
                CapacityStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34d399"));
            }
        }
        else
        {
            CapacityBar.Width = 0;
            CapacityStatusText.Text = "Необмежена кількість місць";
            CapacityStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280"));
        }
    }

    private async Task LoadBookings(int scheduleId)
    {
        try
        {
            var bookings = await ApiClient.GetAsync<List<JsonElement>>($"api/bookings?scheduleId={scheduleId}");
            var rows = (bookings ?? [])
                .Where(b => b.GetProperty("status").GetString() != "Cancelled")
                .Select(b =>
                {
                    var extraCharge = b.TryGetProperty("extraCharge", out var ec) ? ec.GetDecimal() : 0m;
                    var chargeReason = b.TryGetProperty("chargeReason", out var cr) && cr.ValueKind != JsonValueKind.Null
                        ? cr.GetString() : null;
                    var source = b.TryGetProperty("source", out var src) ? src.GetString() : "Manual";
                    return new BookingRow
                    {
                        BookingId = b.GetProperty("id").GetInt32(),
                        ClientName = b.GetProperty("clientName").GetString() ?? "",
                        ClientPhone = b.TryGetProperty("clientPhone", out var ph) && ph.ValueKind != JsonValueKind.Null
                            ? ph.GetString() : null,
                        MembershipInfo = extraCharge > 0
                            ? (chargeReason?.Split(" | ")[0] ?? "Без абонементу")
                            : "Абонемент активний",
                        NeedsPayment = extraCharge > 0,
                        ExtraCharge = extraCharge,
                        Source = source ?? "Manual"
                    };
                }).ToList();

            BookingsList.ItemsSource = rows;
            NoBookingsText.Visibility = rows.Any() ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }

    private async void Schedule_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is ScheduleItem item)
            await SelectSchedule(item);
    }

    private async void BookClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int scheduleId) return;
        var item = _items.FirstOrDefault(i => i.Id == scheduleId);
        if (item == null) return;
        var dialog = new BookClientDialog(scheduleId, item.ClassName, item.MaxCapacity, item.BookingsCount);
        if (dialog.ShowDialog() == true)
        {
            await LoadSchedule(_weekView);
            if (_selectedItem?.Id == scheduleId)
                await LoadBookings(scheduleId);
        }
    }

    private async void CancelBooking_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int bookingId) return;
        if (MessageBox.Show("Скасувати запис клієнта?", "Підтвердження",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await ApiClient.DeleteAsync($"api/bookings/{bookingId}");
            if (_selectedItem != null)
            {
                _selectedItem.BookingsCount = Math.Max(0, _selectedItem.BookingsCount - 1);
                UpdateCapacityBar(_selectedItem);
                await LoadBookings(_selectedItem.Id);
                await LoadSchedule(_weekView);
            }
        }
        catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
    }

    private async void TodayView_Click(object sender, RoutedEventArgs e) => await LoadSchedule(false);
    private async void WeekView_Click(object sender, RoutedEventArgs e) => await LoadSchedule(true);

    private static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        if (diff.TotalMinutes < 1) return "щойно";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} хв тому";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} год тому";
        return time.ToLocalTime().ToString("dd.MM HH:mm");
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
            : name[0].ToString().ToUpper();
    }
}

// ══════════════════════════════════════════════════════════
//  НОВІ DTOs
// ══════════════════════════════════════════════════════════
public class TrainerClientRow
{
    public int Id { get; set; }
    public int TrainerId { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public string Initials { get; set; } = "?";
    public string PaymentType { get; set; } = "single";
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalPaid { get; set; }
    public int PaymentCount { get; set; }
    public DateTime? NextPaymentDate { get; set; }
    public bool IsOverdue { get; set; }

    public string PaymentTypeLabel => PaymentType switch
    {
        "weekly" => "📅 Тижнева",
        "monthly" => "📆 Місячна",
        _ => "1️⃣ Разова"
    };

    public string RateText => $"{Rate:N0} ₴";
    public string TotalPaidText => $"{TotalPaid:N0} ₴";

    public string NextPaymentText => PaymentType == "single"
        ? $"Платежів: {PaymentCount}"
        : NextPaymentDate.HasValue
            ? (IsOverdue
                ? $"⚠ Протерміновано: {NextPaymentDate.Value:dd.MM.yyyy}"
                : $"Наступний: {NextPaymentDate.Value:dd.MM.yyyy}")
            : "";

    public string OverdueBadgeVisibility => IsOverdue && IsActive ? "Visible" : "Collapsed";
}

public class ClientPaymentRow
{
    public int Id { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Note { get; set; } = "";

    public string PaidAtStr => PaidAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public string AmountStr => $"+{Amount:N0} ₴";
}

// ══════════════════════════════════════════════════════════
//  ІСНУЮЧІ DTOs (без змін)
// ══════════════════════════════════════════════════════════
public class BookingRow
{
    public int BookingId { get; set; }
    public string ClientName { get; set; } = "";
    public string? ClientPhone { get; set; }
    public string MembershipInfo { get; set; } = "";
    public bool NeedsPayment { get; set; }
    public decimal ExtraCharge { get; set; }
    public string Source { get; set; } = "Manual";

    public string ChargeText => ExtraCharge > 0 ? $"{ExtraCharge:N0} ₴" : "";
    public Visibility TelegramBadgeVisibility => Source == "Telegram" ? Visibility.Visible : Visibility.Collapsed;
    public string Initials => ClientName.Length > 1
        ? string.Join("", ClientName.Split(' ').Take(2).Where(w => w.Length > 0).Select(w => w[0]))
        : ClientName.FirstOrDefault().ToString();
}

public class PendingBookingRow
{
    public int Id { get; set; }
    public string ClientName { get; set; } = "";
    public string? ClientPhone { get; set; }
    public string ClassName { get; set; } = "";
    public string ScheduleDateStr { get; set; } = "";
    public string TimeStr { get; set; } = "";
    public string? Room { get; set; }
    public string BookedAtStr { get; set; } = "";
    public string Initials { get; set; } = "";
}