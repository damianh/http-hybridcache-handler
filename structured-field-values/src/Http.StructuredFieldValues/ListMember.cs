// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a member of a structured field list.
/// A list member can be either an item or an inner list.
/// RFC 8941 § 3.1
/// </summary>
public sealed class ListMember
{
    private readonly StructuredFieldItem? _item;
    private readonly InnerList? _innerList;

    private ListMember(StructuredFieldItem? item, InnerList? innerList)
    {
        if (item == null && innerList == null)
        {
            throw new ArgumentException("ListMember must contain either an item or an inner list.");
        }
        
        if (item != null && innerList != null)
        {
            throw new ArgumentException("ListMember cannot contain both an item and an inner list.");
        }

        _item = item;
        _innerList = innerList;
    }

    /// <summary>
    /// Creates a ListMember from an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>A new ListMember.</returns>
    public static ListMember FromItem(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ListMember(item, null);
    }

    /// <summary>
    /// Creates a ListMember from an inner list.
    /// </summary>
    /// <param name="innerList">The inner list.</param>
    /// <returns>A new ListMember.</returns>
    public static ListMember FromInnerList(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        return new ListMember(null, innerList);
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
    public override string ToString() => IsItem ? Item.ToString()! : InnerList.ToString()!;

    /// <summary>
    /// Implicit conversion from StructuredFieldItem to ListMember.
    /// </summary>
    public static implicit operator ListMember(StructuredFieldItem item) => FromItem(item);

    /// <summary>
    /// Implicit conversion from InnerList to ListMember.
    /// </summary>
    public static implicit operator ListMember(InnerList innerList) => FromInnerList(innerList);
}
