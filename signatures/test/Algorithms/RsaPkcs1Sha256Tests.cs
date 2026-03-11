// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="RsaPkcs1Sha256SignatureAlgorithm"/>.
/// RSA PKCS#1 v1.5 is deterministic for the same key, but there are no RFC test vectors
/// for this algorithm, so we test sign/verify round-trips.
/// </summary>
public sealed class RsaPkcs1Sha256Tests
{
    private static readonly RsaPkcs1Sha256SignatureAlgorithm Algorithm = new();

    [Fact]
    public void AlgorithmName_IsCorrect() =>
        Algorithm.AlgorithmName.ShouldBe("rsa-v1_5-sha256");

    [Fact]
    public void SignAndVerify_RoundTrip()
    {
        var signingKey = RfcTestKeys.RsaSigningKey;
        var verificationKey = RfcTestKeys.RsaVerificationKey;
        var data = "test data for rsa-pkcs1"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var result = Algorithm.Verify(data, verificationKey, signature);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Sign_ProducesCorrectLength()
    {
        var key = RfcTestKeys.RsaSigningKey;
        var data = "test"u8;

        var signature = Algorithm.Sign(data, key);

        // RSA 2048-bit key → 256-byte signature
        signature.Length.ShouldBe(256);
    }

    [Fact]
    public void Sign_IsDeterministic()
    {
        var key = RfcTestKeys.RsaSigningKey;
        var data = "deterministic pkcs1 test"u8;

        var sig1 = Algorithm.Sign(data, key);
        var sig2 = Algorithm.Sign(data, key);

        // PKCS#1 v1.5 is deterministic
        sig1.ShouldBe(sig2);
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.RsaSigningKey;
        var verificationKey = RfcTestKeys.RsaVerificationKey;

        var signature = Algorithm.Sign("original"u8, signingKey);

        Algorithm.Verify("tampered"u8, verificationKey, signature).ShouldBeFalse();
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.RsaSigningKey;
        var verificationKey = RfcTestKeys.RsaVerificationKey;
        var data = "test"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var tampered = signature.ToArray();
        tampered[0] ^= 0xFF;

        Algorithm.Verify(data, verificationKey, tampered).ShouldBeFalse();
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.RsaSigningKey;
        // Using the RSA-PSS key as "wrong" key — different RSA key pair
        var wrongKey = RfcTestKeys.RsaPssVerificationKey;
        var data = "test data"u8;

        var signature = Algorithm.Sign(data, signingKey);

        Algorithm.Verify(data, wrongKey, signature).ShouldBeFalse();
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
        Should.Throw<ArgumentException>(() => Algorithm.Verify(data, wrongKey, new byte[256]));
    }
}
