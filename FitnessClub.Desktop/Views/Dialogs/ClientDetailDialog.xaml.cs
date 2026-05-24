using System.Text.Json;
using System.Windows;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Dialogs;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class ClientDetailDialog : Window
{
    private readonly int _clientId;
    private JsonElement _clientData;

    public ClientDetailDialog(int clientId)
    {
        InitializeComponent();
        _clientId = clientId;
        Loaded += async (_, _) => await LoadClient();
    }

    private async Task LoadClient()
    {
        try
        {
            var result = await ApiClient.GetAsync<JsonElement>($"api/clients/{_clientId}");
            _clientData = result;

            var firstName = result.GetProperty("firstName").GetString() ?? "";
            var lastName = result.GetProperty("lastName").GetString() ?? "";
            var email = result.GetProperty("email").GetString() ?? "";

            TitleText.Text = $"{firstName} {lastName}";
            ClientNameText.Text = $"{firstName} {lastName}";
            ClientEmailText.Text = email;
            AvatarText.Text = $"{firstName.FirstOrDefault()}{lastName.FirstOrDefault()}".ToUpper();

            PhoneText.Text = result.TryGetProperty("phone", out var phone) && phone.ValueKind != JsonValueKind.Null
                ? phone.GetString() : "—";

            if (result.TryGetProperty("dateOfBirth", out var dob) && dob.ValueKind != JsonValueKind.Null)
                BirthDateText.Text = dob.GetDateTime().ToString("dd.MM.yyyy");
            else
                BirthDateText.Text = "—";

            GenderText.Text = result.TryGetProperty("gender", out var gender) && gender.ValueKind != JsonValueKind.Null
                ? gender.GetString() : "—";

            AddressText.Text = result.TryGetProperty("address", out var address) && address.ValueKind != JsonValueKind.Null
                ? address.GetString() : "—";

            TotalVisitsText.Text = result.GetProperty("totalVisits").GetInt32().ToString();

            // Платежі
            var payments = result.GetProperty("recentPayments");
            var paymentList = new List<PaymentRow>();
            foreach (var p in payments.EnumerateArray())
            {
                paymentList.Add(new PaymentRow
                {
                    Amount = p.GetProperty("amount").GetDecimal(),
                    PaymentDate = p.GetProperty("paymentDate").GetDateTime(),
                    PaymentMethod = p.TryGetProperty("paymentMethod", out var pm) ? pm.GetString() : ""
                });
            }
            PaymentsList.ItemsSource = paymentList;
            TotalSpentText.Text = $"{paymentList.Sum(p => p.Amount):N0} ₴";

            // Відвідування
            var memberships = result.GetProperty("memberships");
            var activeMembership = memberships.EnumerateArray()
                .FirstOrDefault(m => m.GetProperty("status").GetString() == "Active");

            if (activeMembership.ValueKind != JsonValueKind.Undefined)
            {
                MembershipTypeText.Text = activeMembership.GetProperty("name").GetString();
                MembershipEndText.Text = activeMembership.GetProperty("endDate").GetDateTime().ToString("dd.MM.yyyy");
                MembershipStatusText.Text = "Активний";
            }
            else
            {
                MembershipTypeText.Text = "—";
                MembershipEndText.Text = "—";
                MembershipStatusText.Text = "Немає";
                MembershipStatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private async void CheckIn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApiClient.PostAsync<object>($"api/clients/{_clientId}/checkin", new { });
            MessageBox.Show("✅ Відвідування відмічено!", "Успіх",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadClient();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EditClientDialog(_clientId, _clientData);
        if (dialog.ShowDialog() == true)
            _ = LoadClient();
    }

    private void SellMembership_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SellMembershipDialog(_clientId);
        if (dialog.ShowDialog() == true)
            _ = LoadClient();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public class PaymentRow
{
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
}

public class VisitRow
{
    public DateTime CheckIn { get; set; }
    public string CheckInStr => CheckIn.ToString("dd.MM.yyyy HH:mm");
}