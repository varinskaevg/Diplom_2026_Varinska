using System.Windows;
using FitnessClub.Desktop.Views;

namespace FitnessClub.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new LoginWindow().Show();
    }
}