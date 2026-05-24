using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Pages;
using FitnessClub.Desktop.Models;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class SellMembershipDialog : Window
{
    private readonly int? _preselectedClientId;
    private List<MembershipTypeItem> _types = [];

    public SellMembershipDialog(int? clientId = null)
    {
        InitializeComponent();
        _preselectedClientId = clientId;
        Loaded += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            // Завантажуємо клієнтів
            var clients = await ApiClient.GetAsync<List<ClientItem>>("api/clients");
            if (clients != null)
            {
                ClientBox.ItemsSource = clients;
                if (_preselectedClientId.HasValue)
                {
                    var preselected = clients.FirstOrDefault(c => c.Id == _preselectedClientId.Value);
                    if (preselected != null)
                        ClientBox.SelectedItem = preselected;
                }
            }

            // Завантажуємо типи абонементів
            var types = await ApiClient.GetAsync<List<MembershipTypeItem>>("api/memberships/types");
            if (types != null)
            {
                _types = types;
                MembershipTypeBox.ItemsSource = _types;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private void MembershipTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MembershipTypeBox.SelectedItem is MembershipTypeItem type)
        {
            PriceText.Text = $"{type.Price:N0} ₴";
            DurationText.Text = $"{type.DurationDays} дн.";
            EndDateText.Text = DateTime.Today.AddDays(type.DurationDays).ToString("dd.MM.yyyy");
            MembershipInfoPanel.Visibility = Visibility.Visible;
        }
    }

    private void ClientBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private async void Sell_Click(object sender, RoutedEventArgs e)
    {
        if (ClientBox.SelectedItem is not ClientItem client)
        {
            ShowError("Оберіть клієнта");
            return;
        }
        if (MembershipTypeBox.SelectedItem is not MembershipTypeItem type)
        {
            ShowError("Оберіть тип абонементу");
            return;
        }

        var paymentMethod = (PaymentMethodBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Cash";

        try
        {
            await ApiClient.PostAsync<object>("api/memberships", new
            {
                clientId = client.Id,
                membershipTypeId = type.Id,
                paymentMethod
            });

            MessageBox.Show($"✅ Абонемент \"{type.Name}\" продано клієнту {client.FullName}!",
                "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Помилка: {ex.Message}");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}

public class MembershipTypeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public bool IncludesGym { get; set; }
    public bool IncludesPool { get; set; }
    public bool IncludesClasses { get; set; }
}