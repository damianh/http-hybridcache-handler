// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Describes the mapping from the bare RFC 8941 item value to a POCO property,
/// capturing compiled property accessors and the target RFC 8941 type.
/// </summary>
internal sealed class ValueMapping<T>
{
    internal ValueMapping(
        Func<T, object?> getter,
        Action<T, object?> setter,
        ValueKind kind,
        bool isRequired,
        Type clrType)
    {
        Getter = getter;
        Setter = setter;
        Kind = kind;
        IsRequired = isRequired;
        ClrType = clrType;
    }

    /// <summary>Compiled property getter returning a boxed value.</summary>
    internal Func<T, object?> Getter { get; }

    /// <summary>Compiled property setter accepting a boxed value.</summary>
    internal Action<T, object?> Setter { get; }

    /// <summary>RFC 8941 bare item type for the item value.</summary>
    internal ValueKind Kind { get; }

    /// <summary>
    /// Whether the item value is required during parse.
    /// An absent value throws <see cref="StructuredFieldParseException"/>.
    /// </summary>
    internal bool IsRequired { get; }

    /// <summary>The CLR type of the property (used for int/int? narrowing).</summary>
    internal Type ClrType { get; }
}
