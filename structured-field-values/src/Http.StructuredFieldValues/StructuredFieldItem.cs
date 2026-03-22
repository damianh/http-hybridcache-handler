// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Base class for all structured field items as defined in RFC 8941.
/// Items are the fundamental building blocks of structured field values.
/// </summary>
public abstract class StructuredFieldItem
{
    /// <summary>
    /// Gets the parameters associated with this item.
    /// Parameters are key-value pairs that provide additional metadata.
    /// </summary>
    public Parameters Parameters { get; init; } = new();

    /// <summary>
    /// Gets the underlying value of this item.
    /// </summary>
    public abstract object Value { get; }

    /// <summary>
    /// Gets the type of this structured field item.
    /// </summary>
    public abstract ItemType Type { get; }
}
