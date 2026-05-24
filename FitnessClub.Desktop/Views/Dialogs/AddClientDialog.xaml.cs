using System.Windows;
using FitnessClub.Desktop.Services;


namespace FitnessClub.Desktop.Views.Dialogs;

public partial class AddClientDialog : Window
{
    public AddClientDialog()
    {
        InitializeComponent();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();
        var email = EmailBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            ShowError("Введіть ім'я та прізвище");
            return;
        }
        if (string.IsNullOrEmpty(email))
        {
            ShowError("Введіть email");
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            ShowError("Введіть пароль");
            return;
        }

        try
        {
            var gender = (GenderBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            var birthDate = BirthDatePicker.SelectedDate.HasValue
                ? DateOnly.FromDateTime(BirthDatePicker.SelectedDate.Value).ToString("yyyy-MM-dd")
                : null;

            await ApiClient.PostAsync<object>("api/clients", new
            {
                email,
                password,
                firstName,
                lastName,
                phone = PhoneBox.Text.Trim(),
                dateOfBirth = birthDate,
                gender,
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