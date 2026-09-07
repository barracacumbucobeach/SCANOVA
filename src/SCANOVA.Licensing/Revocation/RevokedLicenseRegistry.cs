using System.Text.Json;

namespace SCANOVA.Licensing.Revocation;

/// <summary>
/// Lê a lista de <c>LicenseId</c> revogados (fraude, estorno etc.) a partir do recurso embutido
/// <c>Revocation/RevokedLicenses.json</c> — um array JSON simples de strings, atualizado a cada
/// versão lançada do SCANOVA. Nunca consulta a internet: revogação funciona 100% offline, ao
/// custo de só ter efeito prático quando o cliente atualiza o aplicativo (compromisso aceito
/// para manter o SCANOVA sem nenhuma dependência de rede — seção 40, mesmo princípio do OCR).
/// </summary>
internal static class RevokedLicenseRegistry
{
    private const string ResourceName = "SCANOVA.Licensing.Revocation.RevokedLicenses.json";

    public static IReadOnlyCollection<string> Load()
    {
        using var stream = typeof(RevokedLicenseRegistry).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embutido \"{ResourceName}\" não encontrado.");

        var ids = JsonSerializer.Deserialize<string[]>(stream) ?? [];
        return new HashSet<string>(ids, StringComparer.Ordinal);
    }
}
