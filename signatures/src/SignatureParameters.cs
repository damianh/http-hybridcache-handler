// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Represents the signature parameters per RFC 9421 §2.3.
/// Contains the ordered set of covered components and signature metadata (created, expires, nonce, alg, keyid, tag).
/// </summary>
public sealed class SignatureParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureParameters"/> class.
    /// </summary>
    /// <param name="coveredComponents">The ordered list of component identifiers to cover.</param>
    public SignatureParameters(IReadOnlyList<ComponentIdentifier> coveredComponents)
    {
        ArgumentNullException.ThrowIfNull(coveredComponents);
        CoveredComponents = coveredComponents;
    }

    /// <summary>Gets the ordered list of covered component identifiers.</summary>
    public IReadOnlyList<ComponentIdentifier> CoveredComponents { get; }

    /// <summary>
    /// Gets the raw serialized form of these parameters, preserved from the original
    /// <c>Signature-Input</c> header when parsed. Used by the verifier to reconstruct
    /// the exact signature base that the signer produced.
    /// When null, <see cref="Serialize"/> generates the canonical form.
    /// </summary>
    internal string? RawSerializedValue { get; init; }

    /// <summary>Gets the Unix timestamp of when the signature was created.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the Unix timestamp of when the signature expires.</summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>Gets the nonce value to prevent signature replay attacks.</summary>
    public string? Nonce { get; init; }

    /// <summary>Gets the algorithm name as registered in the HTTP Signature Algorithms registry.</summary>
    public string? Algorithm { get; init; }

    /// <summary>Gets the key identifier used to select the signing/verification key.</summary>
    public string? KeyId { get; init; }

    /// <summary>Gets the application-specific tag value.</summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Serializes these signature parameters to the Inner List format used in
    /// the signature base and <c>Signature-Input</c> header.
    /// Example: <c>("@method" "@authority" "content-type");created=1618884473;keyid="test-key"</c>
    /// </summary>
    /// <returns>The serialized Inner List string.</returns>
    public string Serialize()
    {
        // When parsed from a Signature-Input header, use the preserved raw serialization
        // to ensure the @signature-params line in the signature base matches exactly
        // what the signer used (preserving original parameter order).
        if (RawSerializedValue is not null)
        {
            return RawSerializedValue;
        }

        var sb = new StringBuilder();

        // Inner list opening: (component1 component2 ...)
        sb.Append('(');
        for (var i = 0; i < CoveredComponents.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(CoveredComponents[i].Serialize());
        }
        sb.Append(')');

        // Signature parameters — order per RFC 9421 Appendix B test vectors:
        // created, expires, keyid, nonce, alg, tag
        if (Created.HasValue)
        {
            sb.Append(";created=");
            sb.Append(Created.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }
        if (Expires.HasValue)
        {
            sb.Append(";expires=");
            sb.Append(Expires.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }
        if (KeyId is not null)
        {
            sb.Append(";keyid=\"");
            AppendEscapedString(sb, KeyId);
            sb.Append('"');
        }
        if (Nonce is not null)
        {
            sb.Append(";nonce=\"");
            AppendEscapedString(sb, Nonce);
            sb.Append('"');
        }
        if (Algorithm is not null)
        {
            sb.Append(";alg=\"");
            AppendEscapedString(sb, Algorithm);
            sb.Append('"');
        }
        if (Tag is not null)
        {
            sb.Append(";tag=\"");
            AppendEscapedString(sb, Tag);
            sb.Append('"');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses signature parameters from a Signature-Input dictionary member (an <see cref="InnerList"/> with parameters).
    /// </summary>
    /// <param name="innerList">The inner list representing the signature parameters.</param>
    /// <returns>The parsed <see cref="SignatureParameters"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when innerList is null.</exception>
    /// <exception cref="FormatException">Thrown when the inner list cannot be parsed as signature parameters.</exception>
    public static SignatureParameters Parse(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);

        var coveredComponents = new List<ComponentIdentifier>(innerList.Count);

        for (var i = 0; i < innerList.Count; i++)
        {
            var item = innerList[i];
            if (item is not StringItem nameItem)
            {
                throw new FormatException(
                    $"Component identifier at index {i} must be an SF String, but was {item.GetType().Name}.");
            }

            var name = nameItem.StringValue;
            var componentParams = item.Parameters;

            var sf = componentParams.ContainsKey("sf");
            string? key = null;
            if (componentParams.TryGetValue("key", out var keyItem))
            {
                if (keyItem is not StringItem keyStr)
                    throw new FormatException("Component identifier 'key' parameter must be an SF String.");
                key = keyStr.StringValue;
            }
            var bs = componentParams.ContainsKey("bs");
            var req = componentParams.ContainsKey("req");
            var tr = componentParams.ContainsKey("tr");
            string? queryParamName = null;
            if (componentParams.TryGetValue("name", out var nameParamItem))
            {
                if (nameParamItem is not StringItem nameParamStr)
                    throw new FormatException("Component identifier 'name' parameter must be an SF String.");
                queryParamName = nameParamStr.StringValue;
            }

            coveredComponents.Add(new ComponentIdentifier(name)
            {
                Sf = sf,
                Key = key,
                Bs = bs,
                Req = req,
                Tr = tr,
                QueryParamName = queryParamName,
            });
        }

        var sigParams = innerList.Parameters;

        DateTimeOffset? created = null;
        if (sigParams.TryGetValue("created", out var createdItem))
        {
            if (createdItem is not IntegerItem createdInt)
                throw new FormatException("Signature parameter 'created' must be an SF Integer.");
            created = DateTimeOffset.FromUnixTimeSeconds(createdInt.LongValue);
        }

        DateTimeOffset? expires = null;
        if (sigParams.TryGetValue("expires", out var expiresItem))
        {
            if (expiresItem is not IntegerItem expiresInt)
                throw new FormatException("Signature parameter 'expires' must be an SF Integer.");
            expires = DateTimeOffset.FromUnixTimeSeconds(expiresInt.LongValue);
        }

        string? nonce = null;
        if (sigParams.TryGetValue("nonce", out var nonceItem))
        {
            if (nonceItem is not StringItem nonceStr)
                throw new FormatException("Signature parameter 'nonce' must be an SF String.");
            nonce = nonceStr.StringValue;
        }

        string? alg = null;
        if (sigParams.TryGetValue("alg", out var algItem))
        {
            if (algItem is not StringItem algStr)
                throw new FormatException("Signature parameter 'alg' must be an SF String.");
            alg = algStr.StringValue;
        }

        string? keyId = null;
        if (sigParams.TryGetValue("keyid", out var keyIdItem))
        {
            if (keyIdItem is not StringItem keyIdStr)
                throw new FormatException("Signature parameter 'keyid' must be an SF String.");
            keyId = keyIdStr.StringValue;
        }

        string? tag = null;
        if (sigParams.TryGetValue("tag", out var tagItem))
        {
            if (tagItem is not StringItem tagStr)
                throw new FormatException("Signature parameter 'tag' must be an SF String.");
            tag = tagStr.StringValue;
        }

        return new SignatureParameters(coveredComponents)
        {
            Created = created,
            Expires = expires,
            Nonce = nonce,
            Algorithm = alg,
            KeyId = keyId,
            Tag = tag,
            RawSerializedValue = SerializeInnerList(innerList),
        };
    }

    /// <summary>
    /// Serializes an <see cref="InnerList"/> to the RFC 8941 canonical form,
    /// preserving the parameter order from the parsed input.
    /// This is used to reconstruct the exact <c>@signature-params</c> value
    /// that was present in the original <c>Signature-Input</c> header.
    /// </summary>
    private static string SerializeInnerList(InnerList innerList)
    {
        var sb = new StringBuilder();
        sb.Append('(');

        for (var i = 0; i < innerList.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            SerializeSfItem(innerList[i], sb);
        }

        sb.Append(')');
        SerializeSfParameters(innerList.Parameters, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Serializes a structured field item (bare item + parameters) per RFC 8941.
    /// </summary>
    private static void SerializeSfItem(StructuredFieldItem item, StringBuilder sb)
    {
        // Bare item
        switch (item)
        {
            case StringItem si:
                sb.Append('"');
                AppendEscapedString(sb, si.StringValue);
                sb.Append('"');
                break;
            case IntegerItem ii:
                sb.Append(ii.LongValue.ToString(CultureInfo.InvariantCulture));
                break;
            case TokenItem ti:
                sb.Append(ti.TokenValue);
                break;
            case BooleanItem bi:
                sb.Append(bi.BooleanValue ? "?1" : "?0");
                break;
            case ByteSequenceItem bsi:
                sb.Append(':');
                sb.Append(Convert.ToBase64String(bsi.ByteArrayValue));
                sb.Append(':');
                break;
            case DecimalItem di:
                sb.Append(di.DecimalValue.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new FormatException($"Unsupported structured field item type: {item.GetType().Name}");
        }

        // Item parameters
        SerializeSfParameters(item.Parameters, sb);
    }

    /// <summary>
    /// Serializes structured field parameters per RFC 8941 § 4.1.1.2,
    /// preserving the iteration (insertion) order.
    /// </summary>
    private static void SerializeSfParameters(Parameters parameters, StringBuilder sb)
    {
        foreach (var (key, value) in parameters)
        {
            sb.Append(';');
            sb.Append(key);
            if (value is not null and not BooleanItem { BooleanValue: true })
            {
                sb.Append('=');
                switch (value)
                {
                    case StringItem si:
                        sb.Append('"');
                        AppendEscapedString(sb, si.StringValue);
                        sb.Append('"');
                        break;
                    case IntegerItem ii:
                        sb.Append(ii.LongValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case TokenItem ti:
                        sb.Append(ti.TokenValue);
                        break;
                    case BooleanItem bi:
                        sb.Append(bi.BooleanValue ? "?1" : "?0");
                        break;
                    case ByteSequenceItem bsi:
                        sb.Append(':');
                        sb.Append(Convert.ToBase64String(bsi.ByteArrayValue));
                        sb.Append(':');
                        break;
                    case DecimalItem di:
                        sb.Append(di.DecimalValue.ToString(CultureInfo.InvariantCulture));
                        break;
                }
            }
        }
    }

    private static void AppendEscapedString(StringBuilder sb, string value)
    {
        foreach (var c in value)
        {
            if (c is '\\' or '"') sb.Append('\\');
            sb.Append(c);
        }
    }
}
