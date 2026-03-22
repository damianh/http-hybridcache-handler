// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// End-to-end tests for RFC 9421 Appendix B.2 test vectors (B.2.1–B.2.6).
/// Each test constructs the full HTTP message, exercises the complete pipeline
/// (message context → signature base → sign/verify → header serialization),
/// and validates against the RFC's expected values.
/// Non-deterministic algorithms (RSA-PSS, ECDSA) use sign→verify round-trips
/// plus separate verification of RFC-provided signature values.
/// Deterministic algorithms (HMAC-SHA256) verify exact byte matches.
/// Ed25519 is unsupported on .NET — tests verify <see cref="PlatformNotSupportedException"/>.
/// </summary>
public sealed class RfcAppendixBTests
{
    private static readonly HttpMessageSigner Signer = new();
    private static readonly HttpMessageVerifier Verifier = new();

    /// <summary>
    /// Builds the RFC 9421 Appendix B.2 test request message:
    /// POST /foo?param=Value&amp;Pet=dog HTTP/1.1
    /// </summary>
    private static TestHttpMessageContext BuildRfcTestRequest()
    {
        var ctx = TestHttpMessageContext.CreateRequest(
            method: "POST",
            scheme: "https",
            authority: "example.com",
            path: "/foo",
            query: "?param=Value&Pet=dog");

        ctx.AddHeader("host", "example.com");
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:55 GMT");
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-digest",
            "sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:");
        ctx.AddHeader("content-length", "18");

        return ctx;
    }

    /// <summary>
    /// Builds the RFC 9421 Appendix B.2.4 test response message (HTTP/1.1 200 OK)
    /// with the associated request for request-response binding.
    /// </summary>
    private static TestHttpMessageContext BuildRfcTestResponse()
    {
        var request = BuildRfcTestRequest();
        var ctx = TestHttpMessageContext.CreateResponse(200, request);
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-digest",
            "sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:");
        ctx.AddHeader("content-length", "23");
        return ctx;
    }

    // =======================================================================
    // B.2.1 — Minimal Signature Using rsa-pss-sha512
    // =======================================================================

    /// <summary>
    /// RFC B.2.1 — Sign with empty covered components, then verify round-trip.
    /// </summary>
    [Fact]
    public void B21_MinimalRsaPss_SignAndVerifyRoundTrip()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters([])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
            Nonce = "b3k2pp5k7z-50gnwp.yemd",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig-b21", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);

        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("sig-b21", ctx, RfcTestKeys.RsaPssVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeTrue(verifyResult.ErrorMessage);
    }

    /// <summary>
    /// RFC B.2.1 — Verify the Signature-Input header matches the expected RFC serialization.
    /// </summary>
    [Fact]
    public void B21_MinimalRsaPss_SignatureInputMatchesExpected()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters([])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
            Nonce = "b3k2pp5k7z-50gnwp.yemd",
        };

        var ctx = BuildRfcTestRequest();
        var result = Signer.Sign("sig-b21", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);

        result.SignatureInputHeaderValue.ShouldBe(
            "sig-b21=();created=1618884473;keyid=\"test-key-rsa-pss\";nonce=\"b3k2pp5k7z-50gnwp.yemd\"");
    }

    /// <summary>
    /// RFC B.2.1 — Verify the RFC-provided signature by injecting the exact headers.
    /// </summary>
    [Fact]
    public void B21_MinimalRsaPss_VerifyRfcProvidedSignature()
    {
        var ctx = BuildRfcTestRequest();
        var algorithm = new RsaPssSha512SignatureAlgorithm();

        ctx.AddHeader("signature-input",
            "sig-b21=();created=1618884473;keyid=\"test-key-rsa-pss\";nonce=\"b3k2pp5k7z-50gnwp.yemd\"");
        ctx.AddHeader("signature",
            "sig-b21=:d2pmTvmbncD3xQm8E9ZV2828BjQWGgiwAaw5bAkgibUopem" +
            "LJcWDy/lkbbHAve4cRAtx31Iq786U7it++wgGxbtRxf8Udx7zFZsckzXaJMkA7ChG" +
            "52eSkFxykJeNqsrWH5S+oxNFlD4dzVuwe8DhTSja8xxbR/Z2cOGdCbzR72rgFWhzx" +
            "2VjBqJzsPLMIQKhO4DGezXehhWwE56YCE+O6c0mKZsfxVrogUvA4HELjVKWmAvtl6" +
            "UnCh8jYzuVG5WSb/QEVPnP5TmcAnLH1g+s++v6d4s8m0gCw1fV5/SITLq9mhho8K3" +
            "+7EPYTU8IU1bLhdxO5Nyt8C8ssinQ98Xw9Q==:");

        var result = Verifier.Verify("sig-b21", ctx, RfcTestKeys.RsaPssVerificationKey, algorithm);
        result.IsValid.ShouldBeTrue(result.ErrorMessage);
    }

    // =======================================================================
    // B.2.2 — Selective Covered Components Using rsa-pss-sha512
    // =======================================================================

    /// <summary>
    /// RFC B.2.2 — Sign with selective components, then verify round-trip.
    /// </summary>
    [Fact]
    public void B22_SelectiveRsaPss_SignAndVerifyRoundTrip()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.QueryParam("Pet"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
            Tag = "header-example",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig-b22", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);

        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("sig-b22", ctx, RfcTestKeys.RsaPssVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeTrue(verifyResult.ErrorMessage);
    }

    /// <summary>
    /// RFC B.2.2 — Verify the Signature-Input header matches the expected RFC serialization.
    /// </summary>
    [Fact]
    public void B22_SelectiveRsaPss_SignatureInputMatchesExpected()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.QueryParam("Pet"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
            Tag = "header-example",
        };

        var ctx = BuildRfcTestRequest();
        var result = Signer.Sign("sig-b22", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);

        result.SignatureInputHeaderValue.ShouldBe(
            "sig-b22=(\"@authority\" \"content-digest\" \"@query-param\";name=\"Pet\")" +
            ";created=1618884473;keyid=\"test-key-rsa-pss\";tag=\"header-example\"");
    }

    /// <summary>
    /// RFC B.2.2 — Verify the RFC-provided signature by injecting the exact headers.
    /// </summary>
    [Fact]
    public void B22_SelectiveRsaPss_VerifyRfcProvidedSignature()
    {
        var ctx = BuildRfcTestRequest();
        var algorithm = new RsaPssSha512SignatureAlgorithm();

        ctx.AddHeader("signature-input",
            "sig-b22=(\"@authority\" \"content-digest\" \"@query-param\";name=\"Pet\")" +
            ";created=1618884473;keyid=\"test-key-rsa-pss\";tag=\"header-example\"");
        ctx.AddHeader("signature",
            "sig-b22=:LjbtqUbfmvjj5C5kr1Ugj4PmLYvx9wVjZvD9GsTT4F7GrcQ" +
            "EdJzgI9qHxICagShLRiLMlAJjtq6N4CDfKtjvuJyE5qH7KT8UCMkSowOB4+ECxCmT" +
            "8rtAmj/0PIXxi0A0nxKyB09RNrCQibbUjsLS/2YyFYXEu4TRJQzRw1rLEuEfY17SA" +
            "RYhpTlaqwZVtR8NV7+4UKkjqpcAoFqWFQh62s7Cl+H2fjBSpqfZUJcsIk4N6wiKYd" +
            "4je2U/lankenQ99PZfB4jY3I5rSV2DSBVkSFsURIjYErOs0tFTQosMTAoxk//0RoK" +
            "UqiYY8Bh0aaUEb0rQl3/XaVe4bXTugEjHSw==:");

        var result = Verifier.Verify("sig-b22", ctx, RfcTestKeys.RsaPssVerificationKey, algorithm);
        result.IsValid.ShouldBeTrue(result.ErrorMessage);
    }

    // =======================================================================
    // B.2.3 — Full Coverage Using rsa-pss-sha512
    // =======================================================================

    /// <summary>
    /// RFC B.2.3 — Sign with full coverage, then verify round-trip.
    /// </summary>
    [Fact]
    public void B23_FullCoverageRsaPss_SignAndVerifyRoundTrip()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Method,
            ComponentIdentifier.Path,
            ComponentIdentifier.Query,
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig-b23", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);

        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("sig-b23", ctx, RfcTestKeys.RsaPssVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeTrue(verifyResult.ErrorMessage);
    }

    /// <summary>
    /// RFC B.2.3 — Verify the Signature-Input header matches the expected RFC serialization.
    /// </summary>
    [Fact]
    public void B23_FullCoverageRsaPss_SignatureInputMatchesExpected()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Method,
            ComponentIdentifier.Path,
            ComponentIdentifier.Query,
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
        };

        var ctx = BuildRfcTestRequest();
        var result = Signer.Sign("sig-b23", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);

        result.SignatureInputHeaderValue.ShouldBe(
            "sig-b23=(\"date\" \"@method\" \"@path\" \"@query\" \"@authority\" " +
            "\"content-type\" \"content-digest\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-rsa-pss\"");
    }

    /// <summary>
    /// RFC B.2.3 — Verify the RFC-provided signature by injecting the exact headers.
    /// </summary>
    [Fact]
    public void B23_FullCoverageRsaPss_VerifyRfcProvidedSignature()
    {
        var ctx = BuildRfcTestRequest();
        var algorithm = new RsaPssSha512SignatureAlgorithm();

        ctx.AddHeader("signature-input",
            "sig-b23=(\"date\" \"@method\" \"@path\" \"@query\" \"@authority\" " +
            "\"content-type\" \"content-digest\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-rsa-pss\"");
        ctx.AddHeader("signature",
            "sig-b23=:bbN8oArOxYoyylQQUU6QYwrTuaxLwjAC9fbY2F6SVWvh0yB" +
            "iMIRGOnMYwZ/5MR6fb0Kh1rIRASVxFkeGt683+qRpRRU5p2voTp768ZrCUb38K0fU" +
            "xN0O0iC59DzYx8DFll5GmydPxSmme9v6ULbMFkl+V5B1TP/yPViV7KsLNmvKiLJH1" +
            "pFkh/aYA2HXXZzNBXmIkoQoLd7YfW91kE9o/CCoC1xMy7JA1ipwvKvfrs65ldmlu9" +
            "bpG6A9BmzhuzF8Eim5f8ui9eH8LZH896+QIF61ka39VBrohr9iyMUJpvRX2Zbhl5Z" +
            "JzSRxpJyoEZAFL2FUo5fTIztsDZKEgM4cUA==:");

        var result = Verifier.Verify("sig-b23", ctx, RfcTestKeys.RsaPssVerificationKey, algorithm);
        result.IsValid.ShouldBeTrue(result.ErrorMessage);
    }

    // =======================================================================
    // B.2.4 — Signing a Response Using ecdsa-p256-sha256
    // =======================================================================

    /// <summary>
    /// RFC B.2.4 — Sign response with ECDSA P-256, then verify round-trip.
    /// </summary>
    [Fact]
    public void B24_EcdsaP256_ResponseSignAndVerifyRoundTrip()
    {
        var algorithm = new EcdsaP256Sha256SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-ecc-p256",
        };

        var ctx = BuildRfcTestResponse();
        var signResult = Signer.Sign("sig-b24", ctx, parameters, RfcTestKeys.EcdsaP256SigningKey, algorithm);

        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("sig-b24", ctx, RfcTestKeys.EcdsaP256VerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeTrue(verifyResult.ErrorMessage);
    }

    /// <summary>
    /// RFC B.2.4 — Verify the Signature-Input header matches the expected RFC serialization.
    /// </summary>
    [Fact]
    public void B24_EcdsaP256_SignatureInputMatchesExpected()
    {
        var algorithm = new EcdsaP256Sha256SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-ecc-p256",
        };

        var ctx = BuildRfcTestResponse();
        var result = Signer.Sign("sig-b24", ctx, parameters, RfcTestKeys.EcdsaP256SigningKey, algorithm);

        result.SignatureInputHeaderValue.ShouldBe(
            "sig-b24=(\"@status\" \"content-type\" \"content-digest\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-ecc-p256\"");
    }

    /// <summary>
    /// RFC B.2.4 — Verify the RFC-provided signature by injecting the exact headers.
    /// </summary>
    [Fact]
    public void B24_EcdsaP256_VerifyRfcProvidedSignature()
    {
        var ctx = BuildRfcTestResponse();
        var algorithm = new EcdsaP256Sha256SignatureAlgorithm();

        ctx.AddHeader("signature-input",
            "sig-b24=(\"@status\" \"content-type\" \"content-digest\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-ecc-p256\"");
        ctx.AddHeader("signature",
            "sig-b24=:wNmSUAhwb5LxtOtOpNa6W5xj067m5hFrj0XQ4fvpaCLx0NKocgPquLgyahnzDnDAUy" +
            "5eCdlYUEkLIj+32oiasw==:");

        var result = Verifier.Verify("sig-b24", ctx, RfcTestKeys.EcdsaP256VerificationKey, algorithm);
        result.IsValid.ShouldBeTrue(result.ErrorMessage);
    }

    // =======================================================================
    // B.2.5 — Signing Using HMAC-SHA256 (Deterministic)
    // =======================================================================

    /// <summary>
    /// RFC B.2.5 — HMAC-SHA256 produces the exact RFC signature value (deterministic).
    /// </summary>
    [Fact]
    public void B25_HmacSha256_ProducesExactRfcSignature()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig-b25", ctx, parameters, RfcTestKeys.HmacSharedSigningKey, algorithm);

        // HMAC-SHA256 is deterministic — exact match with RFC B.2.5
        var expectedSignatureBase64 = "pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=";
        Convert.ToBase64String(signResult.SignatureBytes).ShouldBe(expectedSignatureBase64);

        signResult.SignatureInputHeaderValue.ShouldBe(
            "sig-b25=(\"date\" \"@authority\" \"content-type\");created=1618884473;keyid=\"test-shared-secret\"");

        signResult.SignatureHeaderValue.ShouldBe(
            $"sig-b25=:{expectedSignatureBase64}:");
    }

    /// <summary>
    /// RFC B.2.5 — HMAC-SHA256 sign → add headers → verify round-trip.
    /// </summary>
    [Fact]
    public void B25_HmacSha256_SignAndVerifyRoundTrip()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig-b25", ctx, parameters, RfcTestKeys.HmacSharedSigningKey, algorithm);

        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("sig-b25", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeTrue(verifyResult.ErrorMessage);
        verifyResult.Parameters.ShouldNotBeNull();
        verifyResult.Parameters.KeyId.ShouldBe("test-shared-secret");
    }

    /// <summary>
    /// RFC B.2.5 — Verify the RFC-provided signature by injecting the exact headers.
    /// </summary>
    [Fact]
    public void B25_HmacSha256_VerifyRfcProvidedSignature()
    {
        var ctx = BuildRfcTestRequest();
        var algorithm = new HmacSha256SignatureAlgorithm();

        ctx.AddHeader("signature-input",
            "sig-b25=(\"date\" \"@authority\" \"content-type\")" +
            ";created=1618884473;keyid=\"test-shared-secret\"");
        ctx.AddHeader("signature",
            "sig-b25=:pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=:");

        var result = Verifier.Verify("sig-b25", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        result.IsValid.ShouldBeTrue(result.ErrorMessage);
    }

    // =======================================================================
    // B.2.6 — Signing Using Ed25519 (PlatformNotSupportedException)
    // =======================================================================

    /// <summary>
    /// RFC B.2.6 — Ed25519 signing throws <see cref="PlatformNotSupportedException"/> on .NET.
    /// </summary>
    [Fact]
    public void B26_Ed25519_SignThrowsPlatformNotSupportedException()
    {
        var algorithm = new Ed25519SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Method,
            ComponentIdentifier.Path,
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-ed25519",
        };

        var ctx = BuildRfcTestRequest();

        Should.Throw<PlatformNotSupportedException>(() =>
            Signer.Sign("sig-b26", ctx, parameters, RfcTestKeys.Ed25519Signing, algorithm));
    }

    /// <summary>
    /// RFC B.2.6 — Ed25519 verification throws <see cref="PlatformNotSupportedException"/> on .NET.
    /// </summary>
    [Fact]
    public void B26_Ed25519_VerifyThrowsPlatformNotSupportedException()
    {
        var ctx = BuildRfcTestRequest();
        var algorithm = new Ed25519SignatureAlgorithm();

        ctx.AddHeader("signature-input",
            "sig-b26=(\"date\" \"@method\" \"@path\" \"@authority\" \"content-type\" \"content-length\")" +
            ";created=1618884473;keyid=\"test-key-ed25519\"");
        ctx.AddHeader("signature",
            "sig-b26=:wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4" +
            "A0w6vuQv5lIp5WPpBKRCw==:");

        Should.Throw<PlatformNotSupportedException>(() =>
            Verifier.Verify("sig-b26", ctx, RfcTestKeys.Ed25519Verification, algorithm));
    }

    // =======================================================================
    // Cross-cutting: Tamper detection
    // =======================================================================

    /// <summary>
    /// Verifies that modifying a covered header after signing causes verification to fail.
    /// </summary>
    [Fact]
    public void SignThenTamperHeader_VerificationFails()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig1", ctx, parameters, RfcTestKeys.HmacSharedSigningKey, algorithm);
        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        // Tamper with a covered header
        ctx.SetHeader("content-type", "text/html");

        var verifyResult = Verifier.Verify("sig1", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that using the wrong verification key type causes an exception during verification.
    /// The algorithm rejects incompatible key types with <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void WrongVerificationKeyType_ThrowsArgumentException()
    {
        var algorithm = new RsaPssSha512SignatureAlgorithm();
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Method,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
        };

        var ctx = BuildRfcTestRequest();
        var signResult = Signer.Sign("sig1", ctx, parameters, RfcTestKeys.RsaPssSigningKey, algorithm);
        ctx.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        ctx.AddHeader("signature", signResult.SignatureHeaderValue);

        // Verify with wrong key type (ECDSA P-256 instead of RSA-PSS) — algorithm rejects it
        Should.Throw<ArgumentException>(() =>
            Verifier.Verify("sig1", ctx, RfcTestKeys.EcdsaP256VerificationKey, algorithm));
    }
}
