using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;

namespace SCANOVA.Ocr;

/// <summary>Registro de injeção de dependência dos serviços de OCR.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra os serviços de OCR. <paramref name="languageDataCacheFolder"/> é a pasta onde os
    /// modelos de idioma (.traineddata) ficam guardados após o primeiro download — normalmente
    /// <c>AppPaths.OcrLanguageDataFolder</c> (SCANOVA.Infrastructure), passada pelo composition
    /// root para este projeto não precisar referenciar Infrastructure.
    /// </summary>
    public static IServiceCollection AddScanovaOcr(this IServiceCollection services, string languageDataCacheFolder)
    {
        services.AddSingleton<IOcrService>(sp =>
            new TesseractOcrService(sp.GetRequiredService<IImageExporter>(), languageDataCacheFolder));
        services.AddSingleton<ITextDocumentExporter, OpenXmlTextDocumentExporter>();
        return services;
    }
}
