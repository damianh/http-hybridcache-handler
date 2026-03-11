// Copyright (c) Duenne Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DamianH.Http.StructuredFieldValues.Mapping;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// A cached, reusable mapper that converts an RFC 8941 structured field value
/// to and from a POCO of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">
/// The POCO type. Must have a public parameterless constructor.
/// </typeparam>
/// <remarks>
/// <para>
/// Instances are thread-safe and should be stored in static fields for reuse.
/// </para>
/// <example>
/// <code>
/// static readonly StructuredFieldMapper&lt;PriorityHeader&gt; PriorityMapper =
///     StructuredFieldMapper&lt;PriorityHeader&gt;.Dictionary(b => b
///         .Member("u", x => x.Urgency)
///         .Member("i", x => x.Incremental));
///
/// var header = PriorityMapper.Parse("u=3, i");
/// var serialized = PriorityMapper.Serialize(header); // "u=3, i"
/// </code>
/// </example>
/// </remarks>
public sealed class StructuredFieldMapper<T> where T : new()
{
    private readonly Func<string, T> _parse;
    private readonly Func<string?, bool, (bool ok, T? result)> _tryParse;
    private readonly Func<T, string> _serialize;

    // For item-level use by nested mappers (Tasks 6-7)
    internal Func<StructuredFieldItem, T>? ItemParseDelegate { get; }
    internal Func<T, StructuredFieldItem>? ItemSerializeDelegate { get; }

    private StructuredFieldMapper(
        Func<string, T> parse,
        Func<string?, bool, (bool ok, T? result)> tryParse,
        Func<T, string> serialize,
        Func<StructuredFieldItem, T>? itemParse = null,
        Func<T, StructuredFieldItem>? itemSerialize = null)
    {
        _parse = parse;
        _tryParse = tryParse;
        _serialize = serialize;
        ItemParseDelegate = itemParse;
        ItemSerializeDelegate = itemSerialize;
    }

    // -------------------------------------------------------------------------
    // Static factory methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a mapper for an RFC 8941 Dictionary field.
    /// </summary>
    /// <param name="configure">A callback that configures the field mappings.</param>
    /// <returns>A cached, reusable <see cref="StructuredFieldMapper{T}"/>.</returns>
    public static StructuredFieldMapper<T> Dictionary(Action<DictionaryBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new DictionaryBuilder<T>();
        configure(builder);

        var parseDelegate = DictionaryMapperFactory.BuildParseDelegate(builder);
        var serializeDelegate = DictionaryMapperFactory.BuildSerializeDelegate(builder);

        return new StructuredFieldMapper<T>(
            input => parseDelegate(StructuredFieldParser.ParseDictionary(input)),
            (input, _) =>
            {
                if (string.IsNullOrEmpty(input)) return (false, default);
                try { return (true, parseDelegate(StructuredFieldParser.ParseDictionary(input))); }
                catch (StructuredFieldParseException) { return (false, default); }
            },
            value => StructuredFieldSerializer.SerializeDictionary(serializeDelegate(value)));
    }

    /// <summary>
    /// Creates a mapper for an RFC 8941 List field.
    /// </summary>
    /// <param name="configure">A callback that configures the field mappings.</param>
    /// <returns>A cached, reusable <see cref="StructuredFieldMapper{T}"/>.</returns>
    public static StructuredFieldMapper<T> List(Action<ListBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ListBuilder<T>();
        configure(builder);

        var parseDelegate = ListMapperFactory.BuildParseDelegate(builder);
        var serializeDelegate = ListMapperFactory.BuildSerializeDelegate(builder);

        return new StructuredFieldMapper<T>(
            input => parseDelegate(StructuredFieldParser.ParseList(input)),
            (input, _) =>
            {
                if (string.IsNullOrEmpty(input)) return (false, default);
                try { return (true, parseDelegate(StructuredFieldParser.ParseList(input))); }
                catch (StructuredFieldParseException) { return (false, default); }
            },
            value => StructuredFieldSerializer.SerializeList(serializeDelegate(value)));
    }

    /// <summary>
    /// Creates a mapper for an RFC 8941 Item field.
    /// </summary>
    /// <param name="configure">A callback that configures the field mappings.</param>
    /// <returns>A cached, reusable <see cref="StructuredFieldMapper{T}"/>.</returns>
    public static StructuredFieldMapper<T> Item(Action<ItemBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ItemBuilder<T>();
        configure(builder);

        var itemParseDelegate = ItemMapperFactory.BuildParseDelegate(builder);
        var itemSerializeDelegate = ItemMapperFactory.BuildSerializeDelegate(builder);

        return new StructuredFieldMapper<T>(
            input => itemParseDelegate(StructuredFieldParser.ParseItem(input)),
            (input, _) =>
            {
                if (string.IsNullOrEmpty(input)) return (false, default);
                try { return (true, itemParseDelegate(StructuredFieldParser.ParseItem(input))); }
                catch (StructuredFieldParseException) { return (false, default); }
            },
            value => StructuredFieldSerializer.SerializeItem(itemSerializeDelegate(value)),
            itemParseDelegate,
            itemSerializeDelegate);
    }

    // -------------------------------------------------------------------------
    // Instance methods (parse / serialize)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses an RFC 8941 structured field value from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The raw header value string.</param>
    /// <returns>The populated POCO.</returns>
    /// <exception cref="StructuredFieldParseException">
    /// Thrown when the input is malformed or required members/parameters are missing.
    /// </exception>
    public T Parse(string input) => _parse(input);

    /// <summary>
    /// Attempts to parse an RFC 8941 structured field value.
    /// </summary>
    /// <param name="input">The raw header value string, or <see langword="null"/>.</param>
    /// <param name="result">The populated POCO if parsing succeeded; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
    public bool TryParse(string? input, [NotNullWhen(true)] out T? result)
    {
        var (ok, value) = _tryParse(input, false);
        result = value;
        return ok;
    }

    /// <summary>
    /// Serializes a POCO to its RFC 8941 structured field string representation.
    /// </summary>
    /// <param name="value">The POCO to serialize.</param>
    /// <returns>The canonical RFC 8941 string.</returns>
    public string Serialize(T value) => _serialize(value);

    // -------------------------------------------------------------------------
    // Internal helpers for nested item mappers (used by DictionaryMapperFactory / ListMapperFactory)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a <see cref="StructuredFieldItem"/> AST node directly (used for nested items in inner lists).
    /// Only valid for mappers created via <see cref="Item"/>.
    /// </summary>
    internal T? ParseItem(StructuredFieldItem item) =>
        ItemParseDelegate != null ? ItemParseDelegate(item) : default;

    /// <summary>
    /// Serializes a POCO to a <see cref="StructuredFieldItem"/> AST node directly.
    /// Only valid for mappers created via <see cref="Item"/>.
    /// </summary>
    internal StructuredFieldItem SerializeItem(T value) =>
        ItemSerializeDelegate != null
            ? ItemSerializeDelegate(value)
            : throw new InvalidOperationException(
                "This mapper was not created via Item() and cannot serialize to a StructuredFieldItem.");
}
