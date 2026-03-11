using DamianH.Http.StructuredFieldValues;

namespace HttpClientSample;

/// <summary>
/// Represents the RFC 9218 Priority header.
/// Example: "u=3, i"
/// </summary>
public class PriorityHeader
{
    /// <summary>Gets the urgency level (0-7).</summary>
    public int? Urgency { get; init; }

    /// <summary>Gets whether the response can be processed incrementally.</summary>
    public bool? Incremental { get; init; }

    /// <summary>Mapper for parsing and serializing Priority headers.</summary>
    public static readonly StructuredFieldMapper<PriorityHeader> Mapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));
}
