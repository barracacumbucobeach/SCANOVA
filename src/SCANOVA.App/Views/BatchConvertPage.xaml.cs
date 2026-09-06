using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.ViewModels;

namespace SCANOVA.App.Views;

/// <summary>Conversão em lote (Fase 8): fila de arquivos, progresso, pausa/cancelamento e relatório final.</summary>
public sealed partial class BatchConvertPage : Page
{
    public BatchConvertViewModel ViewModel { get; }

    public BatchConvertPage()
    {
        ViewModel = App.Services.GetRequiredService<BatchConvertViewModel>();
        InitializeComponent();
    }
}
