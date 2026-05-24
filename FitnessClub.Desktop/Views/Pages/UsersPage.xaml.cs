using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;

namespace FitnessClub.Desktop.Views.Pages;

public partial class UsersPage : Page
{
    private List<UserItem> _all = [];
    private bool _loaded = false;

    public UsersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _loaded = true;
            await LoadUsers();
        };
    }

    private async Task LoadUsers()
    {
        try
        {
            var users = await ApiClient.GetAsync<List<UserItem>>("api/users");
            if (users == null) return;
            _all = users;
            ApplyFilter();
            UpdateStats();
        }
        catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
    }

    private void UpdateStats()
    {
        AdminCountText.Text = _all.Count(u => u.Role == "Admin").ToString();
        ManagerCountText.Text = _all.Count(u => u.Role == "Manager").ToString();
        TrainerCountText.Text = _all.Count(u => u.Role == "Trainer").ToString();
        ClientCountText.Text = _all.Count(u => u.Role == "Client").ToString();
    }

    private void ApplyFilter()
    {
        // Захист від виклику до повного завантаження UI
        if (!_loaded || UsersList == null || SearchBox == null || RoleFilter == null) return;

        var search = SearchBox.Text.Trim().ToLower();
        var role = (RoleFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Всі ролі";

        var f = _all.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
            f = f.Where(u => u.FullName.ToLower().Contains(search) ||
                             u.Email.ToLower().Contains(search) ||
                             (u.Phone?.Contains(search) ?? false));

        if (role != "Всі ролі")
            f = f.Where(u => u.Role == role);

        UsersList.ItemsSource = f.ToList();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void RoleFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void AddUser_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("В розробці");

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
            MessageBox.Show($"Редагування ID: {id} — в розробці");
    }

    private async void ToggleUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            if (MessageBox.Show("Деактивувати?", "Підтвердження",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    await ApiClient.DeleteAsync($"api/users/{id}/toggle");
                    await LoadUsers();
                }
                catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
            }
        }
    }
}

public class UserItem
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
    public string CreatedAtStr => $"З {CreatedAt:dd.MM.yyyy}";
    public string RoleUa => Role switch
    {
        "Admin" => "Адмін",
        "Manager" => "Менеджер",
        "Trainer" => "Тренер",
        "Client" => "Клієнт",
        _ => Role
    };
    public string RoleIcon => Role switch
    {
        "Admin" => "👑",
        "Manager" => "💼",
        "Trainer" => "🏋️",
        _ => "👤"
    };
    public string AvatarColor1 => Role switch
    {
        "Admin" => "#6C63FF",
        "Manager" => "#059669",
        "Trainer" => "#1d4ed8",
        _ => "#c2410c"
    };
    public string AvatarColor2 => Role switch
    {
        "Admin" => "#a78bfa",
        "Manager" => "#34d399",
        "Trainer" => "#60a5fa",
        _ => "#fb923c"
    };
}