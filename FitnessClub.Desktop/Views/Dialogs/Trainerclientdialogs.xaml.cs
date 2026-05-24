// ══════════════════════════════════════════════════════════
//  AddTrainerClientDialog.xaml.cs
//  Прив'язка нового клієнта до тренера
// ══════════════════════════════════════════════════════════
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class AddTrainerClientDialog : Window
{
    private readonly int _trainerId;
    private List<ClientOption> _clients = [];

    public AddTrainerClientDialog(int trainerId)
    {
        _trainerId = trainerId;
        InitializeComponent();
        Loaded += async (_, _) => await LoadClients();

        // Значення за замовчуванням
        PaymentTypeBox.SelectedIndex = 0;
        PaymentMethodBox.SelectedIndex = 0;
    }

    private async Task LoadClients()
    {
        try
        {
            var data = await ApiClient.GetAsync<List<JsonElement>>("api/trainer-clients/all-clients");
            _clients = (data ?? []).Select(c => new ClientOption
            {
                Id = c.GetProperty("id").GetInt32(),
                Name = c.GetProperty("name").GetString() ?? "",
                Phone = c.TryGetProperty("phone", out var p) ? p.GetString() ?? "" : "",
            }).ToList();
            ClientBox.ItemsSource = _clients;
            ClientBox.DisplayMemberPath = "DisplayText";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження клієнтів: {ex.Message}");
        }
    }

    private void PaymentType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PayNowPanel == null) return;
        var selected = (PaymentTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "single";
        // Для разової оплати показуємо "Оплатити зараз"
        PayNowPanel.Visibility = selected == "single" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ClientBox.SelectedItem is not ClientOption client)
        { MessageBox.Show("Оберіть клієнта"); return; }

        if (!decimal.TryParse(RateBox.Text.Replace(" ", ""), out var rate) || rate <= 0)
        { MessageBox.Show("Вкажіть коректну ставку"); return; }

        var paymentType = (PaymentTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "single";
        var paymentMethod = (PaymentMethodBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Cash";
        var payNow = PayNowCheckBox.IsChecked == true;

        try
        {
            SaveBtn.IsEnabled = false;
            await ApiClient.PostAsync<object>("api/trainer-clients", new
            {
                trainerId = _trainerId,
                clientId = client.Id,
                paymentType,
                rate,
                endDate = (DateTime?)null,
                notes = NotesBox.Text.Trim().Length > 0 ? NotesBox.Text.Trim() : null,
                paidNow = paymentType == "single" && payNow,
                paymentMethod,
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
            SaveBtn.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

public class ClientOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string DisplayText => Phone.Length > 0 ? $"{Name} ({Phone})" : Name;
}

// ══════════════════════════════════════════════════════════
//  TrainerClientPayDialog.xaml.cs
//  Прийняти оплату від клієнта тренера
// ══════════════════════════════════════════════════════════

public partial class TrainerClientPayDialog : Window
{
    private readonly int _trainerClientId;
    private readonly string _paymentType;

    public TrainerClientPayDialog(int trainerClientId, string clientName, decimal rate, string paymentType)
    {
        _trainerClientId = trainerClientId;
        _paymentType = paymentType;
        InitializeComponent();

        TitleText.Text = $"Оплата — {clientName}";
        AmountBox.Text = rate.ToString("N0").Replace(" ", "");
        PaymentMethodBox.SelectedIndex = 0;

        // Авто-примітка
        NoteBox.Text = paymentType switch
        {
            "weekly" => $"Тижнева оплата ({DateTime.Today:dd.MM}–{DateTime.Today.AddDays(6):dd.MM.yyyy})",
            "monthly" => $"Місячна оплата ({DateTime.Today:MM.yyyy})",
            _ => "Оплата тренування"
        };

        PaidAtPicker.SelectedDate = DateTime.Today;
    }

    private async void Pay_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text.Replace(" ", ""), out var amount) || amount <= 0)
        { MessageBox.Show("Вкажіть суму"); return; }

        var method = (PaymentMethodBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Cash";
        var paidAt = PaidAtPicker.SelectedDate ?? DateTime.Today;

        try
        {
            PayBtn.IsEnabled = false;
            await ApiClient.PostAsync<object>($"api/trainer-clients/{_trainerClientId}/pay", new
            {
                amount,
                paymentMethod = method,
                note = NoteBox.Text.Trim().Length > 0 ? NoteBox.Text.Trim() : null,
                paidAt = paidAt.ToString("yyyy-MM-ddT00:00:00"),
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
            PayBtn.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}