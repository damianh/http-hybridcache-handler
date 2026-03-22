// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a member of a structured field dictionary.
/// A dictionary member consists of an item or inner list with optional parameters.
/// RFC 8941 § 3.2
/// </summary>
public sealed class DictionaryMember
{
    private readonly StructuredFieldItem? _item;
    private readonly InnerList? _innerList;

    private DictionaryMember(StructuredFieldItem? item, InnerList? innerList, Parameters parameters)
    {
        if (item == null && innerList == null)
        {
            throw new ArgumentException("DictionaryMember must contain either an item or an inner list.");
        }
        
        if (item != null && innerList != null)
        {
            throw new ArgumentException("DictionaryMember cannot contain both an item and an inner list.");
        }

        _item = item;
        _innerList = innerList;
        Parameters = parameters ?? new Parameters();
    }

    /// <summary>
    /// Creates a DictionaryMember from an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <returns>A new DictionaryMember.</returns>
    public static DictionaryMember FromItem(StructuredFieldItem item, Parameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new DictionaryMember(item, null, parameters ?? new Parameters());
    }

    /// <summary>
    /// Creates a DictionaryMember from an inner list.
    /// </summary>
    /// <param name="innerList">The inner list.</param>
    /// <returns>A new DictionaryMember.</returns>
    public static DictionaryMember FromInnerList(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        return new DictionaryMember(null, innerList, new Parameters());
    }

    /// <summary>
    /// Gets a value indicating whether this member is an item.
    /// </summary>
    public bool IsItem => _item != null;

    /// <summary>
    /// Gets a value indicating whether this member is an inner list.
    /// </summary>
    public bool IsInnerList => _innerList != null;

    /// <summary>
    /// Gets the item value if this member is an item.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this member is not an item.</exception>
    public StructuredFieldItem Item => _item ?? throw new InvalidOperationException("This member is not an item.");

    /// <summary>
    /// Gets the inner list value if this member is an inner list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this member is not an inner list.</exception>
    public InnerList InnerList => _innerList ?? throw new InvalidOperationException("This member is not an inner list.");

    /// <summary>
    /// Gets the parameters associated with this member.
    /// </summary>
    public Parameters Parameters { get; }

    /// <summary>
    /// Tries to get the item value.
    /// </summary>
    /// <param name="item">The item if this member is an item.</param>
    /// <returns>True if this member is an item, false otherwise.</returns>
    public bool TryGetItem([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out StructuredFieldItem? item)
    {
        item = _item;
        return _item != null;
    }

    /// <summary>
    /// Tries to get the inner list value.
    /// </summary>
    /// <param name="innerList">The inner list if this member is an inner list.</param>
    /// <returns>True if this member is an inner list, false otherwise.</returns>
    public bool TryGetInnerList(out InnerList? innerList)
    {
        innerList = _innerList;
        return _innerList != null;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var value = IsItem ? Item.ToString()! : InnerList.ToString()!;
        if (Parameters.Count > 0)
        {
            var paramStr = string.Join("", Parameters.Select(p => 
                p.Value == null ? $";{p.Key}" : $";{p.Key}={p.Value}"));
            return $"{value}{paramStr}";
        }
        return value;
    }

    /// <summary>
    /// Implicit conversion from StructuredFieldItem to DictionaryMember.
    /// </summary>
    public static implicit operator DictionaryMember(StructuredFieldItem item) => FromItem(item);

    /// <summary>
    /// Implicit conversion from InnerList to DictionaryMember.
    /// </summary>
    public static implicit operator DictionaryMember(InnerList innerList) => FromInnerList(innerList);
}
