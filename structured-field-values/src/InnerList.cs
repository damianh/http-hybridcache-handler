// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents an inner list in a structured field value.
/// Inner lists are arrays of items with optional parameters.
/// RFC 8941 § 3.1.1
/// </summary>
public sealed class InnerList
{
    private readonly List<StructuredFieldItem> _items = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="InnerList"/> class.
    /// </summary>
    public InnerList() => Parameters = new Parameters();

    /// <summary>
    /// Initializes a new instance of the <see cref="InnerList"/> class with items.
    /// </summary>
    /// <param name="items">The items to include in the list.</param>
    public InnerList(IEnumerable<StructuredFieldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items.AddRange(items);
        Parameters = new Parameters();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InnerList"/> class with items and parameters.
    /// </summary>
    /// <param name="items">The items to include in the list.</param>
    /// <param name="parameters">The parameters to attach to the list.</param>
    public InnerList(IEnumerable<StructuredFieldItem> items, Parameters parameters)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(parameters);
        _items.AddRange(items);
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the parameters associated with this inner list.
    /// </summary>
    public Parameters Parameters { get; init; }

    /// <summary>
    /// Gets the items in this inner list.
    /// </summary>
    public IReadOnlyList<StructuredFieldItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The item at the specified index.</returns>
    public StructuredFieldItem this[int index] => _items[index];

    /// <summary>
    /// Adds an item to the inner list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    /// <summary>
    /// Adds multiple items to the inner list.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(IEnumerable<StructuredFieldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items.AddRange(items);
    }

    /// <summary>
    /// Removes all items from the inner list.
    /// </summary>
    public void Clear() => _items.Clear();

    /// <inheritdoc/>
    public override string ToString()
    {
        var itemsStr = string.Join(" ", _items.Select(i => i.ToString()));
        return Parameters.Count > 0
            ? $"({itemsStr}){FormatParameters()}"
            : $"({itemsStr})";
    }

    private string FormatParameters()
    {
        var parts = new List<string>();
        foreach (var (key, value) in Parameters)
        {
            parts.Add(value == null ? $";{key}" : $";{key}={value}");
        }
        return string.Join("", parts);
    }
}
