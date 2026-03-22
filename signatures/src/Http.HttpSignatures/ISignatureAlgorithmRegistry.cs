// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Registry for resolving <see cref="ISignatureAlgorithm"/> instances by algorithm name.
/// Used by the verifier for runtime algorithm resolution.
/// </summary>
public interface ISignatureAlgorithmRegistry
{
    /// <summary>
    /// Gets the algorithm instance for the given algorithm name.
    /// </summary>
    /// <param name="algorithmName">The algorithm name (e.g., "rsa-pss-sha512").</param>
    /// <returns>The algorithm instance, or <see langword="null"/> if not registered.</returns>
    ISignatureAlgorithm? GetAlgorithm(string algorithmName);
}
