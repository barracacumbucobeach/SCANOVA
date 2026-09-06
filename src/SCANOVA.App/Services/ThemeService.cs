using Microsoft.UI.Xaml;

namespace SCANOVA.App.Services;

/// <summary>
/// Implementação de <see cref="IThemeService"/>: define <see cref="FrameworkElement.RequestedTheme"/>
/// no conteúdo da janela principal — o tema herda para toda a árvore visual a partir daí (seção
/// 48). Como toda a UI já usa recursos de tema (<c>ThemeResource</c>) em vez de cores fixas
/// (verificado nesta fase), nenhuma outra página precisa de qualquer ajuste para suportar o modo
/// escuro.
/// </summary>
public sealed class ThemeService : IThemeService
{
    public void Apply(string themePreference)
    {
        if (App.MainAppWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = themePreference switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
