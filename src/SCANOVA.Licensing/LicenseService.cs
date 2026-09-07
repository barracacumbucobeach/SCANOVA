using System.Security.Cryptography;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Licensing.Storage;
using SCANOVA.Licensing.Verification;

namespace SCANOVA.Licensing;

/// <summary>
/// Implementação de <see cref="ILicenseService"/> (seção 54): licença vitalícia, ativada por
/// uma chave de licença assinada digitalmente (ECDSA P-256) que o cliente recebe após a compra
/// e cola no aplicativo — nunca é necessário nenhum servidor de ativação nem conexão com a
/// internet (a assinatura é verificada localmente, com a chave pública embutida no aplicativo).
///
/// <see cref="LicenseInfo.ActivationLimit"/> é informativo (quantas máquinas a licença cobre,
/// para o cliente e para o suporte) — não é imposto tecnicamente por este serviço, porque isso
/// exigiria um servidor central que soubesse quantas vezes cada licença já foi ativada, o que
/// contradiria o funcionamento 100% offline (decisão consciente, ver <c>docs/LICENSING.md</c>).
///
/// O construtor recebe a chave pública e a lista de revogados por injeção (nunca acessa
/// <c>LicenseSigningPublicKey</c>/<c>RevokedLicenseRegistry</c> diretamente) para poder ser
/// testado com um par de chaves de teste — ver <see cref="ServiceCollectionExtensions"/> para a
/// instância real usada em produção.
/// </summary>
public sealed class LicenseService : ILicenseService, IDisposable
{
    private readonly LicenseKeyValidator _validator;
    private readonly SecureLicenseStore _store;

    public LicenseService(ECDsa publicKey, IReadOnlyCollection<string> revokedLicenseIds, string licenseFilePath)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(revokedLicenseIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseFilePath);

        _validator = new LicenseKeyValidator(publicKey, revokedLicenseIds);
        _store = new SecureLicenseStore(licenseFilePath);
    }

    public bool IsLicensed() => GetLicenseStatus() == LicenseStatus.Licensed;

    public LicenseStatus GetLicenseStatus() => Evaluate().Status;

    public LicenseInfo? GetLicenseInfo() => Evaluate().Info;

    public Task<ProcessingResult> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseKey);
        cancellationToken.ThrowIfCancellationRequested();

        var trimmed = licenseKey.Trim();
        var outcome = _validator.Validate(trimmed);

        if (outcome.Status == LicenseStatus.Invalid)
        {
            return Task.FromResult(ProcessingResult.Fail(
                outcome.UserMessage ?? "Não foi possível ativar esta chave de licença.",
                outcome.TechnicalDetail));
        }

        if (outcome.Status == LicenseStatus.Revoked)
        {
            // Assinatura genuína, mas a licença foi revogada — não ativa, mesmo sendo "válida"
            // no sentido criptográfico.
            return Task.FromResult(ProcessingResult.Fail(
                outcome.UserMessage ?? "Esta licença foi revogada.",
                outcome.TechnicalDetail));
        }

        // outcome.Status == Licensed: assinatura confere, é do SCANOVA, não está revogada.
        _store.Save(trimmed);

        // ProcessingResult.Ok exige um caminho de saída — não se aplica à ativação de licença;
        // string vazia é inofensiva porque a UI de ativação nunca lê OutputPath.
        return Task.FromResult(ProcessingResult.Ok(string.Empty));
    }

    public Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.Delete();
        return Task.CompletedTask;
    }

    private LicenseValidationOutcome Evaluate()
    {
        var storedKey = _store.Load();
        return storedKey is null ? LicenseValidationOutcome.Unlicensed : _validator.Validate(storedKey);
    }

    public void Dispose() => _validator.Dispose();
}
