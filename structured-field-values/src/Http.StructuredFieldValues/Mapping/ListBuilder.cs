// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Fluent builder that describes how to map an RFC 8941 structured field list
/// to and from a POCO of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The POCO type. Must have a public parameterless constructor.</typeparam>
public sealed class ListBuilder<T> where T : new()
{
    internal ListElementConfig<T>? ElementConfig { get; private set; }

    /// <summary>
    /// Maps the list elements to a collection property on the POCO.
    /// The RFC 8941 element type is inferred from <typeparamref name="TElement"/>:
    /// <c>int</c>/<c>long</c> → Integer, <c>decimal</c> → Decimal,
    /// <c>bool</c> → Boolean, <c>string</c> → String, <c>byte[]</c> → ByteSequence.
    /// </summary>
    /// <typeparam name="TElement">The element CLR type.</typeparam>
    /// <param name="property">A property-access expression for the collection property.</param>
    /// <returns>This builder for chaining.</returns>
    public ListBuilder<T> Elements<TElement>(Expression<Func<T, IReadOnlyList<TElement>>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureNoElementConfig();

        var (getter, setter) = PropertyAccessor.Compile(property);
        var elementKind = ItemTypeResolver.Resolve(typeof(TElement));

        ElementConfig = new ListElementConfig<T>(
            v => getter(v),
            (inst, v) => setter(inst, (IReadOnlyList<TElement>)v!),
            elementKind,
            isToken: false,
            typeof(TElement),
            nestedParse: null,
            nestedSerialize: null);

        return this;
    }

    /// <summary>
    /// Maps the list elements to a <see cref="IReadOnlyList{String}"/> property,
    /// treating each element as an RFC 8941 Token.
    /// </summary>
    /// <param name="property">A property-access expression for the collection property.</param>
    /// <returns>This builder for chaining.</returns>
    public ListBuilder<T> TokenElements(Expression<Func<T, IReadOnlyList<string>>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureNoElementConfig();

        var (getter, setter) = PropertyAccessor.Compile(property);

        ElementConfig = new ListElementConfig<T>(
            v => getter(v),
            (inst, v) => setter(inst, (IReadOnlyList<string>)v!),
            ValueKind.Token,
            isToken: true,
            typeof(string),
            nestedParse: null,
            nestedSerialize: null);

        return this;
    }

    /// <summary>
    /// Maps the list elements to a collection property on the POCO, where each element
    /// is a structured item (value + parameters) mapped by <paramref name="elementMapper"/>.
    /// </summary>
    /// <typeparam name="TElement">The element POCO type.</typeparam>
    /// <param name="property">A property-access expression for the collection property.</param>
    /// <param name="elementMapper">A mapper that handles each element's item-level parse/serialize.</param>
    /// <returns>This builder for chaining.</returns>
    public ListBuilder<T> Elements<TElement>(
        Expression<Func<T, IReadOnlyList<TElement>>> property,
        StructuredFieldMapper<TElement> elementMapper)
        where TElement : new()
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(elementMapper);
        EnsureNoElementConfig();

        var (getter, setter) = PropertyAccessor.Compile(property);

        ElementConfig = new ListElementConfig<T>(
            v => getter(v),
            (inst, v) => setter(inst, (IReadOnlyList<TElement>)v!),
            ValueKind.Token, // unused for nested
            isToken: false,
            typeof(TElement),
            nestedParse: item => elementMapper.ParseItem(item)!,
            nestedSerialize: obj => elementMapper.SerializeItem((TElement)obj));

        return this;
    }

    private void EnsureNoElementConfig()
    {
        if (ElementConfig != null)
            throw new InvalidOperationException("An element mapping has already been registered for this list.");
    }
}

/// <summary>
/// Configuration describing how to parse/serialize individual list elements.
/// </summary>
internal sealed class ListElementConfig<T>
{
    internal ListElementConfig(
        Func<T, object?> getter,
        Action<T, object?> setter,
        ValueKind elementKind,
        bool isToken,
        Type elementClrType,
        Func<StructuredFieldItem, object>? nestedParse,
        Func<object, StructuredFieldItem>? nestedSerialize)
    {
        Getter = getter;
        Setter = setter;
        ElementKind = elementKind;
        IsToken = isToken;
        ElementClrType = elementClrType;
        NestedItemParseDelegate = nestedParse;
        NestedItemSerializeDelegate = nestedSerialize;
    }

    internal Func<T, object?> Getter { get; }
    internal Action<T, object?> Setter { get; }
    internal ValueKind ElementKind { get; }
    internal bool IsToken { get; }
    internal Type ElementClrType { get; }
    internal Func<StructuredFieldItem, object>? NestedItemParseDelegate { get; }
    internal Func<object, StructuredFieldItem>? NestedItemSerializeDelegate { get; }
    internal bool IsNestedItem => NestedItemParseDelegate != null;
}
