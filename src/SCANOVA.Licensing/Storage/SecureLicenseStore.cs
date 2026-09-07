using System.Security.Cryptography;
using System.Text;

namespace SCANOVA.Licensing.Storage;

/// <summary>
/// Guarda a chave de licença ativada em disco protegida por DPAPI (Windows Data Protection API,
/// <see cref="ProtectedData"/>, escopo <see cref="DataProtectionScope.CurrentUser"/>) — os bytes
/// gravados só podem ser decifrados pela mesma conta do Windows, na mesma máquina, o que também
/// impede (sem precisar embutir nenhum identificador de hardware na própria licença) que o
/// arquivo simplesmente copiado para outra máquina/conta continue "ativado" ali: a descriptografia
/// falha e <see cref="Load"/> devolve <c>null</c>, como se não houvesse licença ativada.
///
/// Fora do Windows, <see cref="ProtectedData"/> lança <see cref="PlatformNotSupportedException"/>
/// — degrada automaticamente para gravação sem criptografia (mesmo princípio de degradação
/// graciosa usado por <c>WiaScannerService</c> na Fase 4). Isso só é exercitado pelos testes
/// automatizados deste ambiente cross-platform: o aplicativo em si (SCANOVA.App, WinUI 3) só
/// roda no Windows, então em produção o caminho DPAPI é sempre o usado.
/// </summary>
internal sealed class SecureLicenseStore
{
    private readonly string _filePath;

    public SecureLicenseStore(string filePath)
    {
        _filePath = filePath;
    }

    public void Save(string licenseKey)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var protectedBytes = Protect(Encoding.UTF8.GetBytes(licenseKey));
        File.WriteAllBytes(_filePath, protectedBytes);
    }

    /// <summary>Devolve a chave de licença armazenada, ou <c>null</c> se não houver nenhuma ou se não puder ser lida (arquivo ausente, corrompido, adulterado, ou copiado de outra máquina/conta).</summary>
    public string? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_filePath);
            var bytes = Unprotect(protectedBytes);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // Best-effort: qualquer falha de leitura/descriptografia vira "sem licença
            // armazenada válida" — nunca uma exceção que derruba a inicialização do aplicativo.
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch
        {
            // Best-effort — desativar nunca deveria falhar por causa de um arquivo bloqueado.
        }
    }

    private static byte[] Protect(byte[] data) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : data;

    private static byte[] Unprotect(byte[] protectedData) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(protectedData, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : protectedData;
}
