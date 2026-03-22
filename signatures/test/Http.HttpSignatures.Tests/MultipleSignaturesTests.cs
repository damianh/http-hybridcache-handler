// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for multiple signatures on a single message per RFC 9421 §4.3.
/// Verifies that multiple labeled signatures can coexist and be verified independently.
/// </summary>
public sealed class MultipleSignaturesTests
{
    private static readonly HttpMessageSigner Signer = new();
    private static readonly HttpMessageVerifier Verifier = new();

    private static TestHttpMessageContext BuildTestRequest()
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
    /// Signs a message with two different labels using the same algorithm/key.
    /// Both signatures should be independently verifiable.
    /// </summary>
    [Fact]
    public void TwoSignatures_SameAlgorithm_BothVerifyIndependently()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();

        var params1 = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var params2 = new SignatureParameters(
        [
            ComponentIdentifier.Method,
            ComponentIdentifier.Field("content-type"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884474),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();

        // Sign with first label
        var result1 = Signer.Sign("sig1", ctx, params1, RfcTestKeys.HmacSharedSigningKey, algorithm);

        // Sign with second label
        var result2 = Signer.Sign("sig2", ctx, params2, RfcTestKeys.HmacSharedSigningKey, algorithm);

        // Add both signatures as combined header values (SF Dictionary format)
        var combinedSignatureInput = $"{result1.SignatureInputHeaderValue}, {result2.SignatureInputHeaderValue}";
        var combinedSignature = $"{result1.SignatureHeaderValue}, {result2.SignatureHeaderValue}";

        ctx.AddHeader("signature-input", combinedSignatureInput);
        ctx.AddHeader("signature", combinedSignature);

        // Verify each independently
        var verify1 = Verifier.Verify("sig1", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verify1.IsValid.ShouldBeTrue(verify1.ErrorMessage);
        verify1.Parameters!.KeyId.ShouldBe("test-shared-secret");

        var verify2 = Verifier.Verify("sig2", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verify2.IsValid.ShouldBeTrue(verify2.ErrorMessage);
        verify2.Parameters!.KeyId.ShouldBe("test-shared-secret");
    }

    /// <summary>
    /// Signs a message with two different algorithms (HMAC-SHA256 and RSA-PSS-SHA512).
    /// Both signatures should be independently verifiable with their respective keys.
    /// </summary>
    [Fact]
    public void TwoSignatures_DifferentAlgorithms_BothVerifyIndependently()
    {
        var hmacAlgorithm = new HmacSha256SignatureAlgorithm();
        var rsaAlgorithm = new RsaPssSha512SignatureAlgorithm();

        var hmacParams = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var rsaParams = new SignatureParameters(
        [
            ComponentIdentifier.Method,
            ComponentIdentifier.Path,
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
        };

        var ctx = BuildTestRequest();

        var hmacResult = Signer.Sign("sig-hmac", ctx, hmacParams, RfcTestKeys.HmacSharedSigningKey, hmacAlgorithm);
        var rsaResult = Signer.Sign("sig-rsa", ctx, rsaParams, RfcTestKeys.RsaPssSigningKey, rsaAlgorithm);

        var combinedInput = $"{hmacResult.SignatureInputHeaderValue}, {rsaResult.SignatureInputHeaderValue}";
        var combinedSig = $"{hmacResult.SignatureHeaderValue}, {rsaResult.SignatureHeaderValue}";

        ctx.AddHeader("signature-input", combinedInput);
        ctx.AddHeader("signature", combinedSig);

        var verifyHmac = Verifier.Verify("sig-hmac", ctx, RfcTestKeys.HmacSharedVerificationKey, hmacAlgorithm);
        verifyHmac.IsValid.ShouldBeTrue(verifyHmac.ErrorMessage);

        var verifyRsa = Verifier.Verify("sig-rsa", ctx, RfcTestKeys.RsaPssVerificationKey, rsaAlgorithm);
        verifyRsa.IsValid.ShouldBeTrue(verifyRsa.ErrorMessage);
    }

    /// <summary>
    /// Verifying a non-existent label when multiple signatures exist should fail gracefully.
    /// </summary>
    [Fact]
    public void VerifyNonExistentLabel_WithMultipleSignatures_ReturnsFailed()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();

        var parameters = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();
        var result = Signer.Sign("sig1", ctx, parameters, RfcTestKeys.HmacSharedSigningKey, algorithm);

        ctx.AddHeader("signature-input", result.SignatureInputHeaderValue);
        ctx.AddHeader("signature", result.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("nonexistent", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeFalse();
        verifyResult.ErrorMessage!.ShouldContain("nonexistent");
    }

    /// <summary>
    /// Tampering with one signature should not affect verification of the other.
    /// </summary>
    [Fact]
    public void TamperOneSignature_OtherStillVerifies()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();

        var params1 = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var params2 = new SignatureParameters([ComponentIdentifier.Authority])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884474),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();

        var result1 = Signer.Sign("sig1", ctx, params1, RfcTestKeys.HmacSharedSigningKey, algorithm);
        var result2 = Signer.Sign("sig2", ctx, params2, RfcTestKeys.HmacSharedSigningKey, algorithm);

        // Tamper sig1's signature value (corrupt the base64 payload)
        var tamperedSig1 = result1.SignatureHeaderValue.Replace("sig1=:", "sig1=:AAAA");

        var combinedInput = $"{result1.SignatureInputHeaderValue}, {result2.SignatureInputHeaderValue}";
        var combinedSig = $"{tamperedSig1}, {result2.SignatureHeaderValue}";

        ctx.AddHeader("signature-input", combinedInput);
        ctx.AddHeader("signature", combinedSig);

        // sig1 should fail (tampered)
        var verify1 = Verifier.Verify("sig1", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verify1.IsValid.ShouldBeFalse();

        // sig2 should still pass
        var verify2 = Verifier.Verify("sig2", ctx, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verify2.IsValid.ShouldBeTrue(verify2.ErrorMessage);
    }
}
