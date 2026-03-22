// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Verifies HTTP message signatures per RFC 9421 §3.2.
/// Parses <c>Signature-Input</c> and <c>Signature</c> headers, reconstructs the signature base,
/// and verifies the signature using the provided key material and algorithm.
/// </summary>
public sealed class HttpMessageVerifier
{
    /// <summary>
    /// Verifies a specific labeled signature on an HTTP message using explicit key and algorithm.
    /// </summary>
    /// <param name="label">The signature label to verify (e.g., "sig1").</param>
    /// <param name="context">The HTTP message context containing the signature headers.</param>
    /// <param name="key">The verification key material.</param>
    /// <param name="algorithm">The signature algorithm to use for verification.</param>
    /// <returns>A <see cref="VerificationResult"/> indicating whether the signature is valid.</returns>
    public VerificationResult Verify(
        string label,
        IHttpMessageContext context,
        VerificationKey key,
        ISignatureAlgorithm algorithm)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(algorithm);

        // Parse Signature-Input header
        var signatureInputRaw = context.GetHeaderValue("signature-input");
        if (signatureInputRaw is null)
            return VerificationResult.Failure("Signature-Input header not found.");

        var signatureInputDict = SignatureHeaderParser.ParseSignatureInput(signatureInputRaw);
        if (!signatureInputDict.TryGetValue(label, out var parameters))
            return VerificationResult.Failure($"Signature label '{label}' not found in Signature-Input header.");

        // Parse Signature header
        var signatureRaw = context.GetHeaderValue("signature");
        if (signatureRaw is null)
            return VerificationResult.Failure("Signature header not found.", parameters);

        var signatureDict = SignatureHeaderParser.ParseSignature(signatureRaw);
        if (!signatureDict.TryGetValue(label, out var signatureBytes))
            return VerificationResult.Failure($"Signature label '{label}' not found in Signature header.", parameters);

        // Reconstruct the signature base
        byte[] signatureBase;
        try
        {
            signatureBase = SignatureBaseBuilder.Build(parameters, context);
        }
        catch (SignatureBaseException ex)
        {
            return VerificationResult.Failure($"Failed to construct signature base: {ex.Message}", parameters);
        }

        // Verify — let key type mismatches (ArgumentException) propagate as they indicate programmer error
        var isValid = algorithm.Verify(signatureBase, key, signatureBytes);

        return isValid
            ? VerificationResult.Success(parameters)
            : VerificationResult.Failure("Signature verification failed: cryptographic verification returned false.", parameters);
    }

    /// <summary>
    /// Verifies a signature using a key resolver and algorithm registry for runtime resolution.
    /// </summary>
    /// <param name="label">The signature label to verify (e.g., "sig1").</param>
    /// <param name="context">The HTTP message context containing the signature headers.</param>
    /// <param name="keyResolver">Resolves verification keys by key identifier.</param>
    /// <param name="algorithmRegistry">Resolves algorithms by algorithm name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="VerificationResult"/> indicating whether the signature is valid.</returns>
    public async Task<VerificationResult> VerifyAsync(
        string label,
        IHttpMessageContext context,
        IKeyResolver keyResolver,
        ISignatureAlgorithmRegistry algorithmRegistry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keyResolver);
        ArgumentNullException.ThrowIfNull(algorithmRegistry);

        // Parse Signature-Input header
        var signatureInputRaw = context.GetHeaderValue("signature-input");
        if (signatureInputRaw is null)
            return VerificationResult.Failure("Signature-Input header not found.");

        var signatureInputDict = SignatureHeaderParser.ParseSignatureInput(signatureInputRaw);
        if (!signatureInputDict.TryGetValue(label, out var parameters))
            return VerificationResult.Failure($"Signature label '{label}' not found in Signature-Input header.");

        // Resolve algorithm
        var algName = parameters.Algorithm;
        if (algName is null)
            return VerificationResult.Failure("Signature parameters do not specify an algorithm.", parameters);

        var algorithm = algorithmRegistry.GetAlgorithm(algName);
        if (algorithm is null)
            return VerificationResult.Failure($"Algorithm '{algName}' is not registered.", parameters);

        // Resolve key
        var keyId = parameters.KeyId;
        if (keyId is null)
            return VerificationResult.Failure("Signature parameters do not specify a keyid.", parameters);

        var key = await keyResolver.ResolveKeyAsync(keyId, cancellationToken);
        if (key is null)
            return VerificationResult.Failure($"Key '{keyId}' could not be resolved.", parameters);

        return Verify(label, context, key, algorithm);
    }
}
