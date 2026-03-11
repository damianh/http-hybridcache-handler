// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures.Keys;

/// <summary>
/// Ed25519 signing key material.
/// Wraps raw Ed25519 private key bytes (32 bytes).
/// </summary>
public sealed class Ed25519SigningKey : SigningKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519SigningKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="privateKeyBytes">The raw Ed25519 private key bytes (32 bytes).</param>
    public Ed25519SigningKey(string keyId, byte[] privateKeyBytes)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(privateKeyBytes);
        PrivateKeyBytes = privateKeyBytes;
    }

    /// <summary>Gets the raw Ed25519 private key bytes.</summary>
    public byte[] PrivateKeyBytes { get; }

    /// <inheritdoc/>
    public override string? AlgorithmHint => "ed25519";
}

/// <summary>
/// Ed25519 verification key material.
/// Wraps raw Ed25519 public key bytes (32 bytes).
/// </summary>
public sealed class Ed25519VerificationKey : VerificationKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519VerificationKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="publicKeyBytes">The raw Ed25519 public key bytes (32 bytes).</param>
    public Ed25519VerificationKey(string keyId, byte[] publicKeyBytes)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(publicKeyBytes);
        PublicKeyBytes = publicKeyBytes;
    }

    /// <summary>Gets the raw Ed25519 public key bytes.</summary>
    public byte[] PublicKeyBytes { get; }

    /// <inheritdoc/>
    public override string? AlgorithmHint => "ed25519";
}
