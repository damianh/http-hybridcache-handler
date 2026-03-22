// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Integration tests for <see cref="HttpMessageVerifier"/>.
/// Uses HMAC-SHA256 for sign → verify round-trip testing.
/// </summary>
public sealed class HttpMessageVerifierTests
{
    private static readonly byte[] TestKeyBytes =
        Convert.FromBase64String("uzvJfB4u3N0Jy4T7NZ75MDVcr8zSTInedJtkgcu46YW4XByzNJjxBdtjUkdJPBtbmHhIDi6pcl8jsasjlTMtDQ==");

    private static readonly HmacSharedKey TestSigningKey = new("test-shared-secret", TestKeyBytes);
    private static readonly HmacSharedVerificationKey TestVerificationKey = new("test-shared-secret", TestKeyBytes);
    private static readonly HmacSha256SignatureAlgorithm Algorithm = new();
    private static readonly HttpMessageSigner Signer = new();
    private static readonly HttpMessageVerifier Verifier = new();

    private static TestHttpMessageContext BuildSignedRequest()
    {
        var ctx = TestHttpMessageContext.CreateRequest("POST", "https", "example.com", "/foo", "?param=Value&Pet=dog");
        ctx.AddHeader("host", "example.com");
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:55 GMT");
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-length", "18");

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

        var result = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);
        ctx.AddHeader("signature-input", result.SignatureInputHeaderValue);
        ctx.AddHeader("signature", result.SignatureHeaderValue);

        return ctx;
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsSuccess()
    {
        var ctx = BuildSignedRequest();
        var result = Verifier.Verify("sig1", ctx, TestVerificationKey, Algorithm);

        result.IsValid.ShouldBeTrue();
        result.Parameters.ShouldNotBeNull();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFailed()
    {
        var ctx = BuildSignedRequest();
        var wrongKey = new HmacSharedVerificationKey("wrong-key", new byte[64]);
        var result = Verifier.Verify("sig1", ctx, wrongKey, Algorithm);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public void Verify_TamperedHeader_ReturnsFailed()
    {
        var ctx = BuildSignedRequest();
        // Tamper with a covered header
        ctx.SetHeader("content-type", "text/plain");

        var result = Verifier.Verify("sig1", ctx, TestVerificationKey, Algorithm);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Verify_MissingSignatureInputHeader_ReturnsFailed()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        // No signature-input or signature headers
        var result = Verifier.Verify("sig1", ctx, TestVerificationKey, Algorithm);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Signature-Input");
    }

    [Fact]
    public void Verify_MissingSignatureHeader_ReturnsFailed()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("signature-input", "sig1=();created=1618884473;keyid=\"test-shared-secret\"");
        // No signature header
        var result = Verifier.Verify("sig1", ctx, TestVerificationKey, Algorithm);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Signature header");
    }

    [Fact]
    public void Verify_WrongLabel_ReturnsFailed()
    {
        var ctx = BuildSignedRequest();
        var result = Verifier.Verify("sig99", ctx, TestVerificationKey, Algorithm);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("sig99");
    }

    [Fact]
    public async Task VerifyAsync_WithKeyResolverAndRegistry_Succeeds()
    {
        var ctx = BuildSignedRequestWithAlgorithm();

        var keyResolver = new TestKeyResolver(TestVerificationKey);
        var registry = new SignatureAlgorithmRegistry();
        registry.Register(Algorithm);

        var result = await Verifier.VerifyAsync("sig1", ctx, keyResolver, registry);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyAsync_UnknownAlgorithm_ReturnsFailed()
    {
        var ctx = BuildSignedRequestWithAlgorithm();

        var keyResolver = new TestKeyResolver(TestVerificationKey);
        var registry = new SignatureAlgorithmRegistry(); // no algorithms registered

        var result = await Verifier.VerifyAsync("sig1", ctx, keyResolver, registry);
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not registered");
    }

    [Fact]
    public async Task VerifyAsync_UnknownKey_ReturnsFailed()
    {
        var ctx = BuildSignedRequestWithAlgorithm();

        var keyResolver = new TestKeyResolver(null); // key not found
        var registry = new SignatureAlgorithmRegistry();
        registry.Register(Algorithm);

        var result = await Verifier.VerifyAsync("sig1", ctx, keyResolver, registry);
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("could not be resolved");
    }

    private static TestHttpMessageContext BuildSignedRequestWithAlgorithm()
    {
        var ctx = TestHttpMessageContext.CreateRequest("POST", "https", "example.com", "/foo");
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:55 GMT");
        ctx.AddHeader("content-type", "application/json");

        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
            Algorithm = "hmac-sha256",
        };

        var result = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);
        ctx.AddHeader("signature-input", result.SignatureInputHeaderValue);
        ctx.AddHeader("signature", result.SignatureHeaderValue);

        return ctx;
    }

    private sealed class TestKeyResolver(VerificationKey? key) : IKeyResolver
    {
        public Task<VerificationKey?> ResolveKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(key);
    }
}
