using DamianH.Http.StructuredFieldValues;

namespace HttpClientSample;

/// <summary>
/// Represents a Cache-Control header as an RFC 8941 Structured Dictionary.
/// </summary>
public class CacheControlHeader
{
    /// <summary>Gets or sets the max-age directive in seconds.</summary>
    public int? MaxAge { get; init; }

    /// <summary>Gets or sets the s-maxage directive in seconds.</summary>
    public int? SMaxAge { get; init; }

    /// <summary>Gets or sets whether the response is private.</summary>
    public bool? Private { get; init; }

    /// <summary>Gets or sets whether the response is public.</summary>
    public bool? Public { get; init; }

    /// <summary>Gets or sets whether caches must not serve stale responses.</summary>
    public bool? NoCache { get; init; }

    /// <summary>Gets or sets whether caches must revalidate stale entries.</summary>
    public bool? MustRevalidate { get; init; }

    /// <summary>Mapper for parsing and serializing Cache-Control headers.</summary>
    public static readonly StructuredFieldMapper<CacheControlHeader> Mapper =
        StructuredFieldMapper<CacheControlHeader>.Dictionary(b => b
            .Member("max-age", x => x.MaxAge)
            .Member("s-maxage", x => x.SMaxAge)
            .Member("private", x => x.Private)
            .Member("public", x => x.Public)
            .Member("no-cache", x => x.NoCache)
            .Member("must-revalidate", x => x.MustRevalidate));
}
