using System.Windows;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Pages;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class EditTrainerDialog : Window
{
    private readonly int? _id;

    public EditTrainerDialog(int? id, TrainerViewModel? trainer)
    {
        InitializeComponent();
        _id = id;

        if (id != null && trainer != null)
        {
            TitleText.Text = $"Редагування — {trainer.FullName}";
            EmailPanel.Visibility = Visibility.Collapsed;
            PasswordPanel.Visibility = Visibility.Collapsed;
            FirstNameBox.Text = trainer.FirstName;
            LastNameBox.Text = trainer.LastName;
            PhoneBox.Text = trainer.Phone ?? "";
            SpecializationBox.Text = trainer.Specialization ?? "";
            ExperienceBox.Text = trainer.ExperienceYears.ToString();
            RateBox.Text = trainer.HourlyRate.ToString();
            GroupRateBox.Text = trainer.GroupRate.ToString();
            IndividualRateBox.Text = trainer.IndividualRate.ToString();
            MonthlyPlanRateBox.Text = trainer.MonthlyPlanRate.ToString();
            BioBox.Text = trainer.Bio ?? "";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        { ShowError("Введіть ім'я та прізвище"); return; }

        if (!int.TryParse(ExperienceBox.Text, out var exp))
        { ShowError("Невірне значення досвіду"); return; }

        if (!decimal.TryParse(RateBox.Text, out var rate))
        { ShowError("Невірне значення ставки"); return; }

        decimal.TryParse(GroupRateBox.Text, out var groupRate);
        decimal.TryParse(IndividualRateBox.Text, out var individualRate);
        decimal.TryParse(MonthlyPlanRateBox.Text, out var monthlyPlanRate);

        try
        {
            if (_id == null)
            {
                var email = EmailBox.Text.Trim();
                var password = PasswordBox.Password;
                if (string.IsNullOrEmpty(email)) { ShowError("Введіть email"); return; }
                if (string.IsNullOrEmpty(password)) { ShowError("Введіть пароль"); return; }

                await ApiClient.PostAsync<object>("api/trainers", new
                {
                    firstName,
                    lastName,
                    email,
                    password,
                    phone = PhoneBox.Text.Trim(),
                    specialization = SpecializationBox.Text.Trim(),
                    experienceYears = exp,
                    hourlyRate = rate,
                    groupRate,
                    individualRate,
                    monthlyPlanRate,
                    bio = BioBox.Text.Trim()
                });
            }
            else
            {
                await ApiClient.PutAsync($"api/trainers/{_id}", new
                {
                    firstName,
                    lastName,
                    phone = PhoneBox.Text.Trim(),
                    specialization = SpecializationBox.Text.Trim(),
                    experienceYears = exp,
                    hourlyRate = rate,
                    groupRate,
                    individualRate,
                    monthlyPlanRate,
                    bio = BioBox.Text.Trim()
                });
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex) { ShowError($"Помилка: {ex.Message}"); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}