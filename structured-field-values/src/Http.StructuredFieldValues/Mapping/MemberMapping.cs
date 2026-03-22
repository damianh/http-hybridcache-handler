// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Describes a single dictionary member mapping: the RFC 8941 key, compiled property
/// accessors, the target RFC 8941 type, and whether the member is required or optional.
/// </summary>
internal sealed class MemberMapping<T>
{
    internal MemberMapping(
        string key,
        Func<T, object?> getter,
        Action<T, object?> setter,
        ValueKind kind,
        bool isRequired,
        bool isToken,
        Type clrType,
        InnerListConfig? innerList = null)
    {
        Key = key;
        Getter = getter;
        Setter = setter;
        Kind = kind;
        IsRequired = isRequired;
        IsToken = isToken;
        ClrType = clrType;
        InnerList = innerList;
    }

    /// <summary>RFC 8941 dictionary key.</summary>
    internal string Key { get; }

    /// <summary>Compiled property getter returning a boxed value.</summary>
    internal Func<T, object?> Getter { get; }

    /// <summary>Compiled property setter accepting a boxed value.</summary>
    internal Action<T, object?> Setter { get; }

    /// <summary>The CLR type of the property (used for int/int? narrowing).</summary>
    internal Type ClrType { get; }

    /// <summary>RFC 8941 bare item type for this member.</summary>
    internal ValueKind Kind { get; }

    /// <summary>
    /// Whether the member is required during parse.
    /// A missing required member throws <see cref="StructuredFieldParseException"/>.
    /// </summary>
    internal bool IsRequired { get; }

    /// <summary>
    /// Whether the value should be treated as a Token rather than a String.
    /// Only meaningful when <see cref="Kind"/> is <see cref="ValueKind.Token"/>.
    /// </summary>
    internal bool IsToken { get; }

    /// <summary>
    /// Inner-list configuration when this member maps to an inner list.
    /// <see langword="null"/> for simple item members.
    /// </summary>
    internal InnerListConfig? InnerList { get; }

    /// <summary>Whether this mapping represents an inner-list member.</summary>
    internal bool IsInnerList => InnerList != null;
}

/// <summary>
/// Configuration for inner-list members, capturing element kind and element POCO mapper (for nested items).
/// </summary>
internal sealed class InnerListConfig
{
    internal InnerListConfig(ValueKind elementKind, bool isToken, Type elementClrType)
    {
        ElementKind = elementKind;
        IsToken = isToken;
        ElementClrType = elementClrType;
        NestedItemParseDelegate = null;
        NestedItemSerializeDelegate = null;
    }

    internal InnerListConfig(
        Type elementClrType,
        Func<StructuredFieldItem, object> nestedParse,
        Func<object, StructuredFieldItem> nestedSerialize)
    {
        ElementKind = ValueKind.Token; // unused for nested; kind determined by nested mapper
        IsToken = false;
        ElementClrType = elementClrType;
        NestedItemParseDelegate = nestedParse;
        NestedItemSerializeDelegate = nestedSerialize;
    }

    internal ValueKind ElementKind { get; }
    internal bool IsToken { get; }
    internal Type ElementClrType { get; }

    /// <summary>
    /// When set, each element is a nested structured item handled by this delegate.
    /// </summary>
    internal Func<StructuredFieldItem, object>? NestedItemParseDelegate { get; }

    /// <summary>
    /// When set, each POCO element is serialized to a <see cref="StructuredFieldItem"/> by this delegate.
    /// </summary>
    internal Func<object, StructuredFieldItem>? NestedItemSerializeDelegate { get; }

    internal bool IsNestedItem => NestedItemParseDelegate != null;
}
