// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a structured field list.
/// Lists are ordered sequences of items or inner lists.
/// RFC 8941 § 3.1
/// </summary>
public sealed class StructuredFieldList
{
    private readonly List<ListMember> _members = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldList"/> class.
    /// </summary>
    public StructuredFieldList()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldList"/> class with members.
    /// </summary>
    /// <param name="members">The members to include in the list.</param>
    public StructuredFieldList(IEnumerable<ListMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        _members.AddRange(members);
    }

    /// <summary>
    /// Gets the members of this list.
    /// </summary>
    public IReadOnlyList<ListMember> Members => _members.AsReadOnly();

    /// <summary>
    /// Gets the number of members in the list.
    /// </summary>
    public int Count => _members.Count;

    /// <summary>
    /// Gets the member at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The member at the specified index.</returns>
    public ListMember this[int index] => _members[index];

    /// <summary>
    /// Adds a member to the list.
    /// </summary>
    /// <param name="member">The member to add.</param>
    public void Add(ListMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _members.Add(member);
    }

    /// <summary>
    /// Adds an item to the list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _members.Add(ListMember.FromItem(item));
    }

    /// <summary>
    /// Adds an inner list to the list.
    /// </summary>
    /// <param name="innerList">The inner list to add.</param>
    public void Add(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        _members.Add(ListMember.FromInnerList(innerList));
    }

    /// <summary>
    /// Adds multiple members to the list.
    /// </summary>
    /// <param name="members">The members to add.</param>
    public void AddRange(IEnumerable<ListMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        _members.AddRange(members);
    }

    /// <summary>
    /// Removes all members from the list.
    /// </summary>
    public void Clear() => _members.Clear();

    /// <inheritdoc/>
    public override string ToString() => string.Join(", ", _members.Select(m => m.ToString()));
}
