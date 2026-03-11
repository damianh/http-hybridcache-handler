// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="SignatureHeaderParser"/>.
/// </summary>
public sealed class SignatureHeaderParserTests
{
    [Fact]
    public void ParseSignatureInput_SingleLabel_ParsesCorrectly()
    {
        var headerValue = "sig1=(\"@method\" \"@authority\");created=1618884473;keyid=\"test-key\"";
        var result = SignatureHeaderParser.ParseSignatureInput(headerValue);

        result.ShouldContainKey("sig1");
        var parameters = result["sig1"];
        parameters.CoveredComponents.Count.ShouldBe(2);
        parameters.CoveredComponents[0].Name.ShouldBe("@method");
        parameters.CoveredComponents[1].Name.ShouldBe("@authority");
        parameters.Created.ShouldNotBeNull();
        parameters.Created!.Value.ToUnixTimeSeconds().ShouldBe(1618884473);
        parameters.KeyId.ShouldBe("test-key");
    }

    [Fact]
    public void ParseSignatureInput_MultipleLabels_ParsesBoth()
    {
        var headerValue =
            "sig1=(\"@method\");created=1618884473;keyid=\"k1\", " +
            "sig2=(\"@authority\");created=1618884474;keyid=\"k2\"";
        var result = SignatureHeaderParser.ParseSignatureInput(headerValue);

        result.Count.ShouldBe(2);
        result.ShouldContainKey("sig1");
        result.ShouldContainKey("sig2");
    }

    [Fact]
    public void ParseSignature_SingleLabel_ParsesCorrectly()
    {
        // :dGVzdA==: is base64 for "test"
        var headerValue = "sig1=:dGVzdA==:";
        var result = SignatureHeaderParser.ParseSignature(headerValue);

        result.ShouldContainKey("sig1");
        var bytes = result["sig1"];
        bytes.ShouldBe(Convert.FromBase64String("dGVzdA=="));
    }

    [Fact]
    public void ParseSignature_MultipleLabels_ParsesBoth()
    {
        var headerValue = "sig1=:dGVzdA==:, sig2=:AQID:";
        var result = SignatureHeaderParser.ParseSignature(headerValue);

        result.Count.ShouldBe(2);
        result.ShouldContainKey("sig1");
        result.ShouldContainKey("sig2");
    }

    [Fact]
    public void SerializeSignatureInput_ProducesCorrectFormat()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Method,
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key",
        };

        var result = SignatureHeaderParser.SerializeSignatureInput("sig1", parameters);
        result.ShouldBe("sig1=(\"@method\" \"@authority\");created=1618884473;keyid=\"test-key\"");
    }

    [Fact]
    public void SerializeSignature_ProducesCorrectFormat()
    {
        var bytes = Convert.FromBase64String("dGVzdA==");
        var result = SignatureHeaderParser.SerializeSignature("sig1", bytes);
        result.ShouldBe("sig1=:dGVzdA==:");
    }

    [Fact]
    public void ParseSignatureInput_EmptyComponents_ParsesCorrectly()
    {
        var headerValue = "sig1=();created=1618884473;keyid=\"test\"";
        var result = SignatureHeaderParser.ParseSignatureInput(headerValue);

        result["sig1"].CoveredComponents.Count.ShouldBe(0);
    }

    [Fact]
    public void RoundTrip_SerializeAndParse_SignatureInput()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "my-key",
        };

        var serialized = SignatureHeaderParser.SerializeSignatureInput("sig1", parameters);
        var parsed = SignatureHeaderParser.ParseSignatureInput(serialized);

        parsed["sig1"].CoveredComponents.Count.ShouldBe(2);
        parsed["sig1"].KeyId.ShouldBe("my-key");
    }
}
