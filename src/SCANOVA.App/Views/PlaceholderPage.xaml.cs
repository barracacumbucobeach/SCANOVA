using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace SCANOVA.App.Views;

/// <summary>Parâmetro de navegação para <see cref="PlaceholderPage"/>.</summary>
public sealed record PlaceholderPageParameter(string Title, string Description);

/// <summary>
/// Página genérica exibida para módulos ainda não implementados na fase atual do
/// desenvolvimento (ver seção 136/137 — desenvolvimento por fases). Evita links "mortos" no
/// menu enquanto cada módulo ainda não existe.
/// </summary>
public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is PlaceholderPageParameter parameter)
        {
            TitleText.Text = parameter.Title;
            DescriptionText.Text = parameter.Description;
        }
    }
}
