using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Dialogs;
using FitnessClub.Desktop.Models;

namespace FitnessClub.Desktop.Views.Pages;

public partial class ClientsPage : Page
{
    private List<ClientItem> _allClients = [];
    private string _currentFilter = "active"; // "active", "no_membership", "all"

    public ClientsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadClients();
    }

    private async Task LoadClients(string search = "")
    {
        try
        {
            var url = string.IsNullOrEmpty(search)
                ? "api/clients"
                : $"api/clients?search={Uri.EscapeDataString(search)}";

            var clients = await ApiClient.GetAsync<List<ClientItem>>(url);
            if (clients == null) return;

            _allClients = clients;
            ApplyFilter();
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        var filtered = _currentFilter switch
        {
            "active" => _allClients.Where(c => c.HasActiveMembership).ToList(),
            "no_membership" => _allClients.Where(c => !c.HasActiveMembership).ToList(),
            _ => _allClients
        };

        // Застосувати пошук якщо є текст
        var searchText = SearchBox.Text.Trim().ToLower();
        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(c =>
                c.FullName.ToLower().Contains(searchText) ||
                (c.Email?.ToLower().Contains(searchText) ?? false) ||
                (c.Phone?.Contains(searchText) ?? false)
            ).ToList();
        }

        ClientsList.ItemsSource = filtered;
        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateFilterStyles()
    {
        // Скинути всі фільтри до неактивного стану
        SetFilterInactive(FilterActive, FilterActiveText);
        SetFilterInactive(FilterNoMembership, FilterNoMembershipText);
        SetFilterInactive(FilterAll, FilterAllText);

        // Активувати поточний фільтр
        switch (_currentFilter)
        {
            case "active":
                SetFilterActive(FilterActive, FilterActiveText, "#00FF94");
                break;
            case "no_membership":
                SetFilterActive(FilterNoMembership, FilterNoMembershipText, "#F59E0B");
                break;
            case "all":
                SetFilterActive(FilterAll, FilterAllText, "#4A8AFF");
                break;
        }
    }

    private void SetFilterActive(Border border, TextBlock text, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        border.Background = new SolidColorBrush(Color.FromArgb(0x15, color.R, color.G, color.B));
        border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B));
        text.Foreground = new SolidColorBrush(color);
        text.FontWeight = FontWeights.SemiBold;
    }

    private void SetFilterInactive(Border border, TextBlock text)
    {
        border.Background = new SolidColorBrush(Color.FromArgb(0x10, 255, 255, 255));
        border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 255, 255, 255));
        text.Foreground = new SolidColorBrush(Color.FromArgb(0x70, 255, 255, 255));
        text.FontWeight = FontWeights.Normal;
    }

    private void FilterActive_Click(object sender, MouseButtonEventArgs e)
    {
        _currentFilter = "active";
        UpdateFilterStyles();
        ApplyFilter();
    }

    private void FilterNoMembership_Click(object sender, MouseButtonEventArgs e)
    {
        _currentFilter = "no_membership";
        UpdateFilterStyles();
        ApplyFilter();
    }

    private void FilterAll_Click(object sender, MouseButtonEventArgs e)
    {
        _currentFilter = "all";
        UpdateFilterStyles();
        ApplyFilter();
    }

    private void UpdateStatistics()
    {
        StatTotalClients.Text = _allClients.Count.ToString("N0");
        TotalClientsCount.Text = _allClients.Count.ToString("N0");
        StatActiveMembers.Text = _allClients.Count(c => c.HasActiveMembership).ToString("N0");
        StatNoMembership.Text = _allClients.Count(c => !c.HasActiveMembership).ToString("N0");

        var newThisMonth = _allClients.Count(c => c.RegistrationDate >= DateTime.Now.AddDays(-30));
        StatNewThisMonth.Text = $"+{newThisMonth}";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void AddClient_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddClientDialog();
        if (dialog.ShowDialog() == true)
            _ = LoadClients();
    }

    private void ViewClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var dialog = new ClientDetailDialog(id);
            dialog.ShowDialog();
            _ = LoadClients();
        }
    }

    private async void EditClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            try
            {
                var clientData = await ApiClient.GetAsync<JsonElement>($"api/clients/{id}");
                var dialog = new EditClientDialog(id, clientData);
                if (dialog.ShowDialog() == true)
                    await LoadClients();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void DeleteClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var client = _allClients.FirstOrDefault(c => c.Id == id);
            if (client == null) return;

            var result = MessageBox.Show(
                $"Ви впевнені, що хочете видалити клієнта {client.FullName}?\n\nЦю дію неможливо скасувати.",
                "Підтвердження видалення",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await ApiClient.DeleteAsync($"api/clients/{id}");
                    await LoadClients();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка видалення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}