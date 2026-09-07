using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using SCANOVA.Licensing.Signing;

namespace SCANOVA.Licensing.Tests;

/// <summary>
/// Constrói chaves de licença assinadas para teste — uma implementação independente e
/// deliberadamente separada da usada em produção (que só decodifica, nunca assina), mirando o
/// mesmo formato de envelope documentado em <c>docs/LICENSING.md</c>. Nunca usa a chave de
/// produção real (embutida em <c>LicenseSigningPublicKey</c>) — cada teste gera seu próprio par
/// de chaves ECDSA descartável.
/// </summary>
internal static class TestLicenseKeyBuilder
{
    public static string Build(ECDsa signingKey, LicensePayload payload, byte version = 1, byte[]? signatureOverride = null)
    {
        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = signatureOverride
            ?? signingKey.SignData(payloadJson, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return BuildEnvelope(version, payloadJson, signature);
    }

    public static string BuildEnvelope(byte version, byte[] payloadJson, byte[] signature)
    {
        var envelope = new byte[1 + 4 + payloadJson.Length + signature.Length];
        envelope[0] = version;
        BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(1, 4), payloadJson.Length);
        payloadJson.CopyTo(envelope.AsSpan(5));
        signature.CopyTo(envelope.AsSpan(5 + payloadJson.Length));
        return Base64UrlEncode(envelope);
    }

    public static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
