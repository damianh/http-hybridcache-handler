// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="Ed25519SignatureAlgorithm"/>.
/// Ed25519 is not currently supported by .NET — all operations throw <see cref="PlatformNotSupportedException"/>.
/// When .NET adds Ed25519 support, these tests should be updated to include
/// RFC 9421 Appendix B.2.6 test vector verification.
/// </summary>
public sealed class Ed25519Tests
{
    private static readonly Ed25519SignatureAlgorithm Algorithm = new();

    [Fact]
    public void AlgorithmName_IsCorrect() =>
        Algorithm.AlgorithmName.ShouldBe("ed25519");

    [Fact]
    public void Sign_ThrowsPlatformNotSupportedException()
    {
        var key = RfcTestKeys.Ed25519Signing;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"

        Should.Throw<PlatformNotSupportedException>(
            () => Algorithm.Sign(data, key));
    }

    [Fact]
    public void Verify_ThrowsPlatformNotSupportedException()
    {
        var key = RfcTestKeys.Ed25519Verification;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"

        Should.Throw<PlatformNotSupportedException>(
            () => Algorithm.Verify(data, key, new byte[64]));
    }

    // The following test is commented out until .NET adds Ed25519 support.
    // When available, uncomment and verify against the RFC test vector.
    //
    // /// <summary>
    // /// RFC 9421 Appendix B.2.6 — Ed25519 signing with test-key-ed25519.
    // /// Ed25519 is deterministic, so the signature should be an exact match.
    // /// Expected signature: wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4A0w6vuQv5lIp5WPpBKRCw==
    // /// </summary>
    // [Fact]
    // public void RfcB26_SignatureBase_ProducesExpectedSignature()
    // {
    //     var signatureBase =
    //         "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
    //         "\"@method\": POST\n" +
    //         "\"@path\": /foo\n" +
    //         "\"@authority\": example.com\n" +
    //         "\"content-type\": application/json\n" +
    //         "\"@signature-params\": (\"date\" \"@method\" \"@path\" " +
    //         "\"@authority\" \"content-type\")" +
    //         ";created=1618884473;keyid=\"test-key-ed25519\"" +
    //         ";alg=\"ed25519\"";
    //
    //     var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);
    //     var key = RfcTestKeys.Ed25519Signing;
    //
    //     var signature = Algorithm.Sign(signatureBaseBytes, key);
    //     var signatureBase64 = Convert.ToBase64String(signature);
    //
    //     signatureBase64.ShouldBe("wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4A0w6vuQv5lIp5WPpBKRCw==");
    // }
}
