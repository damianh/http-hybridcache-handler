// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Resolves verification key material from a key identifier.
/// Implementations provide key lookup from keystores, configuration, or remote sources.
/// </summary>
public interface IKeyResolver
{
    /// <summary>
    /// Resolves the verification key for the given key identifier.
    /// </summary>
    /// <param name="keyId">The key identifier from the <c>keyid</c> signature parameter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The verification key if found and trusted; <see langword="null"/> if the key is not found or not trusted.
    /// </returns>
    Task<VerificationKey?> ResolveKeyAsync(string keyId, CancellationToken cancellationToken = default);
}
