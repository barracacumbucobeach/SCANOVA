using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SCANOVA.App.Services;
using SCANOVA.App.ViewModels;
using SCANOVA.Batch;
using SCANOVA.Core.Interfaces;
using SCANOVA.Imaging;
using SCANOVA.Infrastructure;
using SCANOVA.Infrastructure.FileSystem;
using SCANOVA.Licensing;
using SCANOVA.Ocr;
using SCANOVA.Pdf;
using SCANOVA.Scanner;
using SCANOVA.Tiff;

namespace SCANOVA.App;

/// <summary>
/// Raiz de composição do aplicativo: monta o container de injeção de dependência (seção 5.3 —
/// "utilizar interfaces para serviços importantes") e cria a janela principal.
/// </summary>
public partial class App : Application
{
    /// <summary>Container de DI do processo. Exposto estaticamente para resolução em code-behind de página (padrão comum em WinUI 3, que não tem um host de DI nativo para páginas).</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public static Window? MainAppWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        Services = BuildServiceProvider();
        UnhandledException += OnUnhandledException;
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddScanovaInfrastructure();
        services.AddScanovaImaging();
        services.AddScanovaTiff();
        services.AddScanovaPdf();
        services.AddScanovaBatch();
        services.AddScanovaScanner();
        services.AddScanovaOcr(AppPaths.OcrLanguageDataFolder);
        services.AddScanovaLicensing(AppPaths.LicenseFilePath);

        services.AddSingleton<INavigationService, FrameNavigationService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DocumentViewerViewModel>();
        services.AddTransient<ScanViewModel>();
        services.AddTransient<DuplexComposeViewModel>();
        services.AddTransient<BatchConvertViewModel>();
        services.AddTransient<OcrViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var log = Services.GetRequiredService<ILogService>();
        log.Info("SCANOVA iniciando.");

        // Seção 47: em caso de encerramento inesperado anterior, limpa temporários órfãos antes de tudo.
        var tempFiles = Services.GetRequiredService<TempFileManager>();
        tempFiles.CleanupStaleSessions();

        var settingsService = Services.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        MainAppWindow = new MainWindow();
        MainAppWindow.Closed += (_, _) =>
        {
            tempFiles.CleanupCurrentSession();
            log.Info("SCANOVA encerrado.");
        };
        MainAppWindow.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Seção 60: a exceção técnica nunca deve "vazar" cru para o usuário — vai só para o log.
        var log = Services.GetService<ILogService>();
        log?.Error(e.Exception, "Exceção não tratada na UI.");
        e.Handled = true;
    }
}
