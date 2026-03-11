// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Exception thrown when a signature base cannot be constructed due to an error
/// resolving a component, invalid parameters, or other signature base issues.
/// RFC 9421 §2.5
/// </summary>
public sealed class SignatureBaseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureBaseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SignatureBaseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureBaseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SignatureBaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureBaseException"/> class
    /// with context about which component failed.
    /// </summary>
    /// <param name="component">The component identifier that caused the failure.</param>
    /// <param name="message">The error message.</param>
    public SignatureBaseException(ComponentIdentifier component, string message)
        : base($"Failed to resolve component '{component.Serialize()}': {message}")
    {
        Component = component;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureBaseException"/> class
    /// with context about which component failed.
    /// </summary>
    /// <param name="component">The component identifier that caused the failure.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SignatureBaseException(ComponentIdentifier component, string message, Exception innerException)
        : base($"Failed to resolve component '{component.Serialize()}': {message}", innerException)
    {
        Component = component;
    }

    /// <summary>Gets the component identifier that caused the failure, if available.</summary>
    public ComponentIdentifier? Component { get; }
}
