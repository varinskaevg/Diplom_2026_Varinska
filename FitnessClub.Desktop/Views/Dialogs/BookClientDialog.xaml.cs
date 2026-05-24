using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class BookClientDialog : Window
{
    private readonly int _scheduleId;
    private int? _selectedClientId;
    private List<ClientBookingItem> _clients = [];

    public BookClientDialog(int scheduleId, string className,
        int maxCapacity, int currentCount)
    {
        InitializeComponent();
        _scheduleId = scheduleId;
        TitleText.Text = "Записати на заняття";
        SubtitleText.Text = maxCapacity > 0
            ? $"{className} · {currentCount}/{maxCapacity} місць"
            : $"{className} · {currentCount} записаних";
        Loaded += async (_, _) => await LoadClients();
    }

    private async Task LoadClients(string search = "")
    {
        try
        {
            var url = $"api/bookings/available-clients?scheduleId={_scheduleId}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            var data = await ApiClient.GetAsync<List<JsonElement>>(url);

            _clients = (data ?? []).Select(c =>
            {
                var hasMembership = c.TryGetProperty("activeMembership", out var m) &&
                                    m.ValueKind != JsonValueKind.Null;
                var includesClasses = hasMembership &&
                                      m.GetProperty("includesClasses").GetBoolean();
                var membershipName = hasMembership
                    ? m.GetProperty("name").GetString() ?? ""
                    : "";

                return new ClientBookingItem
                {
                    Id = c.GetProperty("id").GetInt32(),
                    FirstName = c.GetProperty("firstName").GetString() ?? "",
                    LastName = c.GetProperty("lastName").GetString() ?? "",
                    Phone = c.TryGetProperty("phone", out var ph) &&
                            ph.ValueKind != JsonValueKind.Null
                        ? ph.GetString() : null,
                    HasMembership = hasMembership,
                    IncludesClasses = includesClasses,
                    MembershipName = membershipName
                };
            }).ToList();

            ClientsList.ItemsSource = _clients;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження клієнтів: {ex.Message}");
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
        => _ = LoadClients(SearchBox.Text);

    private void SelectClient_Click(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.DataContext is not ClientBookingItem client) return;

        _selectedClientId = client.Id;
        BookBtn.IsEnabled = true;

        foreach (var item in _clients)
            item.IsSelected = item.Id == client.Id;

        ClientsList.ItemsSource = null;
        ClientsList.ItemsSource = _clients;
    }

    private async void Book_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClientId == null) return;

        try
        {
            var result = await ApiClient.PostAsync<JsonElement>("api/bookings", new
            {
                scheduleId = _scheduleId,
                clientId = _selectedClientId.Value
            });

            var extraCharge = result.GetProperty("extraCharge").GetDecimal();
            if (extraCharge > 0)
            {
                var reason = result.GetProperty("chargeReason").GetString();
                MessageBox.Show(
                    $"⚠️ Клієнт записаний!\n\nДо оплати: {extraCharge:N0} ₴\nПричина: {reason}",
                    "Увага — додаткова оплата",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка запису: {ex.Message}");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class ClientBookingItem
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public bool HasMembership { get; set; }
    public bool IncludesClasses { get; set; }
    public string MembershipName { get; set; } = "";
    public bool IsSelected { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}"
        .ToUpper().Trim();
    public string MembershipStatus => !HasMembership
        ? "Без абонементу"
        : !IncludesClasses
            ? $"{MembershipName} (без занять)"
            : $"✓ {MembershipName}";
}