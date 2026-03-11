// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Fluent builder that describes how to map an RFC 8941 structured field item
/// to and from a POCO of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The POCO type. Must have a public parameterless constructor.</typeparam>
public sealed class ItemBuilder<T> where T : new()
{
    private ValueMapping<T>? _valueMapping;
    private readonly List<ParameterMapping<T>> _parameters = [];
    private readonly HashSet<string> _parameterKeys = [];

    internal ValueMapping<T>? ValueMapping => _valueMapping;
    internal IReadOnlyList<ParameterMapping<T>> Parameters => _parameters;

    /// <summary>
    /// Maps the bare item value to a POCO property.
    /// The RFC 8941 type is inferred from the property's CLR type:
    /// <c>int</c>/<c>long</c> → Integer, <c>decimal</c> → Decimal,
    /// <c>bool</c> → Boolean, <c>string</c> → String, <c>byte[]</c> → ByteSequence.
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="property">A property-access expression (e.g. <c>x => x.Encoding</c>).</param>
    /// <returns>This builder for chaining.</returns>
    public ItemBuilder<T> Value<TValue>(Expression<Func<T, TValue>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_valueMapping != null)
            throw new InvalidOperationException("A value mapping has already been registered for this item.");

        var prop = PropertyAccessor.GetProperty(property);
        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TValue));
        var isRequired = !IsNullable(typeof(TValue));

        _valueMapping = new ValueMapping<T>(
            v => getter(v),
            (inst, v) => setter(inst, (TValue)v!),
            kind,
            isRequired,
            prop.PropertyType);

        return this;
    }

    /// <summary>
    /// Maps the bare item value to a <see cref="string"/> property, serialized as an RFC 8941 Token.
    /// </summary>
    /// <param name="property">A property-access expression (e.g. <c>x => x.Encoding</c>).</param>
    /// <returns>This builder for chaining.</returns>
    public ItemBuilder<T> TokenValue(Expression<Func<T, string>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_valueMapping != null)
            throw new InvalidOperationException("A value mapping has already been registered for this item.");

        var prop = PropertyAccessor.GetProperty(property);
        var (getter, setter) = PropertyAccessor.Compile(property);

        _valueMapping = new ValueMapping<T>(
            v => getter(v),
            (inst, v) => setter(inst, (string)v!),
            ValueKind.Token,
            isRequired: true,
            prop.PropertyType);

        return this;
    }

    /// <summary>
    /// Maps an RFC 8941 item parameter to a POCO property.
    /// The RFC 8941 type is inferred from the property's CLR type.
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="key">The parameter key (must be a valid RFC 8941 token).</param>
    /// <param name="property">A property-access expression.</param>
    /// <returns>This builder for chaining.</returns>
    public ItemBuilder<T> Parameter<TValue>(string key, Expression<Func<T, TValue>> property)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        ValidateParameterKey(key);

        if (!_parameterKeys.Add(key))
            throw new ArgumentException($"A parameter mapping for key '{key}' has already been registered.", nameof(key));

        var prop = PropertyAccessor.GetProperty(property);
        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TValue));
        var isRequired = !IsNullable(typeof(TValue));

        _parameters.Add(new ParameterMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (TValue)v!),
            kind,
            isRequired,
            prop.PropertyType));

        return this;
    }

    /// <summary>
    /// Maps an RFC 8941 item parameter to a <see cref="string"/> property, treated as a Token.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <param name="property">A property-access expression.</param>
    /// <returns>This builder for chaining.</returns>
    public ItemBuilder<T> TokenParameter(string key, Expression<Func<T, string?>> property)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        ValidateParameterKey(key);

        if (!_parameterKeys.Add(key))
            throw new ArgumentException($"A parameter mapping for key '{key}' has already been registered.", nameof(key));

        var prop = PropertyAccessor.GetProperty(property);
        var (getter, setter) = PropertyAccessor.Compile(property);

        _parameters.Add(new ParameterMapping<T>(
            key,
            v => getter(v),
            (inst, v) => setter(inst, (string?)v),
            ValueKind.Token,
            isRequired: false,
            prop.PropertyType));

        return this;
    }

    private static bool IsNullable(Type t) =>
        !t.IsValueType || Nullable.GetUnderlyingType(t) != null;

    private static void ValidateParameterKey(string key)
    {
        if (!TokenItem.IsValidKey(key))
            throw new ArgumentException(
                $"Parameter key '{key}' is not a valid RFC 8941 key. " +
                "Keys must start with a lowercase letter or '*' and contain only " +
                "lowercase letters, digits, '_', '-', '.', or '*'.",
                nameof(key));
    }
}
