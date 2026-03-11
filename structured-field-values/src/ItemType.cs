// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Enumeration of item types defined in RFC 8941.
/// </summary>
public enum ItemType
{
    /// <summary>
    /// Integer value (e.g., 42, -17).
    /// Range: -999,999,999,999,999 to 999,999,999,999,999
    /// </summary>
    Integer,

    /// <summary>
    /// Decimal value (e.g., 4.5, -1.23).
    /// Up to 12 significant digits with up to 3 decimal places.
    /// </summary>
    Decimal,

    /// <summary>
    /// String value (e.g., "hello world").
    /// Printable ASCII characters.
    /// </summary>
    String,

    /// <summary>
    /// Token value (e.g., application/json, foo).
    /// Unquoted identifier following specific syntax rules.
    /// </summary>
    Token,

    /// <summary>
    /// Byte sequence value (e.g., :aGVsbG8=:).
    /// Base64-encoded binary data.
    /// </summary>
    ByteSequence,

    /// <summary>
    /// Boolean value (e.g., ?1 for true, ?0 for false).
    /// </summary>
    Boolean
}
