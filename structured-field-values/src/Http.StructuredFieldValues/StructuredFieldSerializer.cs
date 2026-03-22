// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Serializes structured field values to RFC 8941 canonical format.
/// </summary>
public static class StructuredFieldSerializer
{
    /// <summary>
    /// Serializes an item to its RFC 8941 canonical representation.
    /// RFC 8941 § 4.1.3
    /// </summary>
    /// <param name="item">The item to serialize.</param>
    /// <returns>The serialized string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when item is null.</exception>
    public static string SerializeItem(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sb = new StringBuilder();
        SerializeBareItem(item, sb);
        SerializeParameters(item.Parameters, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Serializes a list to its RFC 8941 canonical representation.
    /// RFC 8941 § 4.1.1
    /// </summary>
    /// <param name="list">The list to serialize.</param>
    /// <returns>The serialized string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when list is null.</exception>
    public static string SerializeList(StructuredFieldList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (list.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            SerializeListMember(list[i], sb);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serializes a dictionary to its RFC 8941 canonical representation.
    /// RFC 8941 § 4.1.2
    /// </summary>
    /// <param name="dictionary">The dictionary to serialize.</param>
    /// <returns>The serialized string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary is null.</exception>
    public static string SerializeDictionary(StructuredFieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (dictionary.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var first = true;

        foreach (var (key, member) in dictionary)
        {
            if (!first)
            {
                sb.Append(", ");
            }
            first = false;

            sb.Append(key);

            // RFC 8941: If the value is Boolean true, serialize just the key
            if (member.IsItem && member.Item is BooleanItem boolItem && boolItem.BooleanValue)
            {
                SerializeParameters(member.Item.Parameters, sb);
            }
            else
            {
                sb.Append('=');
                SerializeListMember(member.IsItem ? ListMember.FromItem(member.Item) : ListMember.FromInnerList(member.InnerList), sb);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serializes a list member (item or inner list).
    /// RFC 8941 § 4.1.1.1
    /// </summary>
    private static void SerializeListMember(ListMember member, StringBuilder sb)
    {
        if (member.IsItem)
        {
            SerializeBareItem(member.Item, sb);
            SerializeParameters(member.Item.Parameters, sb);
        }
        else
        {
            SerializeInnerList(member.InnerList, sb);
        }
    }

    /// <summary>
    /// Serializes an inner list.
    /// RFC 8941 § 4.1.1.2
    /// </summary>
    private static void SerializeInnerList(InnerList innerList, StringBuilder sb)
    {
        sb.Append('(');

        for (int i = 0; i < innerList.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            SerializeBareItem(innerList[i], sb);
            SerializeParameters(innerList[i].Parameters, sb);
        }

        sb.Append(')');
        SerializeParameters(innerList.Parameters, sb);
    }

    /// <summary>
    /// Serializes parameters.
    /// RFC 8941 § 4.1.1.3
    /// </summary>
    private static void SerializeParameters(Parameters parameters, StringBuilder sb)
    {
        foreach (var (key, value) in parameters)
        {
            sb.Append(';');
            sb.Append(key);

            if (value != null)
            {
                sb.Append('=');
                SerializeBareItem(value, sb);
            }
        }
    }

    /// <summary>
    /// Serializes a bare item (without parameters).
    /// RFC 8941 § 4.1.3.1
    /// </summary>
    private static void SerializeBareItem(StructuredFieldItem item, StringBuilder sb)
    {
        switch (item)
        {
            case IntegerItem intItem:
                SerializeInteger(intItem.LongValue, sb);
                break;

            case DecimalItem decItem:
                SerializeDecimal(decItem.DecimalValue, sb);
                break;

            case StringItem strItem:
                SerializeString(strItem.StringValue, sb);
                break;

            case TokenItem tokItem:
                SerializeToken(tokItem.TokenValue, sb);
                break;

            case ByteSequenceItem byteItem:
                SerializeByteSequence(byteItem, sb);
                break;

            case BooleanItem boolItem:
                SerializeBoolean(boolItem.BooleanValue, sb);
                break;

            default:
                throw new InvalidOperationException($"Unknown item type: {item.GetType().Name}");
        }
    }

    /// <summary>
    /// Serializes an integer.
    /// RFC 8941 § 4.1.4
    /// </summary>
    private static void SerializeInteger(long value, StringBuilder sb) => sb.Append(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Serializes a decimal.
    /// RFC 8941 § 4.1.5
    /// </summary>
    private static void SerializeDecimal(decimal value, StringBuilder sb)
    {
        // RFC 8941: Decimals must have at least one digit before and after decimal point
        // and use at most 3 decimal places

        var integerPart = Math.Truncate(Math.Abs(value));
        var decimalPart = Math.Abs(value) - integerPart;

        // Round to 3 decimal places
        decimalPart = Math.Round(decimalPart, 3, MidpointRounding.ToEven);

        if (value < 0)
        {
            sb.Append('-');
        }

        sb.Append(((long)integerPart).ToString(CultureInfo.InvariantCulture));
        sb.Append('.');

        // Format decimal part with exactly 3 digits, removing trailing zeros
        var decimalStr = ((long)(decimalPart * 1000)).ToString("D3", CultureInfo.InvariantCulture);

        // Remove trailing zeros but keep at least one digit
        var trimmedLength = decimalStr.Length;
        while (trimmedLength > 1 && decimalStr[trimmedLength - 1] == '0')
        {
            trimmedLength--;
        }

        sb.Append(decimalStr, 0, trimmedLength);
    }

    /// <summary>
    /// Serializes a string.
    /// RFC 8941 § 4.1.6
    /// </summary>
    private static void SerializeString(string value, StringBuilder sb)
    {
        sb.Append('"');

        foreach (var c in value)
        {
            // Escape backslash and double quote
            if (c is '\\' or '"')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        sb.Append('"');
    }

    /// <summary>
    /// Serializes a token.
    /// RFC 8941 § 4.1.7
    /// </summary>
    private static void SerializeToken(string value, StringBuilder sb) =>
        // RFC 8941: Tokens are serialized as-is (already validated during construction)
        sb.Append(value);

    /// <summary>
    /// Serializes a byte sequence.
    /// RFC 8941 § 4.1.8
    /// </summary>
    private static void SerializeByteSequence(ByteSequenceItem item, StringBuilder sb)
    {
        sb.Append(':');
        sb.Append(item.Base64Value);
        sb.Append(':');
    }

    /// <summary>
    /// Serializes a boolean.
    /// RFC 8941 § 4.1.9
    /// </summary>
    private static void SerializeBoolean(bool value, StringBuilder sb) => sb.Append(value ? "?1" : "?0");
}
