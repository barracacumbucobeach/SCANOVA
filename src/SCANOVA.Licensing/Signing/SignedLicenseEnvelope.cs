using System.Buffers.Binary;

namespace SCANOVA.Licensing.Signing;

/// <summary>
/// Decodifica o formato binário de uma chave de licença do SCANOVA (versão 1 — ver
/// <c>docs/LICENSING.md</c> para a especificação completa e <c>tools/SCANOVA.LicenseTool</c>
/// para quem a produz):
///
/// <code>
/// [1 byte]   versão do formato
/// [4 bytes]  tamanho do payload JSON, inteiro little-endian
/// [N bytes]  payload JSON (UTF-8)
/// [64 bytes] assinatura ECDSA P-256/SHA-256, formato IEEE P1363 de campo fixo (r || s)
/// </code>
///
/// codificado como Base64Url. Este projeto só precisa DECODIFICAR (verificar) — nunca emite
/// licenças (isso é responsabilidade exclusiva da ferramenta do fabricante, que roda offline com
/// a chave privada, nunca distribuída com o aplicativo).
/// </summary>
internal static class SignedLicenseEnvelope
{
    private const byte CurrentVersion = 1;
    private const int SignatureLength = 64;
    private const int HeaderLength = 1 + 4;

    public static bool TryDecode(string licenseKey, out byte[] payloadJson, out byte[] signature, out string? error)
    {
        payloadJson = [];
        signature = [];

        byte[] envelope;
        try
        {
            envelope = Base64UrlDecode(licenseKey.Trim());
        }
        catch (FormatException)
        {
            error = "A chave de licença informada não está em um formato reconhecido.";
            return false;
        }

        if (envelope.Length < HeaderLength + SignatureLength)
        {
            error = "A chave de licença informada está incompleta.";
            return false;
        }

        var version = envelope[0];
        if (version != CurrentVersion)
        {
            error = $"Esta versão do SCANOVA não reconhece o formato de licença informado (versão {version}).";
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(envelope.AsSpan(1, 4));
        var expectedLength = HeaderLength + payloadLength + SignatureLength;
        if (payloadLength < 0 || envelope.Length != expectedLength)
        {
            error = "A chave de licença informada está corrompida.";
            return false;
        }

        payloadJson = envelope[HeaderLength..(HeaderLength + payloadLength)];
        signature = envelope[(HeaderLength + payloadLength)..];
        error = null;
        return true;
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        var padding = (4 - (base64.Length % 4)) % 4;
        base64 = base64.PadRight(base64.Length + padding, '=');
        return Convert.FromBase64String(base64);
    }
}
