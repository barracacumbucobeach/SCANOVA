using System.Security.Cryptography;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Licensing.Signing;
using Xunit;

namespace SCANOVA.Licensing.Tests;

/// <summary>
/// Testes de ponta a ponta do serviço de licenciamento: ativação, status, informações e
/// desativação, com persistência real em um arquivo temporário — nunca a chave pública de
/// produção real (cada teste usa seu próprio par de chaves ECDSA descartável).
/// </summary>
public class LicenseServiceTests : IDisposable
{
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _licenseFilePath;

    public LicenseServiceTests()
    {
        _licenseFilePath = Path.Combine(Directory.CreateTempSubdirectory("scanova-license-service-test-").FullName, "license.dat");
    }

    public void Dispose()
    {
        _signingKey.Dispose();
        try
        {
            Directory.Delete(Path.GetDirectoryName(_licenseFilePath)!, recursive: true);
        }
        catch
        {
            // Best-effort.
        }
    }

    private ILicenseService CreateSut(IReadOnlyCollection<string>? revokedLicenseIds = null) =>
        new LicenseService(ImportPublicKey(_signingKey), revokedLicenseIds ?? [], _licenseFilePath);

    private string BuildKey(string licenseId = "LIC-0001", string product = "SCANOVA", string edition = "Standard", string? customerName = "Cliente de Teste") =>
        TestLicenseKeyBuilder.Build(_signingKey, new LicensePayload
        {
            LicenseId = licenseId,
            Product = product,
            Edition = edition,
            CustomerName = customerName,
            ActivationLimit = 1,
            CreatedAt = DateTime.UtcNow,
        });

    private static ECDsa ImportPublicKey(ECDsa keyPair)
    {
        var publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(keyPair.ExportSubjectPublicKeyInfo(), out _);
        return publicOnly;
    }

    [Fact]
    public void GetLicenseStatus_NoActivationYet_ReturnsUnlicensed()
    {
        var sut = CreateSut();

        Assert.Equal(LicenseStatus.Unlicensed, sut.GetLicenseStatus());
        Assert.False(sut.IsLicensed());
        Assert.Null(sut.GetLicenseInfo());
    }

    [Fact]
    public async Task ActivateAsync_ValidLicense_PersistsAndReportsLicensed()
    {
        var sut = CreateSut();

        var result = await sut.ActivateAsync(BuildKey());

        Assert.True(result.Success);
        Assert.True(sut.IsLicensed());
        Assert.Equal(LicenseStatus.Licensed, sut.GetLicenseStatus());
        Assert.Equal("LIC-0001", sut.GetLicenseInfo()!.LicenseId);
    }

    [Fact]
    public async Task ActivateAsync_TrimsWhitespaceAroundKey()
    {
        var sut = CreateSut();

        var result = await sut.ActivateAsync("   " + BuildKey() + "\n");

        Assert.True(result.Success);
        Assert.True(sut.IsLicensed());
    }

    [Fact]
    public async Task ActivateAsync_InvalidLicense_FailsAndStaysUnlicensed()
    {
        var sut = CreateSut();

        var result = await sut.ActivateAsync("chave-invalida");

        Assert.False(result.Success);
        Assert.NotNull(result.UserMessage);
        Assert.False(sut.IsLicensed());
        Assert.Equal(LicenseStatus.Unlicensed, sut.GetLicenseStatus());
    }

    [Fact]
    public async Task ActivateAsync_WrongProduct_Fails()
    {
        var sut = CreateSut();

        var result = await sut.ActivateAsync(BuildKey(product: "OUTRO-PRODUTO"));

        Assert.False(result.Success);
        Assert.False(sut.IsLicensed());
    }

    [Fact]
    public async Task ActivateAsync_RevokedLicense_FailsAndDoesNotPersist()
    {
        var sut = CreateSut(revokedLicenseIds: ["LIC-REVOGADA"]);

        var result = await sut.ActivateAsync(BuildKey("LIC-REVOGADA"));

        Assert.False(result.Success);
        Assert.False(sut.IsLicensed());
        Assert.Equal(LicenseStatus.Unlicensed, sut.GetLicenseStatus()); // nada foi persistido
    }

    [Fact]
    public async Task ActivateAsync_EmptyKey_ThrowsArgumentException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ActivateAsync(""));
    }

    [Fact]
    public async Task ActivateAsync_CancelledBeforeStart_ThrowsOperationCanceledException()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ActivateAsync(BuildKey(), cts.Token));
    }

    [Fact]
    public async Task DeactivateAsync_AfterActivation_ReturnsToUnlicensed()
    {
        var sut = CreateSut();
        await sut.ActivateAsync(BuildKey());
        Assert.True(sut.IsLicensed());

        await sut.DeactivateAsync();

        Assert.False(sut.IsLicensed());
        Assert.Equal(LicenseStatus.Unlicensed, sut.GetLicenseStatus());
        Assert.Null(sut.GetLicenseInfo());
    }

    [Fact]
    public async Task DeactivateAsync_WhenNeverActivated_DoesNotThrow()
    {
        var sut = CreateSut();

        var exception = await Record.ExceptionAsync(() => sut.DeactivateAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task Activation_PersistsAcrossServiceInstances()
    {
        // Prova que a persistência é via arquivo, não estado em memória: uma segunda instância
        // apontando para o mesmo arquivo enxerga a mesma licença ativada pela primeira.
        var first = CreateSut();
        await first.ActivateAsync(BuildKey());

        var second = CreateSut();

        Assert.True(second.IsLicensed());
        Assert.Equal("LIC-0001", second.GetLicenseInfo()!.LicenseId);
    }

    [Fact]
    public void GetLicenseStatus_CorruptedStoredFile_ReturnsInvalid()
    {
        // Escreve texto puro diretamente no arquivo, sem passar por SecureLicenseStore.Save —
        // simula um arquivo adulterado. Neste ambiente (fora do Windows), o armazenamento não
        // usa DPAPI, então o texto chega intacto até o validador de chave, que rejeita o
        // formato → Invalid. No Windows real, o mesmo arquivo geralmente falharia primeiro na
        // descriptografia DPAPI (SecureLicenseStore.Load devolve null) → Unlicensed em vez de
        // Invalid — os dois resultados são igualmente seguros (nenhum trata o arquivo adulterado
        // como uma licença válida).
        Directory.CreateDirectory(Path.GetDirectoryName(_licenseFilePath)!);
        File.WriteAllText(_licenseFilePath, "isto-nao-e-uma-licenca-valida");
        var sut = CreateSut();

        Assert.Equal(LicenseStatus.Invalid, sut.GetLicenseStatus());
        Assert.False(sut.IsLicensed());
    }
}
