using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;
using SCANOVA.Tiff.TiffEncoder;
using SCANOVA.Tiff.TiffValidator;

namespace SCANOVA.Tiff;

/// <summary>Registro de injeção de dependência dos serviços de TIFF (BitMiracle.LibTiff.NET).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScanovaTiff(this IServiceCollection services)
    {
        services.AddSingleton<ITiffEncoder, LibTiffEncoder>();
        services.AddSingleton<ITiffValidator, LibTiffValidator>();
        services.AddSingleton<ITiffDocumentPipeline, TiffDocumentPipeline>();
        return services;
    }
}
