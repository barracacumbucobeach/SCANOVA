using System.Text;
using SCANOVA.Licensing.Signing;
using Xunit;

namespace SCANOVA.Licensing.Tests;

public class SignedLicenseEnvelopeTests
{
    private static readonly byte[] SamplePayload = Encoding.UTF8.GetBytes("""{"LicenseId":"LIC-0001"}""");
    private static readonly byte[] SampleSignature = Enumerable.Repeat((byte)0x42, 64).ToArray();

    [Fact]
    public void TryDecode_ValidEnvelope_RoundTripsPayloadAndSignature()
    {
        var key = TestLicenseKeyBuilder.BuildEnvelope(version: 1, SamplePayload, SampleSignature);

        var ok = SignedLicenseEnvelope.TryDecode(key, out var payload, out var signature, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(SamplePayload, payload);
        Assert.Equal(SampleSignature, signature);
    }

    [Fact]
    public void TryDecode_ToleratesSurroundingWhitespace()
    {
        var key = "  " + TestLicenseKeyBuilder.BuildEnvelope(1, SamplePayload, SampleSignature) + "\n";

        var ok = SignedLicenseEnvelope.TryDecode(key, out _, out _, out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void TryDecode_InvalidBase64_ReturnsFalseWithMessage()
    {
        var ok = SignedLicenseEnvelope.TryDecode("###não é base64###", out _, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryDecode_TooShortToContainSignature_ReturnsFalse()
    {
        var tooShort = TestLicenseKeyBuilder.Base64UrlEncode([1, 2, 3]);

        var ok = SignedLicenseEnvelope.TryDecode(tooShort, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryDecode_UnsupportedVersion_ReturnsFalse()
    {
        var key = TestLicenseKeyBuilder.BuildEnvelope(version: 7, SamplePayload, SampleSignature);

        var ok = SignedLicenseEnvelope.TryDecode(key, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("vers", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecode_PayloadLengthLiesAboutEnvelopeSize_ReturnsFalse()
    {
        // Constrói manualmente um envelope cujo campo de tamanho não bate com o conteúdo real.
        var envelope = new byte[1 + 4 + SamplePayload.Length + SampleSignature.Length];
        envelope[0] = 1;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(1, 4), SamplePayload.Length + 100); // mentira
        SamplePayload.CopyTo(envelope.AsSpan(5));
        SampleSignature.CopyTo(envelope.AsSpan(5 + SamplePayload.Length));
        var key = TestLicenseKeyBuilder.Base64UrlEncode(envelope);

        var ok = SignedLicenseEnvelope.TryDecode(key, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryDecode_NegativePayloadLength_ReturnsFalseWithoutThrowing()
    {
        var envelope = new byte[1 + 4 + SampleSignature.Length];
        envelope[0] = 1;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(1, 4), -1);
        SampleSignature.CopyTo(envelope.AsSpan(5));
        var key = TestLicenseKeyBuilder.Base64UrlEncode(envelope);

        var ok = SignedLicenseEnvelope.TryDecode(key, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }
}
