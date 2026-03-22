// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Default implementation of <see cref="ISignatureAlgorithmRegistry"/>.
/// Maps algorithm names to <see cref="ISignatureAlgorithm"/> instances.
/// </summary>
public sealed class SignatureAlgorithmRegistry : ISignatureAlgorithmRegistry
{
    private readonly Dictionary<string, ISignatureAlgorithm> _algorithms = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an algorithm instance.
    /// </summary>
    /// <param name="algorithm">The algorithm to register.</param>
    public void Register(ISignatureAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        _algorithms[algorithm.AlgorithmName] = algorithm;
    }

    /// <inheritdoc/>
    public ISignatureAlgorithm? GetAlgorithm(string algorithmName)
    {
        ArgumentException.ThrowIfNullOrEmpty(algorithmName);
        return _algorithms.GetValueOrDefault(algorithmName);
    }
}
