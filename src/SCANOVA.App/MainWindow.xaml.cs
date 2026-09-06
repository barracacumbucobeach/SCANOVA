using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.Services;
using SCANOVA.App.Views;
using WinRT.Interop;

namespace SCANOVA.App;

/// <summary>Janela principal: casca de navegação (menu lateral + Frame de conteúdo) descrita na seção 7.</summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(4);

    // Capturado explicitamente (em vez de depender de Window.DispatcherQueue) para marshalling
    // seguro de volta à UI thread a partir do handler de INotificationService.
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private int _notificationToken;

    public MainWindow()
    {
        InitializeComponent();

        Title = "SCANOVA — Digitalização e Conversão Documental";
        SetWindowIcon();

        var navigation = App.Services.GetRequiredService<INavigationService>();
        navigation.SetFrame(ContentFrame);
        navigation.NavigateTo(typeof(DashboardPage));

        var notifications = App.Services.GetRequiredService<INotificationService>();
        notifications.Notified += Notifications_Notified;
    }

    private void Notifications_Notified(object? sender, AppNotification notification)
    {
        _dispatcherQueue.TryEnqueue(async () =>
        {
            GlobalInfoBar.Title = notification.Severity switch
            {
                NotificationSeverity.Success => "Sucesso",
                NotificationSeverity.Error => "Não foi possível concluir a operação",
                NotificationSeverity.Warning => "Atenção",
                _ => "Aviso",
            };
            GlobalInfoBar.Message = notification.Message;
            GlobalInfoBar.Severity = notification.Severity switch
            {
                NotificationSeverity.Success => InfoBarSeverity.Success,
                NotificationSeverity.Error => InfoBarSeverity.Error,
                NotificationSeverity.Warning => InfoBarSeverity.Warning,
                _ => InfoBarSeverity.Informational,
            };
            GlobalInfoBar.IsOpen = true;

            var myToken = unchecked(++_notificationToken);
            await Task.Delay(NotificationDuration);
            if (myToken == _notificationToken)
            {
                GlobalInfoBar.IsOpen = false;
            }
        });
    }

    /// <summary>
    /// Define o ícone da janela (barra de título/Alt+Tab). O ícone do executável/taskbar antes
    /// da janela abrir vem de <c>ApplicationIcon</c> no csproj — este é o ícone em tempo de
    /// execução da própria janela, que o Windows App SDK não herda automaticamente.
    /// </summary>
    private void SetWindowIcon()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon("Assets/scanova.ico");
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
                navigation.NavigateTo(typeof(ScanPage));
                break;
            case "Documents":
                navigation.NavigateToPlaceholder("Documentos", "Use \"Abrir documento\" no Início para abrir uma imagem. Uma lista de documentos recentes/multi-página será adicionada em uma próxima etapa.");
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
