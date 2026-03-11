// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Constructs the signature base string per RFC 9421 §2.5.
/// The signature base is an ASCII string consisting of covered component values
/// followed by the <c>@signature-params</c> line.
/// </summary>
public static class SignatureBaseBuilder
{
    /// <summary>
    /// Creates the signature base as a byte array (UTF-8 encoding of ASCII string).
    /// </summary>
    /// <param name="parameters">The signature parameters defining covered components and metadata.</param>
    /// <param name="context">The HTTP message context to resolve component values from.</param>
    /// <returns>The signature base as a byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters or context is null.</exception>
    /// <exception cref="SignatureBaseException">
    /// Thrown when the signature base cannot be constructed (missing component, invalid parameter, duplicate identifier, etc.).
    /// </exception>
    public static byte[] Build(SignatureParameters parameters, IHttpMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(context);

        var str = BuildString(parameters, context);
        return Encoding.ASCII.GetBytes(str);
    }

    /// <summary>
    /// Creates the signature base as a string (for debugging and testing).
    /// </summary>
    /// <param name="parameters">The signature parameters defining covered components and metadata.</param>
    /// <param name="context">The HTTP message context to resolve component values from.</param>
    /// <returns>The signature base string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters or context is null.</exception>
    /// <exception cref="SignatureBaseException">
    /// Thrown when the signature base cannot be constructed.
    /// </exception>
    public static string BuildString(SignatureParameters parameters, IHttpMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(context);

        var sb = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // RFC 9421 §2.5: For each covered component in order
        foreach (var component in parameters.CoveredComponents)
        {
            var serializedId = component.Serialize();

            // RFC 9421 §2.5: Component identifiers MUST NOT appear more than once
            if (!seen.Add(serializedId))
            {
                throw new SignatureBaseException(
                    component,
                    "Duplicate component identifier in covered components list.");
            }

            // Resolve the component value
            string value;
            if (component.IsDerived)
            {
                value = DerivedComponentResolver.Resolve(component, context);
            }
            else
            {
                value = FieldComponentResolver.Resolve(component, context);
            }

            // RFC 9421 §2.5: each line is: "component-id": value\n
            sb.Append(serializedId);
            sb.Append(": ");
            sb.Append(value);
            sb.Append('\n');
        }

        // RFC 9421 §2.5: final line is the @signature-params component
        // The value is the serialized signature parameters (Inner List form)
        sb.Append("\"@signature-params\": ");
        sb.Append(parameters.Serialize());

        // No trailing newline after the @signature-params line (per RFC 9421 §2.5)

        return sb.ToString();
    }
}
