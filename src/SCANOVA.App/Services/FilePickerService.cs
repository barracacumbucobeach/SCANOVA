using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SCANOVA.App.Services;

/// <summary>
/// Implementação de <see cref="IFilePickerService"/> com <see cref="FileOpenPicker"/>/
/// <see cref="FileSavePicker"/>. Como o SCANOVA roda sem empacotamento MSIX (ver
/// SCANOVA.App.csproj), o picker precisa ser explicitamente associado ao HWND da janela
/// principal via <see cref="InitializeWithWindow"/> — sem isso, a chamada falha em runtime.
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickImageFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".tif");
        picker.FileTypeFilter.Add(".tiff");

        InitializeWithWindow.Initialize(picker, GetActiveWindowHandle());

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<IReadOnlyList<string>> PickMultipleImageFilesAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");

        InitializeWithWindow.Initialize(picker, GetActiveWindowHandle());

        var files = await picker.PickMultipleFilesAsync();
        return files.Select(f => f.Path).ToList();
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, IReadOnlyDictionary<string, IList<string>> fileTypeChoices)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = suggestedFileName,
        };

        foreach (var choice in fileTypeChoices)
        {
            picker.FileTypeChoices.Add(choice.Key, choice.Value);
        }

        InitializeWithWindow.Initialize(picker, GetActiveWindowHandle());

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private static IntPtr GetActiveWindowHandle()
    {
        var window = App.MainAppWindow ?? throw new InvalidOperationException("A janela principal ainda não foi criada.");
        return WindowNative.GetWindowHandle(window);
    }
}
