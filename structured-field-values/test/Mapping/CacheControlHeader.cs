// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Represents a Cache-Control header value.
/// Plain POCO — no attributes, no partial class.
/// </summary>
public class CacheControlHeader
{
    public int? MaxAge { get; init; }
    public int? SMaxAge { get; init; }
    public bool? NoCache { get; init; }
    public bool? NoStore { get; init; }
    public bool? MustRevalidate { get; init; }
    public bool? Private { get; init; }
    public bool? Public { get; init; }
}
