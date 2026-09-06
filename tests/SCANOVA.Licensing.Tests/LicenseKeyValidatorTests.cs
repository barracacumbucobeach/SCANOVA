using System.Security.Cryptography;
using SCANOVA.Core.Enums;
using SCANOVA.Licensing.Signing;
using SCANOVA.Licensing.Verification;
using Xunit;

namespace SCANOVA.Licensing.Tests;

/// <summary>
/// Testes do validador de chave de licença, usando um par de chaves ECDSA descartável gerado
/// só para este teste — nunca a chave pública de produção real.
/// </summary>
public class LicenseKeyValidatorTests
{
    private static LicensePayload SamplePayload(string licenseId = "LIC-0001") => new()
    {
        LicenseId = licenseId,
        Product = "SCANOVA",
        Edition = "Standard",
        CustomerName = "Cliente de Teste",
        ActivationLimit = 1,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void Validate_ValidSignedLicense_ReturnsLicensedWithMatchingInfo()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(keyPair), revokedLicenseIds: []);

        var key = TestLicenseKeyBuilder.Build(keyPair, SamplePayload());
        var outcome = validator.Validate(key);

        Assert.Equal(LicenseStatus.Licensed, outcome.Status);
        Assert.NotNull(outcome.Info);
        Assert.Equal("LIC-0001", outcome.Info!.LicenseId);
        Assert.Equal("SCANOVA", outcome.Info.Product);
        Assert.Equal("Standard", outcome.Info.Edition);
        Assert.Equal("Cliente de Teste", outcome.Info.CustomerName);
        Assert.Equal(LicenseStatus.Licensed, outcome.Info.Status);
    }

    [Fact]
    public void Validate_SignedByDifferentKey_ReturnsInvalidWithoutInfo()
    {
        using var realKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var attackerKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(realKeyPair), revokedLicenseIds: []);

        // Assinada por uma chave diferente da configurada no validador (nunca confere).
        var key = TestLicenseKeyBuilder.Build(attackerKeyPair, SamplePayload());
        var outcome = validator.Validate(key);

        Assert.Equal(LicenseStatus.Invalid, outcome.Status);
        Assert.Null(outcome.Info);
    }

    [Fact]
    public void Validate_TamperedPayloadAfterSigning_ReturnsInvalid()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(keyPair), revokedLicenseIds: []);

        var key = TestLicenseKeyBuilder.Build(keyPair, SamplePayload());
        var tampered = TamperWithPayload(key);

        var outcome = validator.Validate(tampered);

        Assert.Equal(LicenseStatus.Invalid, outcome.Status);
        Assert.Null(outcome.Info);
    }

    [Fact]
    public void Validate_WrongProduct_ReturnsInvalidButExposesInfo()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(keyPair), revokedLicenseIds: []);

        var payload = new LicensePayload { LicenseId = "LIC-0002", Product = "OUTRO-PRODUTO", Edition = "Standard" };
        var key = TestLicenseKeyBuilder.Build(keyPair, payload);

        var outcome = validator.Validate(key);

        // Assinatura confere (é genuína), só não é para este produto — por isso os campos
        // continuam expostos (a autenticidade foi verificada), diferente dos casos de assinatura
        // inválida acima.
        Assert.Equal(LicenseStatus.Invalid, outcome.Status);
        Assert.NotNull(outcome.Info);
        Assert.Equal("OUTRO-PRODUTO", outcome.Info!.Product);
    }

    [Fact]
    public void Validate_RevokedLicenseId_ReturnsRevokedWithInfo()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(keyPair), revokedLicenseIds: ["LIC-REVOGADA"]);

        var key = TestLicenseKeyBuilder.Build(keyPair, SamplePayload("LIC-REVOGADA"));
        var outcome = validator.Validate(key);

        Assert.Equal(LicenseStatus.Revoked, outcome.Status);
        Assert.NotNull(outcome.Info);
        Assert.Equal(LicenseStatus.Revoked, outcome.Info!.Status);
        Assert.NotNull(outcome.UserMessage);
    }

    [Fact]
    public void Validate_MalformedKey_ReturnsInvalidWithoutThrowing()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(keyPair), revokedLicenseIds: []);

        var outcome = validator.Validate("isto-nao-e-uma-chave-de-licenca!!!");

        Assert.Equal(LicenseStatus.Invalid, outcome.Status);
        Assert.Null(outcome.Info);
        Assert.NotNull(outcome.UserMessage);
    }

    [Fact]
    public void Validate_UnsupportedEnvelopeVersion_ReturnsInvalid()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var validator = new LicenseKeyValidator(ImportPublicKey(keyPair), revokedLicenseIds: []);

        var key = TestLicenseKeyBuilder.Build(keyPair, SamplePayload(), version: 99);
        var outcome = validator.Validate(key);

        Assert.Equal(LicenseStatus.Invalid, outcome.Status);
    }

    /// <summary>Corrompe um byte do trecho de payload de uma chave codificada (sem tocar na assinatura), simulando adulteração após a assinatura.</summary>
    private static string TamperWithPayload(string licenseKey)
    {
        var envelope = Convert.FromBase64String(PadBase64(licenseKey.Replace('-', '+').Replace('_', '/')));
        envelope[6] ^= 0xFF; // um byte dentro do JSON do payload (após o cabeçalho de 5 bytes)
        return TestLicenseKeyBuilder.Base64UrlEncode(envelope);
    }

    private static string PadBase64(string value)
    {
        var padding = (4 - (value.Length % 4)) % 4;
        return value.PadRight(value.Length + padding, '=');
    }

    /// <summary>Importa só a parte pública de um par de chaves gerado para teste (o validador nunca recebe a chave privada).</summary>
    private static ECDsa ImportPublicKey(ECDsa keyPair)
    {
        var publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(keyPair.ExportSubjectPublicKeyInfo(), out _);
        return publicOnly;
    }
}
