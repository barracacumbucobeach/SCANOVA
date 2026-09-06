using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;
using SCANOVA.Infrastructure.FileSystem;
using SCANOVA.Infrastructure.Logging;
using SCANOVA.Infrastructure.Settings;

namespace SCANOVA.Infrastructure;

/// <summary>Registro de injeção de dependência dos serviços de infraestrutura.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScanovaInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ILogService, SerilogLogService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<TempFileManager>();
        return services;
    }
}
