// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Represents the RFC 9218 Priority header for HTTP/2 and HTTP/3.
/// Plain POCO — no attributes, no partial class.
/// </summary>
public class PriorityHeader
{
    /// <summary>Gets the urgency level (0-7).</summary>
    public int? Urgency { get; init; }

    /// <summary>Gets whether the response can be processed incrementally.</summary>
    public bool? Incremental { get; init; }
}
