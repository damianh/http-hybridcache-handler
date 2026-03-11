// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a token item in a structured field value.
/// RFC 8941 defines tokens as unquoted identifiers following specific syntax rules.
/// Tokens must start with an alpha character or '*' and can contain alphanumerics, ':', '/', '.', '-', '_', '~', '%', '!', '$', '&amp;', '#', '+', or '*'.
/// </summary>
public sealed partial class TokenItem : StructuredFieldItem
{
    private static readonly Regex TokenPattern = CreateTokenPattern();

    private readonly string _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenItem"/> class.
    /// </summary>
    /// <param name="value">The token value.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when value is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when value doesn't match RFC 8941 token syntax.
    /// </exception>
    public TokenItem(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ValidateToken(value);
        _value = value;
    }

    /// <summary>
    /// Gets the token value.
    /// </summary>
    public string TokenValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Token;

    /// <inheritdoc/>
    public override string ToString() => _value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is TokenItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from string to TokenItem.
    /// </summary>
    public static implicit operator TokenItem(string value) => new(value);

    /// <summary>
    /// Implicit conversion from TokenItem to string.
    /// </summary>
    public static implicit operator string(TokenItem item) => item._value;

    /// <summary>
    /// Validates whether a string is a valid RFC 8941 token.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidToken(string value) =>
        !string.IsNullOrEmpty(value) && TokenPattern.IsMatch(value);

    /// <summary>
    /// Validates whether a string is a valid RFC 8941 key (for dictionary member keys and parameter keys).
    /// Keys use a more restrictive grammar than tokens:
    /// <c>sf-key = ( lcalpha / "*" ) *( lcalpha / DIGIT / "_" / "-" / "." / "*" )</c>
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidKey(string value) =>
        !string.IsNullOrEmpty(value) && KeyPattern.IsMatch(value);

    private static void ValidateToken(string value)
    {
        if (!IsValidToken(value))
        {
            throw new ArgumentException(
                $"Invalid token: '{value}'. RFC 8941 tokens must start with alpha or '*' " +
                "and contain only alphanumerics, ':', '/', '.', '-', '_', '~', '%', '!', '$', '&', '#', '+', or '*'.",
                nameof(value));
        }
    }

    [GeneratedRegex("^[a-zA-Z*][a-zA-Z0-9:/.\\-_~%!$&#+*]*$", RegexOptions.Compiled)]
    private static partial Regex CreateTokenPattern();

    [GeneratedRegex("^[a-z*][a-z0-9_\\-.*]*$", RegexOptions.Compiled)]
    private static partial Regex CreateKeyPattern();

    private static readonly Regex KeyPattern = CreateKeyPattern();
}
