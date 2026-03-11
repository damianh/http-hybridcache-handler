// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="EcdsaP256Sha256SignatureAlgorithm"/>.
/// ECDSA is non-deterministic — sign/verify round-trips and RFC test vector verification only.
/// Includes RFC 9421 Appendix B.2.4 verification test.
/// </summary>
public sealed class EcdsaP256Sha256Tests
{
    private static readonly EcdsaP256Sha256SignatureAlgorithm Algorithm = new();

    [Fact]
    public void AlgorithmName_IsCorrect() =>
        Algorithm.AlgorithmName.ShouldBe("ecdsa-p256-sha256");

    [Fact]
    public void SignAndVerify_RoundTrip()
    {
        var signingKey = RfcTestKeys.EcdsaP256SigningKey;
        var verificationKey = RfcTestKeys.EcdsaP256VerificationKey;
        var data = "test data for ecdsa-p256"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var result = Algorithm.Verify(data, verificationKey, signature);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Sign_ProducesCorrectLength()
    {
        var key = RfcTestKeys.EcdsaP256SigningKey;
        var data = "test"u8;

        var signature = Algorithm.Sign(data, key);

        // P-256 IEEE P1363 format: r (32 bytes) || s (32 bytes) = 64 bytes
        signature.Length.ShouldBe(64);
    }

    [Fact]
    public void Sign_IsNonDeterministic()
    {
        var key = RfcTestKeys.EcdsaP256SigningKey;
        var data = "same data"u8;

        var sig1 = Algorithm.Sign(data, key);
        var sig2 = Algorithm.Sign(data, key);

        // ECDSA uses random k, so signatures should differ
        sig1.ShouldNotBe(sig2);
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.EcdsaP256SigningKey;
        var verificationKey = RfcTestKeys.EcdsaP256VerificationKey;

        var signature = Algorithm.Sign("original"u8, signingKey);

        Algorithm.Verify("tampered"u8, verificationKey, signature).ShouldBeFalse();
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.EcdsaP256SigningKey;
        var verificationKey = RfcTestKeys.EcdsaP256VerificationKey;
        var data = "test"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var tampered = signature.ToArray();
        tampered[0] ^= 0xFF;

        Algorithm.Verify(data, verificationKey, tampered).ShouldBeFalse();
    }

    [Fact]
    public void Sign_WithWrongKeyType_ThrowsArgumentException()
    {
        var wrongKey = RfcTestKeys.HmacSharedSigningKey;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"
        Should.Throw<ArgumentException>(() => Algorithm.Sign(data, wrongKey));
    }

    [Fact]
    public void Verify_WithWrongKeyType_ThrowsArgumentException()
    {
        var wrongKey = RfcTestKeys.HmacSharedVerificationKey;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"
        Should.Throw<ArgumentException>(() => Algorithm.Verify(data, wrongKey, new byte[64]));
    }

    /// <summary>
    /// RFC 9421 Appendix B.2.4 — ECDSA P-256 SHA-256 response signing.
    /// The provided signature is non-deterministic, so we verify the RFC-provided value.
    /// </summary>
    [Fact]
    public void RfcB24_VerifyResponseSignature()
    {
        var signatureBase =
            "\"@status\": 200\n" +
            "\"content-type\": application/json\n" +
            "\"content-digest\": sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41Q" +
            "JgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:\n" +
            "\"content-length\": 23\n" +
            "\"@signature-params\": (\"@status\" \"content-type\" " +
            "\"content-digest\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-ecc-p256\"";

        var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);
        var signatureBytes = Convert.FromBase64String(
            "wNmSUAhwb5LxtOtOpNa6W5xj067m5hFrj0XQ4fvpaCLx0NKocgPquLgyahnzDnDAUy5eCdlYUEkLIj+32oiasw==");

        var verificationKey = RfcTestKeys.EcdsaP256VerificationKey;

        Algorithm.Verify(signatureBaseBytes, verificationKey, signatureBytes).ShouldBeTrue();
    }
}
