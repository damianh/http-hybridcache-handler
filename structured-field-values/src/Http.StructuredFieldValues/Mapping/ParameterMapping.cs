// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Describes a mapping from an RFC 8941 item parameter to a POCO property,
/// capturing compiled property accessors, the target RFC 8941 type, and optionality.
/// </summary>
internal sealed class ParameterMapping<T>
{
    internal ParameterMapping(
        string key,
        Func<T, object?> getter,
        Action<T, object?> setter,
        ValueKind kind,
        bool isRequired,
        Type clrType)
    {
        Key = key;
        Getter = getter;
        Setter = setter;
        Kind = kind;
        IsRequired = isRequired;
        ClrType = clrType;
    }

    /// <summary>RFC 8941 parameter key (a token).</summary>
    internal string Key { get; }

    /// <summary>Compiled property getter returning a boxed value.</summary>
    internal Func<T, object?> Getter { get; }

    /// <summary>Compiled property setter accepting a boxed value.</summary>
    internal Action<T, object?> Setter { get; }

    /// <summary>RFC 8941 bare item type for this parameter value.</summary>
    internal ValueKind Kind { get; }

    /// <summary>
    /// Whether the parameter is required during parse.
    /// A missing required parameter throws <see cref="StructuredFieldParseException"/>.
    /// </summary>
    internal bool IsRequired { get; }

    /// <summary>The CLR type of the property (used for int/int? narrowing).</summary>
    internal Type ClrType { get; }
}
