using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;
using SCANOVA.Licensing.Revocation;
using SCANOVA.Licensing.Signing;

namespace SCANOVA.Licensing;

/// <summary>Registro de injeção de dependência do serviço de licenciamento.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="ILicenseService"/> com a chave pública de produção embutida e a
    /// lista de revogação embutida. <paramref name="licenseFilePath"/> é normalmente
    /// <c>AppPaths.LicenseFilePath</c> (SCANOVA.Infrastructure), passada pelo composition root
    /// para este projeto não precisar referenciar Infrastructure.
    /// </summary>
    public static IServiceCollection AddScanovaLicensing(this IServiceCollection services, string licenseFilePath)
    {
        services.AddSingleton<ILicenseService>(_ =>
            new LicenseService(LicenseSigningPublicKey.Load(), RevokedLicenseRegistry.Load(), licenseFilePath));
        return services;
    }
}
