using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Views.Dialogs;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Models;
using System.Text.Json.Serialization;

namespace FitnessClub.Desktop.Views.Pages;

public partial class MembershipsPage : Page
{
    private List<MembershipItem> _all = [];
    private List<MembershipTypeViewModel> _types = [];

    public MembershipsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadMemberships();
    }

    private async Task LoadMemberships()
    {
        try
        {
            var memberships = await ApiClient.GetAsync<List<MembershipItem>>("api/memberships");
            if (memberships == null) return;
            _all = memberships;
            MembershipsList.ItemsSource = _all;
            await UpdateStats();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private async Task UpdateStats()
    {
        var today = DateTime.Today;

        StatActive.Text = _all.Count(m => m.Status == "Active").ToString();
        StatFrozen.Text = _all.Count(m => m.Status == "Frozen").ToString();

        StatExpiring.Text = _all.Count(m =>
            m.Status == "Active" &&
            DateTime.TryParse(m.EndDate, out var end) &&
            (end - today).TotalDays is >= 0 and <= 7
        ).ToString();

        // Беремо дохід з dashboard — поле MonthRevenue
        try
        {
            var dashboard = await ApiClient.GetAsync<DashboardStats>("api/dashboard");
            StatRevenueAmount.Text = dashboard != null
                ? dashboard.MonthRevenue.ToString("N0")
                : "—";
        }
        catch
        {
            StatRevenueAmount.Text = "—";
        }
    }

    private async Task LoadTypes()
    {
        try
        {
            var types = await ApiClient.GetAsync<List<MembershipTypeItem>>("api/memberships/types");
            if (types == null) return;

            var typeCounts = _all
                .Where(m => m.Status == "Active")
                .GroupBy(m => m.MembershipTypeId)
                .ToDictionary(g => g.Key, g => g.Count());

            _types = types.Select(t => new MembershipTypeViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Price = t.Price,
                DurationDays = t.DurationDays,
                Description = t.Description ?? "",
                IncludesGym = t.IncludesGym,
                IncludesPool = t.IncludesPool,
                IncludesClasses = t.IncludesClasses,
                ActiveCount = typeCounts.GetValueOrDefault(t.Id, 0)
            }).OrderByDescending(t => t.ActiveCount).ToList();

            TypesList.ItemsSource = _types;
            TypesCountText.Text = _types.Count.ToString();

            if (_types.Any())
            {
                CheapestText.Text = $"{_types.Min(t => t.Price):N0} ₴";
                MostExpensiveText.Text = $"{_types.Max(t => t.Price):N0} ₴";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка типів: {ex.Message}");
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_all == null || MembershipsList == null) return;
        MembershipsList.ItemsSource =
            FilterAll.IsChecked == true ? _all :
            FilterActive.IsChecked == true ? _all.Where(m => m.Status == "Active").ToList() :
            FilterFrozen.IsChecked == true ? _all.Where(m => m.Status == "Frozen").ToList() :
            FilterExpired.IsChecked == true ? _all.Where(m => m.Status == "Expired").ToList() :
            _all.Where(m => m.Status == "Cancelled").ToList();
    }

    private void TabActive_Click(object sender, RoutedEventArgs e)
    {
        ActivePanel.Visibility = Visibility.Visible;
        TypesPanel.Visibility = Visibility.Collapsed;
        SetTabActive(TabActiveBtn, true);
        SetTabActive(TabTypesBtn, false);
    }

    private async void TabTypes_Click(object sender, RoutedEventArgs e)
    {
        ActivePanel.Visibility = Visibility.Collapsed;
        TypesPanel.Visibility = Visibility.Visible;
        SetTabActive(TabActiveBtn, false);
        SetTabActive(TabTypesBtn, true);
        await LoadTypes();
    }

    private void SetTabActive(Button btn, bool active)
    {
        btn.Background = active
            ? new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6C63FF"))
            : new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1a1f35"));
        btn.Foreground = active
            ? System.Windows.Media.Brushes.White
            : new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ca3af"));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var item = _all.FirstOrDefault(m => m.Id == id);
            if (item == null) return;
            var dialog = new EditMembershipDialog(item);
            if (dialog.ShowDialog() == true)
                _ = LoadMemberships();
        }
    }

    private async void FreezeUnfreeze_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var item = _all.FirstOrDefault(m => m.Id == id);
            if (item == null) return;
            try
            {
                if (item.Status == "Frozen")
                    await ApiClient.PutAsync($"api/memberships/{id}/unfreeze", new { });
                else
                    await ApiClient.PutAsync($"api/memberships/{id}/freeze", new
                    {
                        frozenFrom = DateOnly.FromDateTime(DateTime.Today),
                        frozenTo = DateOnly.FromDateTime(DateTime.Today.AddDays(14))
                    });
                await LoadMemberships();
            }
            catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
        }
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            if (MessageBox.Show("Скасувати абонемент?", "Підтвердження",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    await ApiClient.PutAsync($"api/memberships/{id}/cancel", new { });
                    await LoadMemberships();
                }
                catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
            }
        }
    }

    private async void Refund_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int id) return;
        var item = _all.FirstOrDefault(m => m.Id == id);
        if (item == null) return;

        if (item.Status == "Cancelled")
        {
            MessageBox.Show("Цей абонемент вже скасовано.", "Увага",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Зробити повернення коштів за абонемент \"{item.MembershipTypeName}\" клієнту {item.ClientName}?\n\nАбонемент буде скасовано автоматично.",
            "Підтвердження повернення",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var response = await ApiClient.PutAsync<System.Text.Json.JsonElement>(
                $"api/memberships/{id}/refund", new { });
            var amount = response.GetProperty("refundAmount").GetDecimal();
            MessageBox.Show($"✅ Повернення оформлено!\nСума повернення: {amount:N0} ₴",
                "Успішно", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadMemberships();
        }
        catch (Exception ex) { MessageBox.Show($"Помилка повернення: {ex.Message}"); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            if (MessageBox.Show("Видалити абонемент назавжди?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    await ApiClient.DeleteAsync($"api/memberships/{id}");
                    await LoadMemberships();
                }
                catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
            }
        }
    }

    private void SellMembership_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SellMembershipDialog();
        if (dialog.ShowDialog() == true)
            _ = LoadMemberships();
    }

    private void SellType_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SellMembershipDialog();
        if (dialog.ShowDialog() == true)
            _ = LoadMemberships();
    }
}

// ═══════════════════════════════════════════════════════
//  МОДЕЛІ
// ═══════════════════════════════════════════════════════

public class MembershipItem
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Initials { get; set; } = "";

    // API повертає поле як "typeName"
    [JsonPropertyName("typeName")]
    public string MembershipTypeName { get; set; } = "";

    public int MembershipTypeId { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
    public string? FrozenFrom { get; set; }
    public string? FrozenTo { get; set; }
    public int VisitsUsed { get; set; }

    public string StatusUa => Status switch
    {
        "Active" => "Активний",
        "Expired" => "Прострочений",
        "Frozen" => "Заморожений",
        "Cancelled" => "Скасований",
        _ => Status
    };

    public string DeleteVisible => AppSession.IsAdmin ? "Visible" : "Collapsed";
    public string RefundVisible => Status != "Cancelled" ? "Visible" : "Collapsed";

    public string DaysLeftText
    {
        get
        {
            if (Status == "Cancelled") return "Скасований";
            if (Status == "Frozen") return "Заморожено";
            if (!DateTime.TryParse(EndDate, out var end)) return "—";
            var days = (end - DateTime.Today).Days;
            return days switch
            {
                < 0 => "Прострочено",
                0 => "Останній день",
                _ => $"{days} дн."
            };
        }
    }

    public double ProgressWidth
    {
        get
        {
            if (!DateTime.TryParse(StartDate, out var start)) return 0;
            if (!DateTime.TryParse(EndDate, out var end)) return 0;
            var total = (end - start).TotalDays;
            if (total <= 0) return 0;
            var remaining = (end - DateTime.Today).TotalDays;
            return Math.Round(Math.Clamp(remaining / total, 0, 1) * 80, 0);
        }
    }
}

public class MembershipTypeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public string? Description { get; set; }
    public bool IncludesGym { get; set; }
    public bool IncludesPool { get; set; }
    public bool IncludesClasses { get; set; }
}

public class MembershipTypeViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public string Description { get; set; } = "";
    public bool IncludesGym { get; set; }
    public bool IncludesPool { get; set; }
    public bool IncludesClasses { get; set; }
    public int ActiveCount { get; set; }

    public string PriceText => $"{Price:N0} ₴";
    public string DurationText => $"{DurationDays} днів";
    public string PopularityText => $"{ActiveCount} активних";
    public string GymIcon => IncludesGym ? "✅" : "❌";
    public string PoolIcon => IncludesPool ? "✅" : "❌";
    public string ClassesIcon => IncludesClasses ? "✅" : "❌";
}