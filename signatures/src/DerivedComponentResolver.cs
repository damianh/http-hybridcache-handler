// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Web;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Resolves derived component values from an <see cref="IHttpMessageContext"/>.
/// Derived components start with '@' and represent specific attributes of an HTTP message.
/// RFC 9421 §2.2
/// </summary>
internal static class DerivedComponentResolver
{
    /// <summary>
    /// Resolves the value of a derived component from the given message context.
    /// </summary>
    /// <param name="identifier">The component identifier (must be derived, i.e., start with '@').</param>
    /// <param name="context">The HTTP message context to resolve from.</param>
    /// <returns>The component value string.</returns>
    /// <exception cref="SignatureBaseException">Thrown when the component cannot be resolved.</exception>
    internal static string Resolve(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        // If req is set, resolve from the associated request context
        var resolveContext = identifier.Req
            ? context.AssociatedRequest ?? throw new SignatureBaseException(
                identifier,
                "Component has 'req' parameter but no associated request is available.")
            : context;

        return identifier.Name switch
        {
            "@method" => ResolveMethod(identifier, resolveContext),
            "@target-uri" => ResolveTargetUri(identifier, resolveContext),
            "@authority" => ResolveAuthority(identifier, resolveContext),
            "@scheme" => ResolveScheme(identifier, resolveContext),
            "@request-target" => ResolveRequestTarget(identifier, resolveContext),
            "@path" => ResolvePath(identifier, resolveContext),
            "@query" => ResolveQuery(identifier, resolveContext),
            "@query-param" => ResolveQueryParam(identifier, resolveContext),
            "@status" => ResolveStatus(identifier, resolveContext),
            _ => throw new SignatureBaseException(identifier, $"Unknown derived component '{identifier.Name}'."),
        };
    }

    private static string ResolveMethod(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@method is only valid for request messages.");

        return context.Method?.ToUpperInvariant()
            ?? throw new SignatureBaseException(identifier, "HTTP method is not available.");
    }

    private static string ResolveTargetUri(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@target-uri is only valid for request messages.");

        return context.TargetUri
            ?? throw new SignatureBaseException(identifier, "Target URI is not available.");
    }

    private static string ResolveAuthority(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@authority is only valid for request messages.");

        var authority = context.Authority
            ?? throw new SignatureBaseException(identifier, "Authority is not available.");

        // RFC 9421 §2.2.3: authority must be lowercase
        return authority.ToLowerInvariant();
    }

    private static string ResolveScheme(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@scheme is only valid for request messages.");

        var scheme = context.Scheme
            ?? throw new SignatureBaseException(identifier, "Scheme is not available.");

        // RFC 9421 §2.2.4: scheme must be lowercase
        return scheme.ToLowerInvariant();
    }

    private static string ResolveRequestTarget(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@request-target is only valid for request messages.");

        return context.RequestTarget
            ?? throw new SignatureBaseException(identifier, "Request target is not available.");
    }

    private static string ResolvePath(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@path is only valid for request messages.");

        var path = context.Path;

        // RFC 9421 §2.2.6: if the path is empty, use "/"
        if (string.IsNullOrEmpty(path))
            return "/";

        return path;
    }

    private static string ResolveQuery(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@query is only valid for request messages.");

        // RFC 9421 §2.2.7: query string including the leading '?'
        // If no query, use just "?"
        var query = context.Query;
        return query ?? "?";
    }

    private static string ResolveQueryParam(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@query-param is only valid for request messages.");

        var paramName = identifier.QueryParamName
            ?? throw new SignatureBaseException(identifier, "@query-param requires a 'name' parameter.");

        var query = context.Query;
        if (query is null || query == "?")
            throw new SignatureBaseException(identifier, $"Query parameter '{paramName}' not found: no query string present.");

        // Remove leading '?'
        var queryString = query.StartsWith('?') ? query[1..] : query;

        // RFC 9421 §2.2.8: parse query parameters, percent-decode names to match
        var pairs = queryString.Split('&');
        var found = false;
        string? value = null;

        foreach (var pair in pairs)
        {
            var eqIdx = pair.IndexOf('=');
            string pairName;
            string pairValue;

            if (eqIdx >= 0)
            {
                pairName = pair[..eqIdx];
                pairValue = pair[(eqIdx + 1)..];
            }
            else
            {
                pairName = pair;
                pairValue = string.Empty;
            }

            // Decode the name to compare
            var decodedName = HttpUtility.UrlDecode(pairName);
            if (decodedName == paramName)
            {
                value = pairValue;
                found = true;
                break;
            }
        }

        if (!found)
            throw new SignatureBaseException(identifier, $"Query parameter '{paramName}' not found in query string.");

        // RFC 9421 §2.2.8: value is the percent-encoded value as it appears in the query string
        return value ?? string.Empty;
    }

    private static string ResolveStatus(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (context.IsRequest)
            throw new SignatureBaseException(identifier, "@status is only valid for response messages.");

        var status = context.StatusCode
            ?? throw new SignatureBaseException(identifier, "HTTP status code is not available.");

        return status.ToString();
    }
}
