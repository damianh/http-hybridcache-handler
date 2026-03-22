// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Abstraction over an HTTP message providing component values for signature base construction.
/// Implementations adapt specific HTTP frameworks (HttpClient, ASP.NET Core, etc.).
/// </summary>
public interface IHttpMessageContext
{
    /// <summary>Whether this context represents a request or a response.</summary>
    bool IsRequest { get; }

    /// <summary>The HTTP method (e.g., "GET", "POST"). Only valid for requests.</summary>
    string? Method { get; }

    /// <summary>The scheme of the target URI (e.g., "https"). Only valid for requests.</summary>
    string? Scheme { get; }

    /// <summary>The authority of the target URI (e.g., "example.com"). Only valid for requests.</summary>
    string? Authority { get; }

    /// <summary>The absolute path of the target URI (e.g., "/foo"). Only valid for requests.</summary>
    string? Path { get; }

    /// <summary>The query string including leading '?' (e.g., "?a=b"). Empty query is "?". Absent query is null. Only valid for requests.</summary>
    string? Query { get; }

    /// <summary>The full target URI (e.g., "https://example.com/foo?a=b"). Only valid for requests.</summary>
    string? TargetUri { get; }

    /// <summary>The request target (e.g., "/foo?a=b"). Only valid for requests.</summary>
    string? RequestTarget { get; }

    /// <summary>The HTTP status code. Only valid for responses.</summary>
    int? StatusCode { get; }

    /// <summary>
    /// Gets the combined, canonicalized value for the named header field.
    /// Returns null if the field is not present.
    /// Multiple values are combined per RFC 9110 §5.2 (comma + space).
    /// </summary>
    string? GetHeaderValue(string fieldName);

    /// <summary>
    /// Gets the individual raw values for the named header field.
    /// Returns empty if the field is not present.
    /// Used for <c>bs</c> (binary-wrapped) processing.
    /// </summary>
    IReadOnlyList<string> GetHeaderValues(string fieldName);

    /// <summary>
    /// For response contexts, the associated request context (for <c>req</c> parameter).
    /// Null for request contexts.
    /// </summary>
    IHttpMessageContext? AssociatedRequest { get; }
}
