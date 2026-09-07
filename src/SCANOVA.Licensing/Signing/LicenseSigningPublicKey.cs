using System.Security.Cryptography;

namespace SCANOVA.Licensing.Signing;

/// <summary>
/// Chave pública ECDSA P-256 (NIST P-256/secp256r1, SubjectPublicKeyInfo/X.509, Base64) usada
/// para verificar a assinatura de qualquer chave de licença do SCANOVA.
///
/// A chave PRIVADA correspondente NUNCA fica neste repositório nem em nenhum artefato
/// distribuído com o aplicativo (é o único jeito de garantir que ninguém consiga forjar uma
/// licença a partir do código-fonte ou do executável) — ela é gerada e mantida totalmente fora
/// do controle de versão, só pelo fabricante, e usada exclusivamente pela ferramenta de emissão
/// de licenças (<c>tools/SCANOVA.LicenseTool</c>) para assinar novas chaves. Ver
/// <c>docs/LICENSING.md</c> para o processo completo de geração/rotação de chaves.
/// </summary>
internal static class LicenseSigningPublicKey
{
    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEpfDtOvshE/fTPn2wz4IWX/mNbkMbnKIyNqw96nEODih9UgamTDs/kFsDHAyxCHswBkR9pNNlWxkMvpuU2pnN0A==";

    /// <summary>Importa a chave pública embutida. O chamador é dono da instância e deve descartá-la.</summary>
    public static ECDsa Load()
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);
        return ecdsa;
    }
}
