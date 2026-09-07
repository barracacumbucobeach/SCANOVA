using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;

namespace SCANOVA.Batch;

/// <summary>Registro de injeção de dependência do serviço de conversão em lote.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScanovaBatch(this IServiceCollection services)
    {
        services.AddSingleton<IBatchProcessingService, BatchProcessingService>();
        return services;
    }
}
