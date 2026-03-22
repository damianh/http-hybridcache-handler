// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="DerivedComponentResolver"/> per RFC 9421 §2.2.
/// Exercises each derived component individually.
/// </summary>
public sealed class DerivedComponentResolverTests
{
    private static TestHttpMessageContext BuildRequest(
        string method = "GET",
        string scheme = "https",
        string authority = "example.com",
        string path = "/foo",
        string? query = null) =>
        TestHttpMessageContext.CreateRequest(method, scheme, authority, path, query);

    private static TestHttpMessageContext BuildResponse(int status = 200) =>
        TestHttpMessageContext.CreateResponse(status, BuildRequest());

    // @method
    [Fact]
    public void Resolve_Method_ReturnsUppercaseMethod()
    {
        var ctx = BuildRequest(method: "post");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Method, ctx);
        result.ShouldBe("POST");
    }

    [Fact]
    public void Resolve_Method_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Method, ctx));
    }

    // @target-uri
    [Fact]
    public void Resolve_TargetUri_ReturnsFullUri()
    {
        var ctx = BuildRequest(query: "?a=b");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.TargetUri, ctx);
        result.ShouldBe("https://example.com/foo?a=b");
    }

    [Fact]
    public void Resolve_TargetUri_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.TargetUri, ctx));
    }

    // @authority
    [Fact]
    public void Resolve_Authority_ReturnsLowercase()
    {
        var ctx = BuildRequest(authority: "Example.COM");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Authority, ctx);
        result.ShouldBe("example.com");
    }

    [Fact]
    public void Resolve_Authority_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Authority, ctx));
    }

    // @scheme
    [Fact]
    public void Resolve_Scheme_ReturnsLowercase()
    {
        var ctx = BuildRequest(scheme: "HTTPS");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Scheme, ctx);
        result.ShouldBe("https");
    }

    [Fact]
    public void Resolve_Scheme_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Scheme, ctx));
    }

    // @request-target
    [Fact]
    public void Resolve_RequestTarget_ReturnsPathAndQuery()
    {
        var ctx = BuildRequest(query: "?a=b");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.RequestTarget, ctx);
        result.ShouldBe("/foo?a=b");
    }

    [Fact]
    public void Resolve_RequestTarget_NoQuery_ReturnsPath()
    {
        var ctx = BuildRequest();
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.RequestTarget, ctx);
        result.ShouldBe("/foo");
    }

    // @path
    [Fact]
    public void Resolve_Path_ReturnsPath()
    {
        var ctx = BuildRequest(path: "/bar/baz");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Path, ctx);
        result.ShouldBe("/bar/baz");
    }

    [Fact]
    public void Resolve_Path_EmptyPath_ReturnsSlash()
    {
        var ctx = BuildRequest(path: "");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Path, ctx);
        result.ShouldBe("/");
    }

    // @query
    [Fact]
    public void Resolve_Query_ReturnsQueryWithLeadingQuestionMark()
    {
        var ctx = BuildRequest(query: "?a=b&c=d");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Query, ctx);
        result.ShouldBe("?a=b&c=d");
    }

    [Fact]
    public void Resolve_Query_Absent_ReturnsQuestionMark()
    {
        var ctx = BuildRequest();
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Query, ctx);
        result.ShouldBe("?");
    }

    // @query-param
    [Fact]
    public void Resolve_QueryParam_ReturnsParameterValue()
    {
        var ctx = BuildRequest(query: "?param=Value&Pet=dog");
        var id = ComponentIdentifier.QueryParam("Pet");
        var result = DerivedComponentResolver.Resolve(id, ctx);
        result.ShouldBe("dog");
    }

    [Fact]
    public void Resolve_QueryParam_MissingParameter_Throws()
    {
        var ctx = BuildRequest(query: "?a=b");
        var id = ComponentIdentifier.QueryParam("missing");
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    [Fact]
    public void Resolve_QueryParam_NoQueryString_Throws()
    {
        var ctx = BuildRequest();
        var id = ComponentIdentifier.QueryParam("x");
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    // @status
    [Fact]
    public void Resolve_Status_ReturnsStatusString()
    {
        var ctx = BuildResponse(200);
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Status, ctx);
        result.ShouldBe("200");
    }

    [Fact]
    public void Resolve_Status_404_ReturnsStatusString()
    {
        var ctx = BuildResponse(404);
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Status, ctx);
        result.ShouldBe("404");
    }

    [Fact]
    public void Resolve_Status_OnRequest_Throws()
    {
        var ctx = BuildRequest();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Status, ctx));
    }

    // Unknown derived component
    [Fact]
    public void Resolve_UnknownDerived_Throws()
    {
        var id = new ComponentIdentifier("@unknown");
        var ctx = BuildRequest();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    // req parameter
    [Fact]
    public void Resolve_WithReqParameter_ResolvesFromAssociatedRequest()
    {
        var request = BuildRequest(method: "POST");
        var response = TestHttpMessageContext.CreateResponse(200, request);
        var id = new ComponentIdentifier("@method") { Req = true };
        var result = DerivedComponentResolver.Resolve(id, response);
        result.ShouldBe("POST");
    }

    [Fact]
    public void Resolve_WithReqParameter_NoAssociatedRequest_Throws()
    {
        var response = TestHttpMessageContext.CreateResponse(200);
        var id = new ComponentIdentifier("@method") { Req = true };
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, response));
    }
}
