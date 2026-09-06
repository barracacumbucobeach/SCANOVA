using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.ViewModels;

namespace SCANOVA.App.Views;

/// <summary>
/// Tela "Configurações" (seção 48): licenciamento (Fase 10) e informações sobre o aplicativo.
/// As demais seções (aparência, acessibilidade etc.) chegam na Fase 11.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}
