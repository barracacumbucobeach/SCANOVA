using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;
using SCANOVA.Scanner.Wia;

namespace SCANOVA.Scanner;

/// <summary>
/// Registro de injeção de dependência do serviço de digitalização. Registra apenas
/// <see cref="WiaScannerService"/> (o serviço real) — a implementação mock existe somente no
/// projeto de testes (seção 140: "Scanners podem ter uma implementação mock somente no
/// ambiente de testes"). Marcado como Windows-only porque só faz sentido chamar isto a partir
/// do composition root de <c>SCANOVA.App</c>, que só compila/roda no Windows de qualquer forma.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScanovaScanner(this IServiceCollection services)
    {
        services.AddSingleton<IScannerService, WiaScannerService>();
        return services;
    }
}
