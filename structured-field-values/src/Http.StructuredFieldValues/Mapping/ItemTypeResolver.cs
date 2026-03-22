// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Maps CLR types to <see cref="ValueKind"/> and provides helpers to construct
/// and extract values from <see cref="StructuredFieldItem"/> instances.
/// </summary>
internal static class ItemTypeResolver
{
    /// <summary>
    /// Returns the <see cref="ValueKind"/> that corresponds to the given CLR type.
    /// Nullable value types are unwrapped before mapping.
    /// </summary>
    /// <param name="clrType">The CLR type to resolve.</param>
    /// <returns>The matching <see cref="ValueKind"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="clrType"/> has no RFC 8941 mapping.
    /// </exception>
    internal static ValueKind Resolve(Type clrType)
    {
        var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (underlying == typeof(int) || underlying == typeof(long))
            return ValueKind.Integer;
        if (underlying == typeof(decimal))
            return ValueKind.Decimal;
        if (underlying == typeof(bool))
            return ValueKind.Boolean;
        if (underlying == typeof(string))
            return ValueKind.String;
        if (underlying == typeof(byte[]))
            return ValueKind.ByteSequence;

        throw new NotSupportedException(
            $"CLR type '{clrType.Name}' has no RFC 8941 mapping. " +
            "Supported types: int, long, decimal, bool, string, byte[]. " +
            "Use TokenMember/TokenValue/TokenParameter for token-typed strings.");
    }

    /// <summary>
    /// Constructs a <see cref="StructuredFieldItem"/> from a CLR value using the given <see cref="ValueKind"/>.
    /// Returns <see langword="null"/> when <paramref name="value"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="kind">The target RFC 8941 type.</param>
    /// <param name="value">The CLR value to wrap.</param>
    /// <returns>A new <see cref="StructuredFieldItem"/>, or <see langword="null"/>.</returns>
    internal static StructuredFieldItem? ToItem(ValueKind kind, object? value)
    {
        if (value is null)
            return null;

        return kind switch
        {
            ValueKind.Integer => value is int i ? new IntegerItem(i) : new IntegerItem((long)value),
            ValueKind.Decimal => new DecimalItem((decimal)value),
            ValueKind.Boolean => new BooleanItem((bool)value),
            ValueKind.String  => new StringItem((string)value),
            ValueKind.Token   => new TokenItem((string)value),
            ValueKind.ByteSequence => new ByteSequenceItem((byte[])value),
            _ => throw new NotSupportedException($"Unsupported ValueKind: {kind}"),
        };
    }

    /// <summary>
    /// Extracts the CLR value from a <see cref="StructuredFieldItem"/> using the given <see cref="ValueKind"/>
    /// and target CLR type. Performs int32 narrowing when <paramref name="targetType"/> is <see cref="int"/> or
    /// <see cref="Nullable{T}"/> of <see cref="int"/>.
    /// </summary>
    /// <param name="kind">The expected RFC 8941 type.</param>
    /// <param name="item">The item to extract from.</param>
    /// <param name="targetType">The CLR type expected by the POCO property.</param>
    /// <param name="context">A human-readable description used in error messages (e.g., "dictionary member 'u'").</param>
    /// <returns>The extracted CLR value.</returns>
    /// <exception cref="StructuredFieldParseException">
    /// Thrown when <paramref name="item"/> is not the expected type.
    /// </exception>
    internal static object ExtractValue(ValueKind kind, StructuredFieldItem item, Type targetType, string context)
    {
        switch (kind)
        {
            case ValueKind.Integer:
                if (item is not IntegerItem intItem)
                    throw new StructuredFieldParseException(
                        $"Expected an Integer for {context}, but found {item.Type}.");
                var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (underlying == typeof(int))
                {
                    if (intItem.LongValue < int.MinValue || intItem.LongValue > int.MaxValue)
                        throw new StructuredFieldParseException(
                            $"Integer value {intItem.LongValue} for {context} overflows Int32 (range {int.MinValue}..{int.MaxValue}).");
                    return (object)(int)intItem.LongValue;
                }
                return intItem.LongValue;

            case ValueKind.Decimal:
                if (item is not DecimalItem decItem)
                    throw new StructuredFieldParseException(
                        $"Expected a Decimal for {context}, but found {item.Type}.");
                return decItem.DecimalValue;

            case ValueKind.Boolean:
                if (item is not BooleanItem boolItem)
                    throw new StructuredFieldParseException(
                        $"Expected a Boolean for {context}, but found {item.Type}.");
                return boolItem.BooleanValue;

            case ValueKind.String:
                if (item is not StringItem strItem)
                    throw new StructuredFieldParseException(
                        $"Expected a String for {context}, but found {item.Type}.");
                return strItem.StringValue;

            case ValueKind.Token:
                if (item is not TokenItem tokenItem)
                    throw new StructuredFieldParseException(
                        $"Expected a Token for {context}, but found {item.Type}.");
                return tokenItem.TokenValue;

            case ValueKind.ByteSequence:
                if (item is not ByteSequenceItem byteItem)
                    throw new StructuredFieldParseException(
                        $"Expected a ByteSequence for {context}, but found {item.Type}.");
                return byteItem.ByteArrayValue;

            default:
                throw new NotSupportedException($"Unsupported ValueKind: {kind}");
        }
    }
}
