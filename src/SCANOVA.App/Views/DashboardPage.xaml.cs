using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.ViewModels;

namespace SCANOVA.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        InitializeComponent();
    }

    private async void WelcomeBanner_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        if (ViewModel.DismissWelcomeCommand.CanExecute(null))
        {
            await ViewModel.DismissWelcomeCommand.ExecuteAsync(null);
        }
    }
}
