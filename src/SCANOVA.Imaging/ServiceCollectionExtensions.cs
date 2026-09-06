using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;
using SCANOVA.Imaging.Composition;
using SCANOVA.Imaging.Detection;
using SCANOVA.Imaging.Enhancement;
using SCANOVA.Imaging.ImageLoading;
using SCANOVA.Imaging.ImageProcessing;

namespace SCANOVA.Imaging;

/// <summary>Registro de injeção de dependência dos serviços de imagem (SkiaSharp).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScanovaImaging(this IServiceCollection services)
    {
        services.AddSingleton<IImageLoader, SkiaImageLoader>();
        services.AddSingleton<IImageExporter, SkiaImageExporter>();
        services.AddSingleton<IImageService, SkiaImageService>();
        services.AddSingleton<IDocumentDetectionService, DocumentDetectionService>();
        services.AddSingleton<IDocumentEnhancementService, DocumentEnhancementService>();
        services.AddSingleton<IDuplexCompositionService, DuplexCompositionService>();
        return services;
    }
}
