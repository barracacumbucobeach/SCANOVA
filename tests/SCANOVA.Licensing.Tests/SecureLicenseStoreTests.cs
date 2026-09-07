using SCANOVA.Licensing.Storage;
using Xunit;

namespace SCANOVA.Licensing.Tests;

/// <summary>
/// Testa o roundtrip de armazenamento independentemente de DPAPI estar disponível: fora do
/// Windows, <see cref="SecureLicenseStore"/> degrada para gravação sem criptografia (só usada
/// pelos testes automatizados — o aplicativo em si só roda no Windows, onde o caminho real é
/// sempre o protegido por DPAPI). O contrato testado aqui (salvar/carregar/apagar, nunca lançar)
/// vale nos dois casos.
/// </summary>
public class SecureLicenseStoreTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("scanova-license-store-test-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort.
        }
    }

    private string FilePath(string name = "license.dat") => Path.Combine(_tempDir, name);

    [Fact]
    public void Load_NoFileSaved_ReturnsNull()
    {
        var store = new SecureLicenseStore(FilePath());

        Assert.Null(store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsExactString()
    {
        var store = new SecureLicenseStore(FilePath());
        const string licenseKey = "uma-chave-de-licenca-de-teste-qualquer==";

        store.Save(licenseKey);

        Assert.Equal(licenseKey, store.Load());
    }

    [Fact]
    public void Save_CreatesParentDirectoryAutomatically()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "pasta", "license.dat");
        var store = new SecureLicenseStore(nestedPath);

        store.Save("chave");

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Delete_RemovesStoredLicense()
    {
        var store = new SecureLicenseStore(FilePath());
        store.Save("chave");

        store.Delete();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Delete_WhenNoFileExists_DoesNotThrow()
    {
        var store = new SecureLicenseStore(FilePath());

        var exception = Record.Exception(store.Delete);

        Assert.Null(exception);
    }

    [Fact]
    public void Load_WhenPathUnexpectedlyPointsToADirectory_ReturnsNullInsteadOfThrowing()
    {
        // File.Exists() já devolve falso para um diretório, então Load() nunca chega a tentar
        // ler bytes dele — mas o contrato ("nunca lança, no máximo devolve null") continua
        // valendo mesmo nesse caso incomum.
        var directoryAsFilePath = Path.Combine(_tempDir, "isto-e-um-diretorio");
        Directory.CreateDirectory(directoryAsFilePath);
        var store = new SecureLicenseStore(directoryAsFilePath);

        string? result = null;
        var exception = Record.Exception(() => result = store.Load());

        Assert.Null(exception);
        Assert.Null(result);
    }

    [Fact]
    public void Load_WhenStoredFileIsEmpty_ReturnsEmptyStringInsteadOfThrowing()
    {
        var path = FilePath();
        Directory.CreateDirectory(_tempDir);
        File.WriteAllBytes(path, []);
        var store = new SecureLicenseStore(path);

        var exception = Record.Exception(() => store.Load());

        Assert.Null(exception);
    }
}
