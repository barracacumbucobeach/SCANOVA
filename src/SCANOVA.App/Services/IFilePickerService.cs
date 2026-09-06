namespace SCANOVA.App.Services;

/// <summary>Abstrai os pickers de arquivo do Windows para que ViewModels não dependam diretamente do WinRT.</summary>
public interface IFilePickerService
{
    /// <summary>Abre o seletor de arquivo para escolher uma imagem. Retorna <c>null</c> se o usuário cancelar.</summary>
    Task<string?> PickImageFileAsync();

    /// <summary>
    /// Abre o seletor de "salvar como". <paramref name="fileTypeChoices"/> mapeia um rótulo
    /// (ex.: "Imagem PNG") para a lista de extensões aceitas (ex.: [".png"]).
    /// </summary>
    Task<string?> PickSaveFileAsync(string suggestedFileName, IReadOnlyDictionary<string, IList<string>> fileTypeChoices);
}
