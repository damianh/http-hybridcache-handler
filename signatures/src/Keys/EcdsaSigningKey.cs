// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace DamianH.Http.HttpSignatures.Keys;

/// <summary>
/// ECDSA signing key material wrapping an <see cref="System.Security.Cryptography.ECDsa"/> instance.
/// </summary>
public sealed class EcdsaSigningKey : SigningKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EcdsaSigningKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="ecdsa">The ECDsa key (must contain the private key).</param>
    /// <param name="algorithmHint">Optional algorithm hint.</param>
    public EcdsaSigningKey(string keyId, ECDsa ecdsa, string? algorithmHint = null)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        Ecdsa = ecdsa;
        AlgorithmHint = algorithmHint;
    }

    /// <summary>Gets the ECDsa key.</summary>
    public ECDsa Ecdsa { get; }

    /// <inheritdoc/>
    public override string? AlgorithmHint { get; }
}

/// <summary>
/// ECDSA verification key material wrapping an <see cref="System.Security.Cryptography.ECDsa"/> instance.
/// </summary>
public sealed class EcdsaVerificationKey : VerificationKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EcdsaVerificationKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="ecdsa">The ECDsa key (public key only is sufficient).</param>
    /// <param name="algorithmHint">Optional algorithm hint.</param>
    public EcdsaVerificationKey(string keyId, ECDsa ecdsa, string? algorithmHint = null)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        Ecdsa = ecdsa;
        AlgorithmHint = algorithmHint;
    }

    /// <summary>Gets the ECDsa key.</summary>
    public ECDsa Ecdsa { get; }

    /// <inheritdoc/>
    public override string? AlgorithmHint { get; }
}
