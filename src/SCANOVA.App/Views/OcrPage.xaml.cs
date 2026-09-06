using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SCANOVA.App.ViewModels;

namespace SCANOVA.App.Views;

/// <summary>
/// Parâmetro de navegação opcional para <see cref="OcrPage"/> — usado quando o documento já foi
/// escolhido em outra tela (ex.: cartão "Extrair texto" do Início). Ao navegar pelo item "OCR" do
/// menu lateral diretamente, a página abre sem parâmetro e o próprio usuário escolhe o documento.
/// </summary>
public sealed record OcrPageParameter(string? SourcePath, SCANOVA.Core.Models.RasterImage Image);

/// <summary>
/// Reconhecimento de texto (OCR, Fase 9): abre um documento, reconhece o texto localmente
/// (Tesseract, seção 40/107), permite revisar/corrigir o resultado e salvar como TXT, DOCX ou
/// PDF pesquisável.
/// </summary>
public sealed partial class OcrPage : Page
{
    public OcrViewModel ViewModel { get; }

    public OcrPage()
    {
        ViewModel = App.Services.GetRequiredService<OcrViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is OcrPageParameter parameter)
        {
            await ViewModel.LoadAsync(parameter.SourcePath, parameter.Image);
        }
    }
}
