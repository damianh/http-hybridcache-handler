// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Represents an encoding item with an optional quality parameter.
/// Plain POCO — no attributes, no partial class.
/// </summary>
public class EncodingItem
{
    public string Encoding { get; init; } = "";
    public decimal? Quality { get; init; }
}
