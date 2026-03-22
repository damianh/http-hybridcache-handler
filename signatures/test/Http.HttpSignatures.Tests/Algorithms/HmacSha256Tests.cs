// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="HmacSha256SignatureAlgorithm"/>.
/// HMAC-SHA256 is deterministic, so we can verify exact output.
/// Includes RFC 9421 Appendix B.2.5 test vector.
/// </summary>
public sealed class HmacSha256Tests
{
    private static readonly HmacSha256SignatureAlgorithm Algorithm = new();

    [Fact]
    public void AlgorithmName_IsCorrect() =>
        Algorithm.AlgorithmName.ShouldBe("hmac-sha256");

    [Fact]
    public void Sign_ProducesCorrectLength()
    {
        var key = RfcTestKeys.HmacSharedSigningKey;
        var data = "test data"u8;

        var signature = Algorithm.Sign(data, key);

        // HMAC-SHA256 output is always 32 bytes
        signature.Length.ShouldBe(32);
    }

    [Fact]
    public void Sign_IsDeterministic()
    {
        var key = RfcTestKeys.HmacSharedSigningKey;
        var data = "deterministic test"u8;

        var sig1 = Algorithm.Sign(data, key);
        var sig2 = Algorithm.Sign(data, key);

        sig1.ShouldBe(sig2);
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var signingKey = RfcTestKeys.HmacSharedSigningKey;
        var verificationKey = RfcTestKeys.HmacSharedVerificationKey;
        var data = "verify me"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var result = Algorithm.Verify(data, verificationKey, signature);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.HmacSharedSigningKey;
        var verificationKey = RfcTestKeys.HmacSharedVerificationKey;

        var signature = Algorithm.Sign("original"u8, signingKey);
        var result = Algorithm.Verify("tampered"u8, verificationKey, signature);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.HmacSharedSigningKey;
        var verificationKey = RfcTestKeys.HmacSharedVerificationKey;
        var data = "test data"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var tampered = signature.ToArray();
        tampered[0] ^= 0xFF;

        Algorithm.Verify(data, verificationKey, tampered).ShouldBeFalse();
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.HmacSharedSigningKey;
        var wrongKey = new HmacSharedVerificationKey("wrong-key", new byte[32]);
        var data = "test data"u8;

        var signature = Algorithm.Sign(data, signingKey);

        Algorithm.Verify(data, wrongKey, signature).ShouldBeFalse();
    }

    [Fact]
    public void Sign_WithWrongKeyType_ThrowsArgumentException()
    {
        var wrongKey = RfcTestKeys.RsaPssSigningKey;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"

        Should.Throw<ArgumentException>(() => Algorithm.Sign(data, wrongKey));
    }

    [Fact]
    public void Verify_WithWrongKeyType_ThrowsArgumentException()
    {
        var wrongKey = RfcTestKeys.RsaPssVerificationKey;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"
        var sig = new byte[32];

        Should.Throw<ArgumentException>(() => Algorithm.Verify(data, wrongKey, sig));
    }

    /// <summary>
    /// RFC 9421 Appendix B.2.5 — HMAC-SHA256 signing with test-shared-secret.
    /// The signature base from B.2.5 is known, and the expected signature is deterministic:
    /// <c>pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=</c>
    /// </summary>
    [Fact]
    public void RfcB25_SignatureBase_ProducesExpectedSignature()
    {
        // The signature base from RFC 9421 B.2.5:
        var signatureBase =
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
            "\"@authority\": example.com\n" +
            "\"content-type\": application/json\n" +
            "\"@signature-params\": (\"date\" \"@authority\" " +
            "\"content-type\");created=1618884473" +
            ";keyid=\"test-shared-secret\"";

        var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);
        var key = RfcTestKeys.HmacSharedSigningKey;

        var signature = Algorithm.Sign(signatureBaseBytes, key);
        var signatureBase64 = Convert.ToBase64String(signature);

        signatureBase64.ShouldBe("pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=");
    }

    /// <summary>
    /// RFC 9421 Appendix B.2.5 — Verify the RFC-provided signature.
    /// </summary>
    [Fact]
    public void RfcB25_VerifyRfcProvidedSignature()
    {
        var signatureBase =
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
            "\"@authority\": example.com\n" +
            "\"content-type\": application/json\n" +
            "\"@signature-params\": (\"date\" \"@authority\" " +
            "\"content-type\");created=1618884473" +
            ";keyid=\"test-shared-secret\"";

        var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);
        var signatureBytes = Convert.FromBase64String("pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=");
        var key = RfcTestKeys.HmacSharedVerificationKey;

        Algorithm.Verify(signatureBaseBytes, key, signatureBytes).ShouldBeTrue();
    }
}
