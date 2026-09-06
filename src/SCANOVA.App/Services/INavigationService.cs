using Microsoft.UI.Xaml.Controls;

namespace SCANOVA.App.Services;

/// <summary>
/// Abstrai a navegação entre páginas para que ViewModels não dependam diretamente de
/// <see cref="Frame"/> (mantém a separação entre UI e apresentação).
/// </summary>
public interface INavigationService
{
    /// <summary>Associa o <see cref="Frame"/> raiz da janela principal. Chamado uma vez, por <c>MainWindow</c>.</summary>
    void SetFrame(Frame frame);

    void NavigateTo(Type pageType, object? parameter = null);

    /// <summary>Navega para a página de placeholder de um módulo ainda não implementado nesta fase.</summary>
    void NavigateToPlaceholder(string title, string description);
}
