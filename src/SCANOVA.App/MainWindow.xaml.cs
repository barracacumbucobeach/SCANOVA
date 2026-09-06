using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.Services;
using SCANOVA.App.Views;

namespace SCANOVA.App;

/// <summary>Janela principal: casca de navegação (menu lateral + Frame de conteúdo) descrita na seção 7.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "SCANOVA — Digitalização e Conversão Documental";

        var navigation = App.Services.GetRequiredService<INavigationService>();
        navigation.SetFrame(ContentFrame);
        navigation.NavigateTo(typeof(DashboardPage));
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var navigation = App.Services.GetRequiredService<INavigationService>();

        if (args.IsSettingsSelected)
        {
            navigation.NavigateToPlaceholder("Configurações", "A tela de configurações completa (seção 48) será implementada ao longo das próximas fases.");
            return;
        }

        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "Dashboard":
                navigation.NavigateTo(typeof(DashboardPage));
                break;
            case "Scan":
                navigation.NavigateToPlaceholder("Digitalizar", "A digitalização via WIA será implementada na Fase 4 (Scanner).");
                break;
            case "Documents":
                navigation.NavigateToPlaceholder("Documentos", "A abertura e visualização de documentos será habilitada na Fase 2 (Imagens).");
                break;
            case "Convert":
                navigation.NavigateToPlaceholder("Converter", "A conversão entre formatos e o processamento em lote serão implementados na Fase 8.");
                break;
            case "Compose":
                navigation.NavigateToPlaceholder("Compor", "A composição frente + verso será implementada na Fase 7.");
                break;
            case "Enhance":
                navigation.NavigateToPlaceholder("Melhorar", "O motor de melhoria automática de documentos será implementado na Fase 5 (Automação).");
                break;
            case "Ocr":
                navigation.NavigateToPlaceholder("OCR", "O reconhecimento de texto local será implementado na Fase 9 (OCR).");
                break;
            case "History":
                navigation.NavigateToPlaceholder("Histórico", "O histórico local (seção 46) será conectado a um armazenamento persistente em uma próxima etapa da Fase 1.");
                break;
        }
    }
}
