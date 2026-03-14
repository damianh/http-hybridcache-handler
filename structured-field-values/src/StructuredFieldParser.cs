// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Parses structured field values according to RFC 8941.
/// </summary>
public static class StructuredFieldParser
{
    /// <summary>
    /// Parses an item from the input string.
    /// RFC 8941 § 4.2.3
    /// </summary>
    /// <param name="input">The input string containing the item.</param>
    /// <returns>The parsed structured field item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="StructuredFieldParseException">Thrown when parsing fails.</exception>
    public static StructuredFieldItem ParseItem(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        var parser = new Parser(input.AsSpan());
        parser.ConsumeOptionalSpaces();
        
        var item = ParseBareItem(ref parser);
        var parameters = ParseParameters(ref parser);
        
        parser.ConsumeOptionalSpaces();
        
        if (!parser.IsAtEnd)
        {
            parser.ThrowParseException("Unexpected characters after item");
        }

        item.Parameters.Clear();
        foreach (var (key, value) in parameters)
        {
            item.Parameters.Add(key, value);
        }
        
        return item;
    }

    /// <summary>
    /// Parses a list from the input string.
    /// RFC 8941 § 4.2.1
    /// </summary>
    /// <param name="input">The input string containing the list.</param>
    /// <returns>The parsed structured field list.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="StructuredFieldParseException">Thrown when parsing fails.</exception>
    public static StructuredFieldList ParseList(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        var parser = new Parser(input.AsSpan());
        var list = new StructuredFieldList();
        
        parser.ConsumeOptionalWhitespace();
        
        if (parser.IsAtEnd)
        {
            return list;
        }

        while (true)
        {
            var member = ParseListMember(ref parser);
            list.Add(member);
            
            parser.ConsumeOptionalWhitespace();
            
            if (parser.IsAtEnd)
            {
                break;
            }
            
            if (!parser.TryConsume(','))
            {
                parser.ThrowParseException("Expected ',' between list members");
            }
            
            parser.ConsumeOptionalWhitespace();
            
            if (parser.IsAtEnd)
            {
                parser.ThrowParseException("Unexpected end of input after ','");
            }
        }
        
        return list;
    }

    /// <summary>
    /// Parses a dictionary from the input string.
    /// RFC 8941 § 4.2.2
    /// </summary>
    /// <param name="input">The input string containing the dictionary.</param>
    /// <returns>The parsed structured field dictionary.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="StructuredFieldParseException">Thrown when parsing fails.</exception>
    public static StructuredFieldDictionary ParseDictionary(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        var parser = new Parser(input.AsSpan());
        var dict = new StructuredFieldDictionary();
        
        parser.ConsumeOptionalWhitespace();
        
        if (parser.IsAtEnd)
        {
            return dict;
        }

        while (true)
        {
            var key = ParseKey(ref parser);
            
            DictionaryMember member;
            
            if (parser.TryConsume('='))
            {
                var listMember = ParseListMember(ref parser);

                member = listMember.IsItem
                    ? DictionaryMember.FromItem(listMember.Item)
                    : DictionaryMember.FromInnerList(listMember.InnerList);
            }
            else
            {
                // Boolean true with parameters
                var parameters = ParseParameters(ref parser);
                var item = new BooleanItem(true);
                foreach (var (paramKey, paramValue) in parameters)
                {
                    item.Parameters.Add(paramKey, paramValue);
                }
                member = DictionaryMember.FromItem(item);
            }
            
            // Use indexer instead of Add to allow duplicate keys (last one wins per RFC 8941)
            dict[key] = member;
            
            parser.ConsumeOptionalWhitespace();
            
            if (parser.IsAtEnd)
            {
                break;
            }
            
            if (!parser.TryConsume(','))
            {
                parser.ThrowParseException("Expected ',' between dictionary members");
            }
            
            parser.ConsumeOptionalWhitespace();
            
            if (parser.IsAtEnd)
            {
                parser.ThrowParseException("Unexpected end of input after ','");
            }
        }
        
        return dict;
    }

    /// <summary>
    /// Parses a list member (item or inner list).
    /// RFC 8941 § 4.2.1.1
    /// </summary>
    private static ListMember ParseListMember(ref Parser parser)
    {
        if (parser.Current == '(')
        {
            return ListMember.FromInnerList(ParseInnerList(ref parser));
        }
        else
        {
            var item = ParseBareItem(ref parser);
            var parameters = ParseParameters(ref parser);
            
            item.Parameters.Clear();
            foreach (var (key, value) in parameters)
            {
                item.Parameters.Add(key, value);
            }
            
            return ListMember.FromItem(item);
        }
    }

    /// <summary>
    /// Parses an inner list.
    /// RFC 8941 § 4.2.1.2
    /// </summary>
    private static InnerList ParseInnerList(ref Parser parser)
    {
        if (!parser.TryConsume('('))
        {
            parser.ThrowParseException("Expected '(' at start of inner list");
        }

        var items = new List<StructuredFieldItem>();
        
        while (true)
        {
            parser.ConsumeOptionalWhitespace();
            
            if (parser.Current == ')')
            {
                break;
            }
            
            var item = ParseBareItem(ref parser);
            var parameters = ParseParameters(ref parser);
            
            item.Parameters.Clear();
            foreach (var (key, value) in parameters)
            {
                item.Parameters.Add(key, value);
            }
            
            items.Add(item);
            
            if (parser.Current != ' ' && parser.Current != ')' && parser.Current != '\t')
            {
                parser.ThrowParseException("Expected whitespace or ')' after inner list item");
            }
        }
        
        if (!parser.TryConsume(')'))
        {
            parser.ThrowParseException("Expected ')' at end of inner list");
        }

        var innerList = new InnerList(items);
        var listParameters = ParseParameters(ref parser);
        
        foreach (var (key, value) in listParameters)
        {
            innerList.Parameters.Add(key, value);
        }
        
        return innerList;
    }

    /// <summary>
    /// Parses parameters.
    /// RFC 8941 § 4.2.3.2
    /// </summary>
    private static Parameters ParseParameters(ref Parser parser)
    {
        var parameters = new Parameters();
        
        while (parser.Current == ';')
        {
            parser.Advance(); // consume ';'
            parser.ConsumeOptionalWhitespace();
            
            var key = ParseKey(ref parser);
            
            StructuredFieldItem? value = null;
            
            if (parser.TryConsume('='))
            {
                value = ParseBareItem(ref parser);
            }
            
            parameters.Add(key, value);
        }
        
        return parameters;
    }

    /// <summary>
    /// Parses a bare item (without parameters).
    /// RFC 8941 § 4.2.3.1
    /// </summary>
    private static StructuredFieldItem ParseBareItem(ref Parser parser)
    {
        if (parser.IsAtEnd)
        {
            parser.ThrowParseException("Unexpected end of input when parsing item");
        }

        // Integer or Decimal
        if (parser.IsDigit() || parser.Current == '-')
        {
            return ParseNumber(ref parser);
        }
        
        // String
        if (parser.Current == '"')
        {
            return ParseString(ref parser);
        }
        
        // Token
        if (parser.IsAlpha() || parser.Current == '*')
        {
            return ParseToken(ref parser);
        }
        
        // Byte Sequence
        if (parser.Current == ':')
        {
            return ParseByteSequence(ref parser);
        }
        
        // Boolean
        if (parser.Current == '?')
        {
            return ParseBoolean(ref parser);
        }

        parser.ThrowParseException($"Unexpected character '{parser.Current}' when parsing item");
        return null!; // Never reached
    }

    /// <summary>
    /// Parses an integer or decimal.
    /// RFC 8941 § 4.2.4 and § 4.2.5
    /// </summary>
    private static StructuredFieldItem ParseNumber(ref Parser parser)
    {
        var startPos = parser.Position;
        var isNegative = false;
        
        if (parser.Current == '-')
        {
            isNegative = true;
            parser.Advance();
        }

        if (!parser.IsDigit())
        {
            parser.ThrowParseException("Expected digit after '-'");
        }

        var integerPart = 0L;
        var digitCount = 0;
        
        while (parser.IsDigit())
        {
            integerPart = integerPart * 10 + (parser.Current - '0');
            digitCount++;
            parser.Advance();
            
            if (digitCount > 15)
            {
                parser.ThrowParseException("Integer too large");
            }
        }

        // Check for decimal
        if (parser.Current == '.')
        {
            parser.Advance();
            
            if (!parser.IsDigit())
            {
                parser.ThrowParseException("Expected digit after '.'");
            }

            var decimalPart = 0L;
            var decimalDigits = 0;
            
            while (parser.IsDigit())
            {
                decimalPart = decimalPart * 10 + (parser.Current - '0');
                decimalDigits++;
                parser.Advance();
                
                if (decimalDigits > 3)
                {
                    parser.ThrowParseException("Too many decimal places (maximum 3)");
                }
            }

            // Construct decimal value
            var divisor = (decimal)Math.Pow(10, decimalDigits);
            var value = integerPart + (decimalPart / divisor);
            
            if (isNegative)
            {
                value = -value;
            }

            return new DecimalItem(value);
        }
        else
        {
            // Integer
            var value = isNegative ? -integerPart : integerPart;
            
            if (value is < IntegerItem.MinValue or > IntegerItem.MaxValue)
            {
                parser.ThrowParseException($"Integer value {value} out of range");
            }

            return new IntegerItem(value);
        }
    }

    /// <summary>
    /// Parses a string.
    /// RFC 8941 § 4.2.6
    /// </summary>
    private static StringItem ParseString(ref Parser parser)
    {
        if (!parser.TryConsume('"'))
        {
            parser.ThrowParseException("Expected '\"' at start of string");
        }

        var sb = new StringBuilder();
        
        while (true)
        {
            if (parser.IsAtEnd)
            {
                parser.ThrowParseException("Unterminated string");
            }

            if (parser.Current == '"')
            {
                parser.Advance();
                break;
            }

            if (parser.Current == '\\')
            {
                parser.Advance();
                
                if (parser.IsAtEnd)
                {
                    parser.ThrowParseException("Unterminated string escape");
                }

                if (parser.Current is '"' or '\\')
                {
                    sb.Append(parser.Current);
                    parser.Advance();
                }
                else
                {
                    parser.ThrowParseException($"Invalid escape sequence '\\{parser.Current}'");
                }
            }
            else if (parser.Current >= 0x20 && parser.Current <= 0x7E)
            {
                sb.Append(parser.Current);
                parser.Advance();
            }
            else
            {
                parser.ThrowParseException($"Invalid character in string: 0x{(int)parser.Current:X2}");
            }
        }
        
        return new StringItem(sb.ToString());
    }

    /// <summary>
    /// Parses a token.
    /// RFC 8941 § 4.2.7
    /// </summary>
    private static TokenItem ParseToken(ref Parser parser)
    {
        if (!parser.IsAlpha() && parser.Current != '*')
        {
            parser.ThrowParseException("Token must start with alpha or '*'");
        }

        var sb = new StringBuilder();
        sb.Append(parser.Current);
        parser.Advance();
        
        while (parser.IsTokenChar())
        {
            sb.Append(parser.Current);
            parser.Advance();
        }
        
        return new TokenItem(sb.ToString());
    }

    /// <summary>
    /// Parses a byte sequence.
    /// RFC 8941 § 4.2.8
    /// </summary>
    private static ByteSequenceItem ParseByteSequence(ref Parser parser)
    {
        if (!parser.TryConsume(':'))
        {
            parser.ThrowParseException("Expected ':' at start of byte sequence");
        }

        var sb = new StringBuilder();
        
        while (parser.Current != ':')
        {
            if (parser.IsAtEnd)
            {
                parser.ThrowParseException("Unterminated byte sequence");
            }

            // Base64 characters: A-Z, a-z, 0-9, +, /, =
            if ((parser.Current >= 'A' && parser.Current <= 'Z') ||
                (parser.Current >= 'a' && parser.Current <= 'z') ||
                (parser.Current >= '0' && parser.Current <= '9') ||
                parser.Current == '+' || parser.Current == '/' || parser.Current == '=')
            {
                sb.Append(parser.Current);
                parser.Advance();
            }
            else
            {
                parser.ThrowParseException($"Invalid character in byte sequence: '{parser.Current}'");
            }
        }

        if (!parser.TryConsume(':'))
        {
            parser.ThrowParseException("Expected ':' at end of byte sequence");
        }

        try
        {
            return ByteSequenceItem.FromBase64(sb.ToString());
        }
        catch (FormatException ex)
        {
            throw new StructuredFieldParseException("Invalid base64 in byte sequence", ex);
        }
    }

    /// <summary>
    /// Parses a boolean.
    /// RFC 8941 § 4.2.9
    /// </summary>
    private static BooleanItem ParseBoolean(ref Parser parser)
    {
        if (!parser.TryConsume('?'))
        {
            parser.ThrowParseException("Expected '?' at start of boolean");
        }

        if (parser.Current == '0')
        {
            parser.Advance();
            // Must not use BooleanItem.False singleton because parameters may be mutated after parsing.
            return new BooleanItem(false);
        }
        else if (parser.Current == '1')
        {
            parser.Advance();
            // Must not use BooleanItem.True singleton because parameters may be mutated after parsing.
            return new BooleanItem(true);
        }
        else
        {
            parser.ThrowParseException($"Expected '0' or '1' after '?', got '{parser.Current}'");
        }
        
        return null!; // Never reached
    }

    /// <summary>
    /// Parses a key (for parameters and dictionary keys).
    /// RFC 8941 § 4.2.3.3
    /// </summary>
    private static string ParseKey(ref Parser parser)
    {
        if (!parser.IsKeyChar() || parser.IsDigit())
        {
            parser.ThrowParseException("Key must start with lcalpha or '*'");
        }

        var sb = new StringBuilder();
        
        while (parser.IsKeyChar())
        {
            sb.Append(parser.Current);
            parser.Advance();
        }
        
        var key = sb.ToString();
        
        if (key.Length == 0)
        {
            parser.ThrowParseException("Empty key");
        }
        
        return key;
    }
}
