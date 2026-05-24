using FitnessClub.Desktop.ViewModels;
using System.Windows;
using System.Windows.Input;
namespace FitnessClub.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm = new();
    public LoginWindow()
    {
        InitializeComponent();
        _vm.OnLoginSuccess = () =>
        {
            new MainWindow().Show();
            Close();
        };
        DataContext = _vm;
    }
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) LoginButton_Click(sender, e);
    }
   
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.Email = EmailBox.Text;
        LoginBtn.IsEnabled = false;
        ErrorBorder.Visibility = Visibility.Collapsed;
        await _vm.LoginAsync(PasswordBox.Password);
        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorBorder.Visibility = Visibility.Visible;
        }
        LoginBtn.IsEnabled = true;
    }
}