// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Creates HTTP message signatures per RFC 9421 §3.1.
/// Constructs the signature base, signs it, and produces header values.
/// </summary>
public sealed class HttpMessageSigner
{
    /// <summary>
    /// Signs an HTTP message, producing <c>Signature-Input</c> and <c>Signature</c> header values.
    /// </summary>
    /// <param name="label">The signature label (e.g., "sig1").</param>
    /// <param name="context">The HTTP message context to sign.</param>
    /// <param name="parameters">The signature parameters defining covered components and metadata.</param>
    /// <param name="key">The signing key material.</param>
    /// <param name="algorithm">The signature algorithm to use.</param>
    /// <returns>A <see cref="SignatureResult"/> containing the header values.</returns>
    public SignatureResult Sign(
        string label,
        IHttpMessageContext context,
        SignatureParameters parameters,
        SigningKey key,
        ISignatureAlgorithm algorithm)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(algorithm);

        // Build the signature base
        var signatureBase = SignatureBaseBuilder.Build(parameters, context);

        // Sign the base
        var signatureBytes = algorithm.Sign(signatureBase, key);

        // Build the Signature-Input header member: label=(components);params
        var serializedParams = parameters.Serialize();
        var signatureInputHeaderValue = $"{label}={serializedParams}";

        // Build the Signature header member: label=:base64:
        var base64Signature = Convert.ToBase64String(signatureBytes);
        var signatureHeaderValue = $"{label}=:{base64Signature}:";

        return new SignatureResult(
            label,
            signatureInputHeaderValue,
            signatureHeaderValue,
            signatureBytes);
    }
}
