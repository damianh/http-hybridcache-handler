// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents parameters attached to items or inner lists in structured field values.
/// Parameters are an ordered map of key-value pairs where keys are tokens and values are items.
/// RFC 8941 § 3.1.2
/// </summary>
public sealed class Parameters : IEnumerable<KeyValuePair<string, StructuredFieldItem?>>
{
    private readonly OrderedDictionary<string, StructuredFieldItem?> _parameters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameters"/> class.
    /// </summary>
    public Parameters()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameters"/> class with initial values.
    /// </summary>
    /// <param name="parameters">Initial parameters.</param>
    public Parameters(IEnumerable<KeyValuePair<string, StructuredFieldItem?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        
        foreach (var kvp in parameters)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int Count => _parameters.Count;

    /// <summary>
    /// Gets or sets the parameter with the specified key.
    /// </summary>
    /// <param name="key">The parameter key (must be a valid token).</param>
    /// <returns>The parameter value, or null if the key is present with no value.</returns>
    public StructuredFieldItem? this[string key]
    {
        get => _parameters[key];
        set
        {
            ValidateKey(key);
            _parameters[key] = value;
        }
    }

    /// <summary>
    /// Adds a parameter with the specified key and value.
    /// </summary>
    /// <param name="key">The parameter key (must be a valid token).</param>
    /// <param name="value">The parameter value.</param>
    public void Add(string key, StructuredFieldItem? value)
    {
        ValidateKey(key);
        _parameters.Add(key, value);
    }

    /// <summary>
    /// Tries to get the parameter with the specified key.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The parameter value if found.</param>
    /// <returns>True if the parameter exists, false otherwise.</returns>
    public bool TryGetValue(string key, [NotNullWhen(true)] out StructuredFieldItem? value) => _parameters.TryGetValue(key, out value);

    /// <summary>
    /// Determines whether a parameter with the specified key exists.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>True if the parameter exists, false otherwise.</returns>
    public bool ContainsKey(string key) => _parameters.ContainsKey(key);

    /// <summary>
    /// Removes the parameter with the specified key.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>True if the parameter was removed, false otherwise.</returns>
    public bool Remove(string key) => _parameters.Remove(key);

    /// <summary>
    /// Removes all parameters.
    /// </summary>
    public void Clear() => _parameters.Clear();

    /// <summary>
    /// Gets an enumerator that iterates through the parameters in insertion order.
    /// </summary>
    public IEnumerator<KeyValuePair<string, StructuredFieldItem?>> GetEnumerator() => _parameters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        
        if (!TokenItem.IsValidKey(key))
        {
            throw new ArgumentException(
                $"Parameter key '{key}' is not a valid RFC 8941 key. " +
                "Keys must start with a lowercase letter or '*' and contain only " +
                "lowercase letters, digits, '_', '-', '.', or '*'.",
                nameof(key));
        }
    }
}
