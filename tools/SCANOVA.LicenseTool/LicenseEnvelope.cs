using System.Buffers.Binary;

namespace SCANOVA.LicenseTool;

/// <summary>
/// Formato binário da chave de licença do SCANOVA (versão 1 — ver docs/LICENSING.md):
///
/// <code>
/// [1 byte]  versão do formato (atualmente sempre 1)
/// [4 bytes] tamanho do payload JSON, inteiro little-endian
/// [N bytes] payload JSON (UTF-8) — campos de LicensePayload
/// [64 bytes] assinatura ECDSA P-256/SHA-256, formato IEEE P1363 de campo fixo (r || s, 32+32
///            bytes) — NÃO é o formato ASN.1/DER padrão do .NET, escolhido para manter o
///            envelope de tamanho previsível.
/// </code>
///
/// O envelope inteiro (todos os bytes acima, concatenados) é codificado em Base64Url (sem
/// padding, "+"→"-", "/"→"_") para virar a string de licença que o usuário cola no aplicativo.
/// Esta é uma segunda implementação independente do mesmo formato usado por
/// <c>SCANOVA.Licensing</c> para verificação — ver o comentário no .csproj desta ferramenta.
/// </summary>
public static class LicenseEnvelope
{
    private const byte CurrentVersion = 1;
    private const int SignatureLength = 64;

    public static string Encode(byte[] payloadJson, byte[] signature)
    {
        if (signature.Length != SignatureLength)
        {
            throw new ArgumentException($"A assinatura precisa ter {SignatureLength} bytes (ECDSA P-256, formato IEEE P1363 fixo).", nameof(signature));
        }

        var envelope = new byte[1 + 4 + payloadJson.Length + SignatureLength];
        envelope[0] = CurrentVersion;
        BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(1, 4), payloadJson.Length);
        payloadJson.CopyTo(envelope.AsSpan(5));
        signature.CopyTo(envelope.AsSpan(5 + payloadJson.Length));

        return Base64UrlEncode(envelope);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
