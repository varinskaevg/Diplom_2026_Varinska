using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class EditClientDialog : Window
{
    private readonly int _clientId;

    public EditClientDialog(int clientId, JsonElement clientData)
    {
        InitializeComponent();
        _clientId = clientId;
        FillForm(clientData);
    }

    private void FillForm(JsonElement data)
    {
        FirstNameBox.Text = data.GetProperty("firstName").GetString() ?? "";
        LastNameBox.Text = data.GetProperty("lastName").GetString() ?? "";

        if (data.TryGetProperty("phone", out var phone) && phone.ValueKind != JsonValueKind.Null)
            PhoneBox.Text = phone.GetString();

        if (data.TryGetProperty("address", out var address) && address.ValueKind != JsonValueKind.Null)
            AddressBox.Text = address.GetString();

        if (data.TryGetProperty("emergencyContact", out var ec) && ec.ValueKind != JsonValueKind.Null)
            EmergencyContactBox.Text = ec.GetString();

        if (data.TryGetProperty("healthNotes", out var hn) && hn.ValueKind != JsonValueKind.Null)
            HealthNotesBox.Text = hn.GetString();

        if (data.TryGetProperty("dateOfBirth", out var dob) && dob.ValueKind != JsonValueKind.Null)
            BirthDatePicker.SelectedDate = dob.GetDateTime();

        if (data.TryGetProperty("gender", out var gender) && gender.ValueKind != JsonValueKind.Null)
        {
            var g = gender.GetString();
            foreach (ComboBoxItem item in GenderBox.Items)
                if (item.Content.ToString() == g)
                { GenderBox.SelectedItem = item; break; }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            ShowError("Введіть ім'я та прізвище");
            return;
        }

        try
        {
            var gender = (GenderBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var birthDate = BirthDatePicker.SelectedDate.HasValue
                ? DateOnly.FromDateTime(BirthDatePicker.SelectedDate.Value).ToString("yyyy-MM-dd")
                : null;

            await ApiClient.PutAsync($"api/clients/{_clientId}", new
            {
                firstName,
                lastName,
                phone = PhoneBox.Text.Trim(),
                dateOfBirth = birthDate,
                gender,
                address = AddressBox.Text.Trim(),
                emergencyContact = EmergencyContactBox.Text.Trim(),
                healthNotes = HealthNotesBox.Text.Trim()
            });

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