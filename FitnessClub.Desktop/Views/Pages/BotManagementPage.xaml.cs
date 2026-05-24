using FitnessClub.Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;


namespace FitnessClub.Desktop.Views.Pages;

public partial class BotManagementPage : Page
{
    private readonly DispatcherTimer _refreshTimer;
    private long? _selectedChatId;
    private bool _showOnlyUnread = false;

    public BotManagementPage()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _refreshTimer.Tick += async (_, _) => await RefreshCurrentTab();

        Loaded += async (_, _) =>
        {
            await LoadBotStatus();
            await LoadStats();
            _refreshTimer.Start();
        };

        Unloaded += (_, _) => _refreshTimer.Stop();

        BroadcastText.TextChanged += (_, _) => UpdateBroadcastPreview();
    }

    // ══════════════════════════════════════════════════════════
    //  ВКЛАДКИ
    // ══════════════════════════════════════════════════════════
    private async void TabStats_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabStatsBtn);
        ShowTab(StatsTab);
        await LoadStats();
    }

    private async void TabChats_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabChatsBtn);
        ShowTab(ChatsTab);
        await LoadChats();
    }

    private async void TabBroadcast_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabBroadcastBtn);
        ShowTab(BroadcastTab);
        await LoadBroadcastHistory();
    }

    private void TabSettings_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabSettingsBtn);
        ShowTab(SettingsTab);
    }

    private void SetActiveTab(Button activeBtn)
    {
        TabStatsBtn.Style = (Style)Resources["TabButtonStyle"];
        TabChatsBtn.Style = (Style)Resources["TabButtonStyle"];
        TabBroadcastBtn.Style = (Style)Resources["TabButtonStyle"];
        TabSettingsBtn.Style = (Style)Resources["TabButtonStyle"];
        activeBtn.Style = (Style)Resources["ActiveTabStyle"];
    }

    private void ShowTab(UIElement tab)
    {
        StatsTab.Visibility = Visibility.Collapsed;
        ChatsTab.Visibility = Visibility.Collapsed;
        BroadcastTab.Visibility = Visibility.Collapsed;
        SettingsTab.Visibility = Visibility.Collapsed;
        tab.Visibility = Visibility.Visible;
    }

    private async Task RefreshCurrentTab()
    {
        if (StatsTab.Visibility == Visibility.Visible)
            await LoadStats();
        else if (ChatsTab.Visibility == Visibility.Visible)
            await LoadChats();
    }

    // ══════════════════════════════════════════════════════════
    //  СТАТУС БОТА
    // ══════════════════════════════════════════════════════════
    private async Task LoadBotStatus()
    {
        try
        {
            var status = await ApiClient.GetAsync<BotStatusDto>("api/bot/status");
            if (status == null) return;

            BotStatusText.Text = status.IsRunning
                ? $"Статус: 🟢 Онлайн | @{status.Username}"
                : "Статус: 🔴 Офлайн";

            BotStatusIndicator.Fill = status.IsRunning
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : new SolidColorBrush(Color.FromRgb(239, 68, 68));

            BotStatusLabel.Text = status.IsRunning ? "Онлайн" : "Офлайн";
            BotStatusLabel.Foreground = status.IsRunning
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : new SolidColorBrush(Color.FromRgb(239, 68, 68));

            ToggleBotBtn.Content = status.IsRunning ? "⏸ Зупинити" : "▶️ Запустити";
            BotUsernameText.Text = $"@{status.Username}";
            BotIdText.Text = status.BotId.ToString();
            BotStartedText.Text = status.StartedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—";
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════
    //  СТАТИСТИКА
    // ══════════════════════════════════════════════════════════
    private async Task LoadStats()
    {
        try
        {
            var stats = await ApiClient.GetAsync<BotStatsDto>("api/bot/stats");
            if (stats == null) return;

            TotalUsersText.Text = stats.TotalUsers.ToString();
            NewUsersText.Text = $"+{stats.NewUsersThisWeek} за тиждень";
            TodayMessagesText.Text = stats.TodayMessages.ToString();
            WeekMessagesText.Text = $"{stats.WeekMessages} за тиждень";
            QrRequestsText.Text = stats.TodayQrRequests.ToString();
            SupportRequestsText.Text = stats.TotalSupportRequests.ToString();
            UnreadSupportText.Text = $"{stats.UnreadSupportRequests} непрочитаних";

            // Популярні команди
            var maxCount = stats.CommandStats.MaxBy(c => c.Count)?.Count ?? 1;
            CommandStatsList.ItemsSource = stats.CommandStats.Select(c => new
            {
                c.Command,
                c.Count,
                BarWidth = (double)c.Count / maxCount * 200
            }).ToList();

            // Останні дії
            RecentActionsList.ItemsSource = stats.RecentActions.Select(a => new
            {
                Emoji = GetActionEmoji(a.Action),
                a.UserName,
                Action = GetActionText(a.Action),
                TimeAgo = GetTimeAgo(a.Time)
            }).ToList();
        }
        catch { }
    }

    private static string GetActionEmoji(string action) => action switch
    {
        "qr" => "🎫",
        "membership" => "💳",
        "schedule" => "📅",
        "support" => "📩",
        "start" => "👋",
        _ => "💬"
    };

    private static string GetActionText(string action) => action switch
    {
        "qr" => "запросив QR-код",
        "membership" => "переглянув абонемент",
        "schedule" => "переглянув розклад",
        "support" => "написав в підтримку",
        "start" => "зареєструвався",
        _ => "надіслав повідомлення"
    };

    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        if (diff.TotalMinutes < 1) return "щойно";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} хв тому";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} год тому";
        return time.ToLocalTime().ToString("dd.MM HH:mm");
    }

    // ══════════════════════════════════════════════════════════
    //  ПЕРЕПИСКИ
    // ══════════════════════════════════════════════════════════
    private async Task LoadChats()
    {
        try
        {
            var chats = await ApiClient.GetAsync<List<SupportChatDto>>("api/bot/support/chats");
            if (chats == null) return;

            var filtered = _showOnlyUnread
                ? chats.Where(c => c.UnreadCount > 0).ToList()
                : chats;

            ChatsList.ItemsSource = filtered.Select(c => new
            {
                c.ChatId,
                Initials = GetInitials(c.UserName),
                c.UserName,
                LastMessage = c.LastMessage?.Length > 40
                    ? c.LastMessage[..40] + "..."
                    : c.LastMessage,
                TimeStr = GetTimeAgo(c.LastMessageTime),
                c.UnreadCount,
                UnreadVisibility = c.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed,
                Background = c.ChatId == _selectedChatId ? "#2d2d3a" : "#1a1a2e"
            }).ToList();
        }
        catch { }
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyUnread = false;
        FilterAllBtn.Background = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        FilterUnreadBtn.Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));
        _ = LoadChats();
    }

    private void FilterUnread_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyUnread = true;
        FilterUnreadBtn.Background = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        FilterAllBtn.Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));
        _ = LoadChats();
    }

    private async void ChatsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChatsList.SelectedItem == null) return;

        dynamic selected = ChatsList.SelectedItem;
        _selectedChatId = selected.ChatId;

        await LoadChatMessages(_selectedChatId.Value);
        await LoadChats(); // Оновити список для підсвітки
    }

    private async Task LoadChatMessages(long chatId)
    {
        try
        {
            var messages = await ApiClient.GetAsync<SupportChatDetailDto>($"api/bot/support/chat/{chatId}");
            if (messages == null) return;

            NoChatSelected.Visibility = Visibility.Collapsed;
            ChatHeaderPanel.Visibility = Visibility.Visible;
            ReplyPanel.Visibility = Visibility.Visible;

            ChatUserName.Text = messages.UserName;
            ChatUserInfo.Text = messages.Phone ?? "Телефон не вказано";

            MessagesList.ItemsSource = messages.Messages.Select(m => new
            {
                m.Text,
                TimeStr = m.Time.ToLocalTime().ToString("HH:mm"),
                Background = m.IsFromAdmin ? "#6366f1" : "#1e1e2e",
                Alignment = m.IsFromAdmin ? HorizontalAlignment.Right : HorizontalAlignment.Left
            }).ToList();

            // Скрол вниз
            MessagesScroll.ScrollToEnd();

            // Позначити як прочитане
            await ApiClient.PostAsync<object>($"api/bot/support/chat/{chatId}/read", null);
        }
        catch { }
    }

    private async void SendReply_Click(object sender, RoutedEventArgs e)
    {
        await SendReply();
    }

    private async void ReplyInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            await SendReply();
        }
    }

    private async Task SendReply()
    {
        if (_selectedChatId == null || string.IsNullOrWhiteSpace(ReplyInput.Text)) return;

        var text = ReplyInput.Text.Trim();
        ReplyInput.Text = "";

        try
        {
            await ApiClient.PostAsync<object>("api/bot/support/reply", new
            {
                ChatId = _selectedChatId.Value,
                Message = text
            });

            await LoadChatMessages(_selectedChatId.Value);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка надсилання: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  РОЗСИЛКА
    // ══════════════════════════════════════════════════════════
    private void UpdateBroadcastPreview()
    {
        BroadcastPreview.Text = BroadcastText.Text;
    }

    private void PreviewBroadcast_Click(object sender, RoutedEventArgs e)
    {
        UpdateBroadcastPreview();
    }

    private async void SendBroadcast_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BroadcastText.Text))
        {
            MessageBox.Show("Введіть текст повідомлення", "Увага",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "Ви впевнені, що хочете надіслати розсилку?",
            "Підтвердження",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        BroadcastProgress.Visibility = Visibility.Visible;
        BroadcastProgressText.Text = "Надсилання...";
        BroadcastProgressBar.Value = 0;

        try
        {
            var targetType = BroadcastAll.IsChecked == true ? "all"
                : BroadcastActive.IsChecked == true ? "active"
                : "expiring";

            var response = await ApiClient.PostAsync<BroadcastResultDto>("api/bot/broadcast", new
            {
                Message = BroadcastText.Text,
                TargetType = targetType
            });

            BroadcastProgressBar.Value = 100;
            BroadcastProgressText.Text = $"✅ Надіслано: {response?.SentCount ?? 0} повідомлень";

            BroadcastText.Text = "";
            await LoadBroadcastHistory();
        }
        catch (Exception ex)
        {
            BroadcastProgressText.Text = $"❌ Помилка: {ex.Message}";
        }
    }

    private async Task LoadBroadcastHistory()
    {
        try
        {
            var history = await ApiClient.GetAsync<List<BroadcastHistoryDto>>("api/bot/broadcast/history");
            if (history == null) return;

            BroadcastHistoryList.ItemsSource = history.Select(h => new
            {
                Text = h.Message.Length > 50 ? h.Message[..50] + "..." : h.Message,
                DateStr = h.SentAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                SentCount = $"✅ {h.SentCount} отримувачів"
            }).ToList();
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════
    //  НАЛАШТУВАННЯ
    // ══════════════════════════════════════════════════════════
    private async void ToggleBot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApiClient.PostAsync<object>("api/bot/toggle", null);
            await LoadBotStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestartBot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApiClient.PostAsync<object>("api/bot/restart", null);
            MessageBox.Show("Бота перезапущено", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadBotStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApiClient.PostAsync<object>("api/bot/clear-cache", null);
            MessageBox.Show("Кеш очищено", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApiClient.PostAsync<object>("api/bot/settings", new
            {
                NotifyExpiry = NotifyExpiryCheck.IsChecked ?? false,
                NotifyNewSchedule = NotifyNewScheduleCheck.IsChecked ?? false,
                NotifyPromos = NotifyPromosCheck.IsChecked ?? false
            });
            MessageBox.Show("Налаштування збережено", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportStats_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var data = await ApiClient.GetAsync<byte[]>("api/bot/export-stats");
            if (data == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"bot_stats_{DateTime.Now:yyyyMMdd}.csv",
                DefaultExt = ".csv",
                Filter = "CSV файли|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dialog.FileName, data);
                MessageBox.Show("Статистику експортовано", "Успіх",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
//  DTOs
// ══════════════════════════════════════════════════════════
public class BotStatusDto
{
    public bool IsRunning { get; set; }
    public string Username { get; set; } = "";
    public long BotId { get; set; }
    public DateTime? StartedAt { get; set; }
}

public class BotStatsDto
{
    public int TotalUsers { get; set; }
    public int NewUsersThisWeek { get; set; }
    public int TodayMessages { get; set; }
    public int WeekMessages { get; set; }
    public int TodayQrRequests { get; set; }
    public int TotalSupportRequests { get; set; }
    public int UnreadSupportRequests { get; set; }
    public List<CommandStatDto> CommandStats { get; set; } = new();
    public List<RecentActionDto> RecentActions { get; set; } = new();
}

public class CommandStatDto
{
    public string Command { get; set; } = "";
    public int Count { get; set; }
}

public class RecentActionDto
{
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime Time { get; set; }
}

public class SupportChatDto
{
    public long ChatId { get; set; }
    public string UserName { get; set; } = "";
    public string? LastMessage { get; set; }
    public DateTime LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}

public class SupportChatDetailDto
{
    public long ChatId { get; set; }
    public string UserName { get; set; } = "";
    public string? Phone { get; set; }
    public List<SupportMessageDto> Messages { get; set; } = new();
}

public class SupportMessageDto
{
    public string Text { get; set; } = "";
    public DateTime Time { get; set; }
    public bool IsFromAdmin { get; set; }
}

public class BroadcastResultDto
{
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
}

public class BroadcastHistoryDto
{
    public string Message { get; set; } = "";
    public DateTime SentAt { get; set; }
    public int SentCount { get; set; }
}