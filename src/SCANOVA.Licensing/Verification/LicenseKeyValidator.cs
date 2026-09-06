using System.Security.Cryptography;
using System.Text.Json;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Models;
using SCANOVA.Licensing.Signing;

namespace SCANOVA.Licensing.Verification;

/// <summary>
/// Valida uma chave de licença: decodifica o envelope, verifica a assinatura ECDSA contra a
/// chave pública informada, confere se é uma licença do SCANOVA e se não está na lista de
/// revogação — tudo local, sem nenhuma chamada de rede (seção 40).
///
/// Recebe a chave pública e a lista de revogados por injeção (em vez de usar
/// <see cref="LicenseSigningPublicKey"/>/<c>RevokedLicenseRegistry</c> diretamente) para poder
/// ser testado com um par de chaves de teste, sem qualquer relação com a chave de produção real.
/// </summary>
internal sealed class LicenseKeyValidator : IDisposable
{
    private const string ExpectedProduct = "SCANOVA";

    private readonly ECDsa _publicKey;
    private readonly IReadOnlyCollection<string> _revokedLicenseIds;

    public LicenseKeyValidator(ECDsa publicKey, IReadOnlyCollection<string> revokedLicenseIds)
    {
        _publicKey = publicKey;
        _revokedLicenseIds = revokedLicenseIds;
    }

    public LicenseValidationOutcome Validate(string licenseKey)
    {
        if (!SignedLicenseEnvelope.TryDecode(licenseKey, out var payloadJson, out var signature, out var decodeError))
        {
            return new LicenseValidationOutcome(LicenseStatus.Invalid, null, decodeError, decodeError);
        }

        bool signatureValid;
        try
        {
            signatureValid = _publicKey.VerifyData(payloadJson, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException ex)
        {
            // Assinatura em formato inesperado (ex.: tamanho errado) — trata como inválida em
            // vez de deixar a exceção escapar (seção 60).
            return new LicenseValidationOutcome(
                LicenseStatus.Invalid,
                null,
                "A chave de licença não pôde ser validada. Verifique se ela foi copiada corretamente.",
                $"Falha ao verificar assinatura: {ex.Message}");
        }

        if (!signatureValid)
        {
            return new LicenseValidationOutcome(
                LicenseStatus.Invalid,
                null,
                "A chave de licença não pôde ser validada. Verifique se ela foi copiada corretamente.",
                "Assinatura ECDSA não confere.");
        }

        LicensePayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadJson)
                ?? throw new InvalidOperationException("Payload desserializado é nulo.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // A assinatura confere, mas o conteúdo não é um LicensePayload válido — não deveria
            // acontecer com uma chave emitida pela ferramenta oficial, mas nunca confia
            // cegamente mesmo com a assinatura OK.
            return new LicenseValidationOutcome(
                LicenseStatus.Invalid,
                null,
                "A chave de licença está corrompida.",
                $"JSON inválido apesar de assinatura válida: {ex.Message}");
        }

        if (!string.Equals(payload.Product, ExpectedProduct, StringComparison.Ordinal))
        {
            return new LicenseValidationOutcome(
                LicenseStatus.Invalid,
                ToLicenseInfo(payload, LicenseStatus.Invalid),
                "Esta chave de licença não é para o SCANOVA.",
                $"Product=\"{payload.Product}\", esperado \"{ExpectedProduct}\".");
        }

        if (_revokedLicenseIds.Contains(payload.LicenseId))
        {
            return new LicenseValidationOutcome(
                LicenseStatus.Revoked,
                ToLicenseInfo(payload, LicenseStatus.Revoked),
                "Esta licença foi revogada. Entre em contato com o suporte.",
                $"LicenseId=\"{payload.LicenseId}\" está na lista de revogação.");
        }

        return new LicenseValidationOutcome(LicenseStatus.Licensed, ToLicenseInfo(payload, LicenseStatus.Licensed), null, null);
    }

    private static LicenseInfo ToLicenseInfo(LicensePayload payload, LicenseStatus status) => new()
    {
        LicenseId = payload.LicenseId,
        Product = payload.Product,
        Edition = payload.Edition,
        CustomerName = payload.CustomerName,
        ActivationLimit = payload.ActivationLimit,
        CreatedAt = payload.CreatedAt,
        Status = status,
    };

    public void Dispose() => _publicKey.Dispose();
}
