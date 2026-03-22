// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="RsaPssSha512SignatureAlgorithm"/>.
/// RSA-PSS is non-deterministic — sign/verify round-trips and RFC test vector verification only.
/// Includes RFC 9421 Appendix B.2.1, B.2.2, B.2.3 verification tests.
/// </summary>
public sealed class RsaPssSha512Tests
{
    private static readonly RsaPssSha512SignatureAlgorithm Algorithm = new();

    [Fact]
    public void AlgorithmName_IsCorrect() =>
        Algorithm.AlgorithmName.ShouldBe("rsa-pss-sha512");

    [Fact]
    public void SignAndVerify_RoundTrip()
    {
        var signingKey = RfcTestKeys.RsaPssSigningKey;
        var verificationKey = RfcTestKeys.RsaPssVerificationKey;
        var data = "test data for rsa-pss"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var result = Algorithm.Verify(data, verificationKey, signature);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Sign_ProducesCorrectLength()
    {
        var key = RfcTestKeys.RsaPssSigningKey;
        var data = "test"u8;

        var signature = Algorithm.Sign(data, key);

        // RSA 2048-bit key → 256-byte signature
        signature.Length.ShouldBe(256);
    }

    [Fact]
    public void Sign_IsNonDeterministic()
    {
        var key = RfcTestKeys.RsaPssSigningKey;
        var data = "same data"u8;

        var sig1 = Algorithm.Sign(data, key);
        var sig2 = Algorithm.Sign(data, key);

        // RSA-PSS uses random salt, so signatures should differ
        // (extremely unlikely to be equal)
        sig1.ShouldNotBe(sig2);
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.RsaPssSigningKey;
        var verificationKey = RfcTestKeys.RsaPssVerificationKey;

        var signature = Algorithm.Sign("original"u8, signingKey);

        Algorithm.Verify("tampered"u8, verificationKey, signature).ShouldBeFalse();
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var signingKey = RfcTestKeys.RsaPssSigningKey;
        var verificationKey = RfcTestKeys.RsaPssVerificationKey;
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
        Should.Throw<ArgumentException>(() => Algorithm.Verify(data, wrongKey, new byte[256]));
    }

    /// <summary>
    /// RFC 9421 Appendix B.2.1 — Minimal signature using rsa-pss-sha512.
    /// Signature base is just the @signature-params line (empty covered components).
    /// The provided signature is non-deterministic, so we verify only.
    /// </summary>
    [Fact]
    public void RfcB21_VerifyMinimalSignature()
    {
        // RFC B.2.1 signature base (RFC 8792 unwrapped):
        var signatureBase =
            "\"@signature-params\": ();created=1618884473" +
            ";keyid=\"test-key-rsa-pss\";nonce=\"b3k2pp5k7z-50gnwp.yemd\"";

        var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);

        // RFC B.2.1 signature value (RFC 8792 unwrapped, single-line base64):
        var signatureBytes = Convert.FromBase64String(
            "d2pmTvmbncD3xQm8E9ZV2828BjQWGgiwAaw5bAkgibUopem" +
            "LJcWDy/lkbbHAve4cRAtx31Iq786U7it++wgGxbtRxf8Udx7zFZsckzXaJMkA7ChG" +
            "52eSkFxykJeNqsrWH5S+oxNFlD4dzVuwe8DhTSja8xxbR/Z2cOGdCbzR72rgFWhzx" +
            "2VjBqJzsPLMIQKhO4DGezXehhWwE56YCE+O6c0mKZsfxVrogUvA4HELjVKWmAvtl6" +
            "UnCh8jYzuVG5WSb/QEVPnP5TmcAnLH1g+s++v6d4s8m0gCw1fV5/SITLq9mhho8K3" +
            "+7EPYTU8IU1bLhdxO5Nyt8C8ssinQ98Xw9Q==");

        var verificationKey = RfcTestKeys.RsaPssVerificationKey;

        Algorithm.Verify(signatureBaseBytes, verificationKey, signatureBytes).ShouldBeTrue();
    }

    /// <summary>
    /// RFC 9421 Appendix B.2.2 — Selective covered components using rsa-pss-sha512.
    /// </summary>
    [Fact]
    public void RfcB22_VerifySelectiveSignature()
    {
        // RFC B.2.2 signature base (RFC 8792 unwrapped):
        var signatureBase =
            "\"@authority\": example.com\n" +
            "\"content-digest\": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX" +
            "+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:\n" +
            "\"@query-param\";name=\"Pet\": dog\n" +
            "\"@signature-params\": (\"@authority\" \"content-digest\" " +
            "\"@query-param\";name=\"Pet\")" +
            ";created=1618884473;keyid=\"test-key-rsa-pss\"" +
            ";tag=\"header-example\"";

        var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);

        // RFC B.2.2 signature value (RFC 8792 unwrapped):
        var signatureBytes = Convert.FromBase64String(
            "LjbtqUbfmvjj5C5kr1Ugj4PmLYvx9wVjZvD9GsTT4F7GrcQ" +
            "EdJzgI9qHxICagShLRiLMlAJjtq6N4CDfKtjvuJyE5qH7KT8UCMkSowOB4+ECxCmT" +
            "8rtAmj/0PIXxi0A0nxKyB09RNrCQibbUjsLS/2YyFYXEu4TRJQzRw1rLEuEfY17SA" +
            "RYhpTlaqwZVtR8NV7+4UKkjqpcAoFqWFQh62s7Cl+H2fjBSpqfZUJcsIk4N6wiKYd" +
            "4je2U/lankenQ99PZfB4jY3I5rSV2DSBVkSFsURIjYErOs0tFTQosMTAoxk//0RoK" +
            "UqiYY8Bh0aaUEb0rQl3/XaVe4bXTugEjHSw==");

        var verificationKey = RfcTestKeys.RsaPssVerificationKey;

        Algorithm.Verify(signatureBaseBytes, verificationKey, signatureBytes).ShouldBeTrue();
    }

    /// <summary>
    /// RFC 9421 Appendix B.2.3 — Full coverage using rsa-pss-sha512.
    /// </summary>
    [Fact]
    public void RfcB23_VerifyFullCoverageSignature()
    {
        // RFC B.2.3 signature base (RFC 8792 unwrapped):
        var signatureBase =
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
            "\"@method\": POST\n" +
            "\"@path\": /foo\n" +
            "\"@query\": ?param=Value&Pet=dog\n" +
            "\"@authority\": example.com\n" +
            "\"content-type\": application/json\n" +
            "\"content-digest\": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX" +
            "+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:\n" +
            "\"content-length\": 18\n" +
            "\"@signature-params\": (\"date\" \"@method\" \"@path\" \"@query\" " +
            "\"@authority\" \"content-type\" \"content-digest\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-rsa-pss\"";

        var signatureBaseBytes = Encoding.ASCII.GetBytes(signatureBase);

        // RFC B.2.3 signature value (RFC 8792 unwrapped):
        var signatureBytes = Convert.FromBase64String(
            "bbN8oArOxYoyylQQUU6QYwrTuaxLwjAC9fbY2F6SVWvh0yB" +
            "iMIRGOnMYwZ/5MR6fb0Kh1rIRASVxFkeGt683+qRpRRU5p2voTp768ZrCUb38K0fU" +
            "xN0O0iC59DzYx8DFll5GmydPxSmme9v6ULbMFkl+V5B1TP/yPViV7KsLNmvKiLJH1" +
            "pFkh/aYA2HXXZzNBXmIkoQoLd7YfW91kE9o/CCoC1xMy7JA1ipwvKvfrs65ldmlu9" +
            "bpG6A9BmzhuzF8Eim5f8ui9eH8LZH896+QIF61ka39VBrohr9iyMUJpvRX2Zbhl5Z" +
            "JzSRxpJyoEZAFL2FUo5fTIztsDZKEgM4cUA==");

        var verificationKey = RfcTestKeys.RsaPssVerificationKey;

        Algorithm.Verify(signatureBaseBytes, verificationKey, signatureBytes).ShouldBeTrue();
    }
}
