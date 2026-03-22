// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Fluent builder that describes how to map an RFC 8941 structured field dictionary
/// to and from a POCO of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The POCO type. Must have a public parameterless constructor.</typeparam>
public sealed class DictionaryBuilder<T> where T : new()
{
    private readonly List<MemberMapping<T>> _members = [];
    private readonly HashSet<string> _keys = [];

    internal IReadOnlyList<MemberMapping<T>> Members => _members;

    /// <summary>
    /// Maps a dictionary member key to a POCO property.
    /// The RFC 8941 type is inferred from the property's CLR type:
    /// <c>int</c>/<c>long</c> → Integer, <c>decimal</c> → Decimal,
    /// <c>bool</c> → Boolean, <c>string</c> → String, <c>byte[]</c> → ByteSequence.
    /// Nullable CLR types are treated as optional (missing member → property left at default).
    /// Non-nullable CLR types are treated as required (missing member → <see cref="StructuredFieldParseException"/>).
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="key">The RFC 8941 dictionary key (must be a valid token).</param>
    /// <param name="property">A property-access expression (e.g. <c>x => x.Urgency</c>).</param>
    /// <returns>This builder for chaining.</returns>
    public DictionaryBuilder<T> Member<TValue>(string key, Expression<Func<T, TValue>> property)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        EnsureUniqueKey(key);

        var prop = PropertyAccessor.GetProperty(property);
        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TValue));
        var isRequired = !IsNullable(typeof(TValue));

        _members.Add(new MemberMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (TValue)v!),
            kind,
            isRequired,
            isToken: false,
            clrType: prop.PropertyType));

        return this;
    }

    /// <summary>
    /// Maps a dictionary member key to a <see cref="string"/> property, serialized as an RFC 8941 Token.
    /// The member is optional when the property is nullable.
    /// </summary>
    /// <param name="key">The RFC 8941 dictionary key.</param>
    /// <param name="property">A property-access expression.</param>
    /// <returns>This builder for chaining.</returns>
    public DictionaryBuilder<T> TokenMember(string key, Expression<Func<T, string?>> property)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        EnsureUniqueKey(key);

        var prop = PropertyAccessor.GetProperty(property);
        var (getter, setter) = PropertyAccessor.Compile(property);

        _members.Add(new MemberMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (string?)v),
            ValueKind.Token,
            isRequired: false,
            isToken: true,
            clrType: prop.PropertyType));

        return this;
    }

    /// <summary>
    /// Maps a dictionary member key to an inner list of primitive elements.
    /// The RFC 8941 element type is inferred from <typeparamref name="TElement"/>.
    /// The collection property is optional (missing member → <see langword="null"/>).
    /// </summary>
    /// <typeparam name="TElement">The element CLR type.</typeparam>
    /// <param name="key">The RFC 8941 dictionary key.</param>
    /// <param name="property">A property-access expression for the collection property.</param>
    /// <returns>This builder for chaining.</returns>
    public DictionaryBuilder<T> InnerList<TElement>(
        string key,
        Expression<Func<T, IReadOnlyList<TElement>?>> property)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        EnsureUniqueKey(key);

        var (getter, setter) = PropertyAccessor.Compile(property);
        var elementKind = ItemTypeResolver.Resolve(typeof(TElement));
        var innerListConfig = new InnerListConfig(elementKind, isToken: false, typeof(TElement));

        _members.Add(new MemberMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (IReadOnlyList<TElement>?)v),
            ValueKind.Token, // kind unused for inner list
            isRequired: false,
            isToken: false,
            clrType: typeof(IReadOnlyList<TElement>),
            innerList: innerListConfig));

        return this;
    }

    /// <summary>
    /// Maps a dictionary member key to an inner list of RFC 8941 Token elements
    /// stored as strings.
    /// The collection property is optional (missing member → <see langword="null"/>).
    /// </summary>
    /// <param name="key">The RFC 8941 dictionary key.</param>
    /// <param name="property">A property-access expression for the collection property.</param>
    /// <returns>This builder for chaining.</returns>
    public DictionaryBuilder<T> TokenInnerList(
        string key,
        Expression<Func<T, IReadOnlyList<string>?>> property)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        EnsureUniqueKey(key);

        var (getter, setter) = PropertyAccessor.Compile(property);
        var innerListConfig = new InnerListConfig(ValueKind.Token, isToken: true, typeof(string));

        _members.Add(new MemberMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (IReadOnlyList<string>?)v),
            ValueKind.Token, // kind unused for inner list
            isRequired: false,
            isToken: true,
            clrType: typeof(IReadOnlyList<string>),
            innerList: innerListConfig));

        return this;
    }

    /// <summary>
    /// Maps a dictionary member key to an inner list of nested structured items,
    /// where each element is mapped by <paramref name="elementMapper"/>.
    /// The collection property is optional (missing member → <see langword="null"/>).
    /// </summary>
    /// <typeparam name="TElement">The element POCO type.</typeparam>
    /// <param name="key">The RFC 8941 dictionary key.</param>
    /// <param name="property">A property-access expression for the collection property.</param>
    /// <param name="elementMapper">A mapper that handles each element's item-level parse/serialize.</param>
    /// <returns>This builder for chaining.</returns>
    public DictionaryBuilder<T> InnerList<TElement>(
        string key,
        Expression<Func<T, IReadOnlyList<TElement>?>> property,
        StructuredFieldMapper<TElement> elementMapper)
        where TElement : new()
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(elementMapper);
        EnsureUniqueKey(key);

        var (getter, setter) = PropertyAccessor.Compile(property);
        var innerListConfig = new InnerListConfig(
            typeof(TElement),
            item => elementMapper.ParseItem(item)!,
            obj => elementMapper.SerializeItem((TElement)obj));

        _members.Add(new MemberMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (IReadOnlyList<TElement>?)v),
            ValueKind.Token, // kind unused for inner list
            isRequired: false,
            isToken: false,
            clrType: typeof(IReadOnlyList<TElement>),
            innerList: innerListConfig));

        return this;
    }

    private void EnsureUniqueKey(string key)
    {
        if (!TokenItem.IsValidKey(key))
        {
            throw new ArgumentException(
                $"Dictionary key '{key}' is not a valid RFC 8941 key. " +
                "Keys must start with a lowercase letter or '*' and contain only " +
                "lowercase letters, digits, '_', '-', '.', or '*'.",
                nameof(key));
        }

        if (!_keys.Add(key))
        {
            throw new ArgumentException($"A mapping for dictionary key '{key}' has already been registered.", nameof(key));
        }
    }

    private static bool IsNullable(Type t) =>
        !t.IsValueType || Nullable.GetUnderlyingType(t) != null;
}
