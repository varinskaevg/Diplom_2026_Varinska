using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Views.Pages;

namespace FitnessClub.Desktop.Views.Dialogs;

public partial class EditMembershipDialog : Window
{
    private readonly MembershipItem _item;
    private List<MembershipTypeItem> _types = [];

    public EditMembershipDialog(MembershipItem item)
    {
        InitializeComponent();
        _item = item;
        Loaded += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            ClientNameText.Text = _item.ClientName;
            TitleText.Text = $"Абонемент — {_item.ClientName}";

            var types = await ApiClient.GetAsync<List<MembershipTypeItem>>("api/memberships/types");
            if (types != null)
            {
                _types = types;
                TypeBox.ItemsSource = _types;
                TypeBox.SelectedItem = _types.FirstOrDefault(t => t.Id == _item.MembershipTypeId);
            }

            if (DateTime.TryParse(_item.StartDate, out var start))
                StartDatePicker.SelectedDate = start;
            if (DateTime.TryParse(_item.EndDate, out var end))
                EndDatePicker.SelectedDate = end;

            NotesBox.Text = _item.Notes ?? "";

            foreach (ComboBoxItem si in StatusBox.Items)
                if (si.Tag?.ToString() == _item.Status)
                { StatusBox.SelectedItem = si; break; }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TypeBox.SelectedItem is not MembershipTypeItem type)
        {
            ShowError("Оберіть тип абонементу");
            return;
        }
        if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
        {
            ShowError("Вкажіть дати");
            return;
        }

        var status = (StatusBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Active";

        try
        {
            await ApiClient.PutAsync($"api/memberships/{_item.Id}", new
            {
                membershipTypeId = type.Id,
                startDate = DateOnly.FromDateTime(StartDatePicker.SelectedDate.Value),
                endDate = DateOnly.FromDateTime(EndDatePicker.SelectedDate.Value),
                status,
                notes = NotesBox.Text.Trim()
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