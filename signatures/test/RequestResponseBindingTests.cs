// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for request-response binding via the <c>req</c> parameter (RFC 9421 §2.4).
/// When signing a response, covered components with <c>;req</c> are resolved from the associated request.
/// </summary>
public sealed class RequestResponseBindingTests
{
    private static readonly HttpMessageSigner Signer = new();
    private static readonly HttpMessageVerifier Verifier = new();

    private static TestHttpMessageContext BuildRequest()
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

    private static TestHttpMessageContext BuildResponse(TestHttpMessageContext request)
    {
        var ctx = TestHttpMessageContext.CreateResponse(200, request);
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:56 GMT");
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-digest",
            "sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:");
        ctx.AddHeader("content-length", "23");
        return ctx;
    }

    /// <summary>
    /// RFC 9421 §2.4 example: Signature base for a response that binds to request components.
    /// The signature base should include <c>"@method";req</c>, <c>"@authority";req</c>, and <c>"@path";req</c>
    /// resolved from the associated request, plus <c>"@status"</c> and response fields.
    /// </summary>
    [Fact]
    public void SignatureBase_IncludesRequestComponentsViaReqParam()
    {
        var request = BuildRequest();
        var response = BuildResponse(request);

        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
            new ComponentIdentifier("@method") { Req = true },
            new ComponentIdentifier("@authority") { Req = true },
            new ComponentIdentifier("@path") { Req = true },
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
        };

        var result = SignatureBaseBuilder.BuildString(parameters, response);

        // Verify the signature base contains request-derived components
        result.ShouldContain("\"@method\";req: POST");
        result.ShouldContain("\"@authority\";req: example.com");
        result.ShouldContain("\"@path\";req: /foo");

        // And response components
        result.ShouldContain("\"@status\": 200");
        result.ShouldContain("\"content-type\": application/json");

        // The @signature-params line should include ;req on the request-bound components
        result.ShouldContain("\"@method\";req");
        result.ShouldContain("\"@authority\";req");
        result.ShouldContain("\"@path\";req");
    }

    /// <summary>
    /// Sign → verify round-trip for a response with request-bound components.
    /// </summary>
    [Fact]
    public void ResponseWithReqBinding_SignAndVerifyRoundTrip()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();
        var request = BuildRequest();
        var response = BuildResponse(request);

        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            ComponentIdentifier.Field("content-type"),
            new ComponentIdentifier("@method") { Req = true },
            new ComponentIdentifier("@authority") { Req = true },
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var signResult = Signer.Sign("sig-resp", response, parameters, RfcTestKeys.HmacSharedSigningKey, algorithm);

        response.AddHeader("signature-input", signResult.SignatureInputHeaderValue);
        response.AddHeader("signature", signResult.SignatureHeaderValue);

        var verifyResult = Verifier.Verify("sig-resp", response, RfcTestKeys.HmacSharedVerificationKey, algorithm);
        verifyResult.IsValid.ShouldBeTrue(verifyResult.ErrorMessage);
    }

    /// <summary>
    /// Request-bound field component (e.g., <c>"content-digest";req</c>) should resolve from the associated request.
    /// </summary>
    [Fact]
    public void RequestBoundFieldComponent_ResolvesFromRequest()
    {
        var request = BuildRequest();
        var response = BuildResponse(request);

        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            new ComponentIdentifier("content-digest") { Req = true },
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key",
        };

        var result = SignatureBaseBuilder.BuildString(parameters, response);

        // content-digest;req should have the request's content-digest value
        result.ShouldContain("\"content-digest\";req: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:");
    }

    /// <summary>
    /// Using <c>;req</c> on a response without an associated request should throw.
    /// </summary>
    [Fact]
    public void ReqParam_WithoutAssociatedRequest_ThrowsSignatureBaseException()
    {
        var response = TestHttpMessageContext.CreateResponse(200); // no associated request
        response.AddHeader("content-type", "application/json");

        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            new ComponentIdentifier("@method") { Req = true },
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key",
        };

        Should.Throw<SignatureBaseException>(() =>
            SignatureBaseBuilder.BuildString(parameters, response));
    }
}
