using FitnessClub.Desktop.Views;
using FitnessClub.Desktop.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FitnessClub.Desktop;

public partial class MainWindow : Window
{
    private Button? _activeButton;

    public MainWindow()
    {
        InitializeComponent();
        SetupUser();
        if (AppSession.IsTrainer)
            NavigateTo(BtnDashboard, new TrainerDashboardPage());
        else
            NavigateTo(BtnDashboard, new DashboardPage());
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeBtn.Content = "⬜";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeBtn.Content = "❐";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Notifications_Click(object sender, RoutedEventArgs e)
        => NotificationPopup.IsOpen = !NotificationPopup.IsOpen;

    private void SetupUser()
    {
        UserNameText.Text = AppSession.FullName;
        UserRoleText.Text = AppSession.Role;
        AvatarText.Text = AppSession.FullName.Length > 0
            ? AppSession.FullName[0].ToString().ToUpper() : "?";

        if (!AppSession.IsAdmin)
            AdminSection.Visibility = Visibility.Collapsed;

        if (AppSession.IsTrainer)
        {
            BtnPayments.Visibility = Visibility.Collapsed;
            BtnAnalytics.Visibility = Visibility.Collapsed;
            BtnMemberships.Visibility = Visibility.Collapsed;
            BtnClients.Visibility = Visibility.Collapsed;
            BtnTrainers.Visibility = Visibility.Collapsed;
            BtnMonitor.Visibility = Visibility.Collapsed;
        }

        if (AppSession.Role == "Client")
        {
            BtnClients.Visibility = Visibility.Collapsed;
            BtnMemberships.Visibility = Visibility.Collapsed;
            BtnPayments.Visibility = Visibility.Collapsed;
            BtnTrainers.Visibility = Visibility.Collapsed;
            BtnAnalytics.Visibility = Visibility.Collapsed;
            BtnMonitor.Visibility = Visibility.Collapsed;
        }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        try
        {
            Page? page = btn.Tag?.ToString() switch
            {
                "Dashboard" => AppSession.IsTrainer ? new TrainerDashboardPage() : new DashboardPage(),
                "Clients" => new ClientsPage(),
                "Memberships" => new MembershipsPage(),
                "Schedule" => new SchedulePage(),
                "Payments" => new PaymentsPage(),
                "Trainers" => new TrainersPage(),
                "Analytics" => new AnalyticsPage(),
                "Users" => new UsersPage(),
                "Monitor" => new AccessMonitorPage(),
                "Bot" => new BotManagementPage(), // ← НОВИЙ
                _ => null
            };
            if (page != null) NavigateTo(btn, page);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка навігації: {ex.Message}", "Помилка");
        }
    }

    public void NavigateToPage(string tag)
    {
        var btn = tag switch
        {
            "Clients" => BtnClients,
            "Memberships" => BtnMemberships,
            "Schedule" => BtnSchedule,
            "Payments" => BtnPayments,
            "Trainers" => BtnTrainers,
            "Analytics" => BtnAnalytics,
            "Monitor" => BtnMonitor,
            _ => BtnDashboard
        };
        Navigate_Click(btn, new RoutedEventArgs());
    }

    private void NavigateTo(Button btn, Page page)
    {
        if (_activeButton != null)
        {
            var idle = TryFindResource("NavIconButton") as Style;
            if (idle != null) _activeButton.Style = idle;

            if (_activeButton.Content is System.Windows.Shapes.Path oldPath)
                oldPath.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x9C, 0xA3, 0xAF));
        }

        var active = TryFindResource("NavIconButtonActive") as Style;
        if (active != null) btn.Style = active;

        if (btn.Content is System.Windows.Shapes.Path newPath)
            newPath.Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x63, 0x66, 0xF1));

        _activeButton = btn;
        MainFrame.Navigate(page);
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        AppSession.Clear();
        new LoginWindow().Show();
        Close();
    }
}