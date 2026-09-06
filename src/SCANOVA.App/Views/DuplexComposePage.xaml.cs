using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.ViewModels;

namespace SCANOVA.App.Views;

/// <summary>Composição frente/verso (Fase 7): junta dois lotes de páginas escaneadas/abertas separadamente em um único documento.</summary>
public sealed partial class DuplexComposePage : Page
{
    public DuplexComposeViewModel ViewModel { get; }

    public DuplexComposePage()
    {
        ViewModel = App.Services.GetRequiredService<DuplexComposeViewModel>();
        InitializeComponent();
    }
}
