# DamianH.Http.StructuredFieldValues

RFC 8941/9651 parser, serializer, and POCO mapper for HTTP Structured Field Values.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
  - [Parsing](#parsing)
  - [Serializing](#serializing)
  - [POCO Mapping](#poco-mapping)
- [API Reference](#api-reference)
  - [StructuredFieldParser](#structuredfieldparser)
  - [StructuredFieldSerializer](#structuredfieldserializer)
  - [StructuredFieldMapper\<T\>](#structuredfieldmappert)
  - [Item Types](#item-types)
  - [Collection Types](#collection-types)
  - [DictionaryBuilder\<T\>](#dictionarybuildert)
  - [ListBuilder\<T\>](#listbuildert)
  - [ItemBuilder\<T\>](#itembuildert)
- [Type Mapping](#type-mapping)
- [Samples](#samples)

## Installation

```bash
dotnet add package DamianH.Http.StructuredFieldValues
```

## Quick Start

### Parsing

```csharp
using DamianH.Http.StructuredFieldValues;

// Parse a structured dictionary (e.g. Cache-Control, Priority)
StructuredFieldDictionary dict = StructuredFieldParser.ParseDictionary("u=3, i");
// dict["u"] → IntegerItem(3)
// dict["i"] → BooleanItem(true)  (bare key = ?1)

// Parse a structured list
StructuredFieldList list = StructuredFieldParser.ParseList("a, b, c");

// Parse a single item
StructuredFieldItem item = StructuredFieldParser.ParseItem("42");
```

### Serializing

```csharp
var dict = new StructuredFieldDictionary
{
    ["max-age"] = DictionaryMember.FromItem(new IntegerItem(3600)),
    ["no-cache"] = DictionaryMember.FromItem(BooleanItem.True)
};

string header = StructuredFieldSerializer.SerializeDictionary(dict);
// "max-age=3600, no-cache"
```

### POCO Mapping

The mapper converts RFC 8941 structured values to and from plain C# objects. Mappers are thread-safe and should be stored in `static readonly` fields.

```csharp
// Define a POCO
public class PriorityHeader
{
    public int? Urgency { get; init; }
    public bool? Incremental { get; init; }

    // Define the mapper once, store statically
    public static readonly StructuredFieldMapper<PriorityHeader> Mapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));
}

// Parse
var priority = PriorityHeader.Mapper.Parse("u=3, i");
// priority.Urgency == 3, priority.Incremental == true

// Serialize
string header = PriorityHeader.Mapper.Serialize(new PriorityHeader { Urgency = 3, Incremental = true });
// "u=3, i"

// Try-parse (returns false for malformed input instead of throwing)
if (PriorityHeader.Mapper.TryParse(request.Headers["Priority"], out var p))
{
    // use p
}
```

## API Reference

### StructuredFieldParser

Static class for parsing RFC 8941 structured field values.

| Method | Returns | Description |
|--------|---------|-------------|
| `ParseItem(string input)` | `StructuredFieldItem` | Parses a bare item with optional parameters (RFC 8941 §4.2.3) |
| `ParseList(string input)` | `StructuredFieldList` | Parses a list of items and/or inner lists (RFC 8941 §4.2.1) |
| `ParseDictionary(string input)` | `StructuredFieldDictionary` | Parses a dictionary of key→member pairs (RFC 8941 §4.2.2) |

All methods throw `ArgumentNullException` for null input and `StructuredFieldParseException` for malformed input.

### StructuredFieldSerializer

Static class for serializing RFC 8941 structured field values to their canonical string form.

| Method | Returns | Description |
|--------|---------|-------------|
| `SerializeItem(StructuredFieldItem item)` | `string` | Serializes an item with parameters (RFC 8941 §4.1.3) |
| `SerializeList(StructuredFieldList list)` | `string` | Serializes a list (RFC 8941 §4.1.1) |
| `SerializeDictionary(StructuredFieldDictionary dictionary)` | `string` | Serializes a dictionary (RFC 8941 §4.1.2) |

### StructuredFieldMapper\<T\>

A cached, reusable mapper that converts an RFC 8941 structured field value to and from a POCO of type `T`. `T` must have a public parameterless constructor.

**Factory methods** (choose based on the RFC 8941 field type):

| Factory | When to use |
|---------|------------|
| `StructuredFieldMapper<T>.Dictionary(Action<DictionaryBuilder<T>> configure)` | Field is an RFC 8941 Dictionary (e.g. `Cache-Control`, `Priority`) |
| `StructuredFieldMapper<T>.List(Action<ListBuilder<T>> configure)` | Field is an RFC 8941 List |
| `StructuredFieldMapper<T>.Item(Action<ItemBuilder<T>> configure)` | Field is an RFC 8941 Item |

**Instance methods:**

| Method | Description |
|--------|-------------|
| `T Parse(string input)` | Parses the header value. Throws `StructuredFieldParseException` on failure. |
| `bool TryParse(string? input, out T? result)` | Returns false instead of throwing for missing or malformed input. |
| `string Serialize(T value)` | Serializes the POCO to its canonical RFC 8941 string. |

### Item Types

All item types extend `StructuredFieldItem` and carry a `Parameters` collection.

| Type | CLR value | RFC 8941 wire format | Example |
|------|-----------|---------------------|---------|
| `IntegerItem` | `long` via `.LongValue` | Signed integer | `42`, `-1` |
| `DecimalItem` | `decimal` via `.DecimalValue` | Decimal (up to 3 d.p.) | `3.14` |
| `StringItem` | `string` via `.StringValue` | Quoted string | `"hello"` |
| `TokenItem` | `string` via `.TokenValue` | Unquoted token | `gzip`, `*` |
| `ByteSequenceItem` | `byte[]` via `.ByteValue` | `:base64:` | `:aGVsbG8=:` |
| `BooleanItem` | `bool` via `.BooleanValue` | `?0` / `?1` | `?1` |

### Collection Types

| Type | Description |
|------|-------------|
| `StructuredFieldList` | Ordered list of `ListMember` (item or inner list). Supports `Add`, `AddRange`, count, and indexer access. |
| `StructuredFieldDictionary` | Ordered dictionary of `string` → `DictionaryMember`. Implements `IEnumerable<KeyValuePair<string, DictionaryMember>>` and supports indexer by key. |
| `InnerList` | A parenthesised list of `StructuredFieldItem` entries, with its own `Parameters`. |
| `Parameters` | An ordered list of key → optional `StructuredFieldItem?` pairs attached to items or inner lists. |

### DictionaryBuilder\<T\>

Configures mappings from an RFC 8941 Dictionary to POCO properties.

| Method | Description |
|--------|-------------|
| `.Member(key, x => x.Prop)` | Maps a key to a primitive property. Type inferred from CLR type (see [Type Mapping](#type-mapping)). Nullable → optional; non-nullable → required. |
| `.TokenMember(key, x => x.Prop)` | Maps a key to a `string?` property serialized as an RFC 8941 Token. Always optional. |
| `.InnerList(key, x => x.Prop)` | Maps a key to an `IReadOnlyList<TElement>?` property (primitive elements). |
| `.TokenInnerList(key, x => x.Prop)` | Maps a key to an `IReadOnlyList<string>?` property where strings are serialized as tokens. |
| `.InnerList(key, x => x.Prop, elementMapper)` | Maps a key to an `IReadOnlyList<TElement>?` property where each element is mapped by a nested `StructuredFieldMapper<TElement>`. |

### ListBuilder\<T\>

Configures mappings from an RFC 8941 List to a POCO.

| Method | Description |
|--------|-------------|
| `.Elements(x => x.Prop)` | Maps list elements to an `IReadOnlyList<TElement>?` property. |
| `.TokenElements(x => x.Prop)` | Maps list elements to an `IReadOnlyList<string>?` property where strings are tokens. |

### ItemBuilder\<T\>

Configures mappings from an RFC 8941 Item to a POCO.

| Method | Description |
|--------|-------------|
| `.Value(x => x.Prop)` | Maps the bare item value to a POCO property. |
| `.TokenValue(x => x.Prop)` | Maps the bare item value to a `string` property serialized as a token. |
| `.Parameter(paramKey, x => x.Prop)` | Maps a parameter to a POCO property. |
| `.TokenParameter(paramKey, x => x.Prop)` | Maps a parameter to a `string` property serialized as a token. |

## Type Mapping

The mapper infers RFC 8941 types from CLR property types:

| CLR Type | RFC 8941 Type | Notes |
|----------|--------------|-------|
| `int`, `long` | Integer | Range: −999,999,999,999,999 to 999,999,999,999,999 |
| `decimal` | Decimal | Up to 3 decimal places |
| `bool` | Boolean | `?1` / `?0`; bare key in dictionaries = `?1` |
| `string` | String | Quoted. Use `TokenMember`/`TokenValue` for token semantics |
| `byte[]` | Byte Sequence | `:base64:` encoding |
| Nullable (`int?`, `bool?`, …) | Optional | Missing member/parameter → property left at `default` |
| Non-nullable (`int`, `bool`, …) | Required | Missing member/parameter → `StructuredFieldParseException` |

## Samples

- [`samples/HttpClientSample`](../../samples/HttpClientSample) — `HttpClient` integration showing mapper definitions for `Priority` (RFC 9218) and `Cache-Control` headers, with helpers for reading and writing structured headers on `HttpRequestMessage` and `HttpResponseMessage`.

- [`samples/AspNetCoreSample`](../../samples/AspNetCoreSample) — ASP.NET Core integration showing the same mapper pattern applied to `HttpRequest` and `HttpResponse` extension methods.

> **Note**: The sample projects target .NET 10 with `LangVersion=preview` and use C# 14 extension declaration syntax.
