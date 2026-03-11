// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a decimal item in a structured field value.
/// RFC 8941 defines decimals with up to 12 significant digits and up to 3 decimal places.
/// </summary>
public sealed class DecimalItem : StructuredFieldItem
{
    /// <summary>
    /// The maximum number of significant digits allowed.
    /// </summary>
    public const int MaxSignificantDigits = 12;

    /// <summary>
    /// The maximum number of decimal places allowed.
    /// </summary>
    public const int MaxDecimalPlaces = 3;

    private readonly decimal _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecimalItem"/> class.
    /// </summary>
    /// <param name="value">The decimal value.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the value has too many significant digits or decimal places.
    /// </exception>
    public DecimalItem(decimal value)
    {
        ValidateDecimal(value);
        _value = value;
    }

    /// <summary>
    /// Gets the decimal value.
    /// </summary>
    public decimal DecimalValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Decimal;

    /// <inheritdoc/>
    public override string ToString() => _value.ToString("G");

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is DecimalItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from decimal to DecimalItem.
    /// </summary>
    public static implicit operator DecimalItem(decimal value) => new(value);

    /// <summary>
    /// Implicit conversion from DecimalItem to decimal.
    /// </summary>
    public static implicit operator decimal(DecimalItem item) => item._value;

    /// <summary>
    /// Implicit conversion from double to DecimalItem.
    /// </summary>
    public static implicit operator DecimalItem(double value) => new((decimal)value);

    private static void ValidateDecimal(decimal value)
    {
        // Get the string representation to count digits
        var valueStr = Math.Abs(value).ToString("G29");
        var parts = valueStr.Split('.');

        // Count integer digits (excluding leading zeros)
        var integerPart = parts[0].TrimStart('0');
        if (string.IsNullOrEmpty(integerPart))
        {
            integerPart = "0";
        }

        // Count decimal places
        var decimalPlaces = parts.Length > 1 ? parts[1].Length : 0;

        // Calculate total significant digits
        var totalDigits = integerPart.Length + decimalPlaces;

        if (totalDigits > MaxSignificantDigits)
        {
            throw new ArgumentException(
                $"Decimal value has {totalDigits} significant digits, but RFC 8941 allows maximum {MaxSignificantDigits}.",
                nameof(value));
        }

        if (decimalPlaces > MaxDecimalPlaces)
        {
            throw new ArgumentException(
                $"Decimal value has {decimalPlaces} decimal places, but RFC 8941 allows maximum {MaxDecimalPlaces}.",
                nameof(value));
        }
    }
}
