// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// The result of a signature verification operation.
/// </summary>
public sealed class VerificationResult
{
    private VerificationResult(bool isValid, SignatureParameters? parameters, string? errorMessage)
    {
        IsValid = isValid;
        Parameters = parameters;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets a value indicating whether the signature is valid.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the parsed signature parameters if available.</summary>
    public SignatureParameters? Parameters { get; }

    /// <summary>Gets the error message if verification failed.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates a successful verification result.</summary>
    /// <param name="parameters">The verified signature parameters.</param>
    internal static VerificationResult Success(SignatureParameters parameters) =>
        new(true, parameters, null);

    /// <summary>Creates a failed verification result.</summary>
    /// <param name="errorMessage">Description of why verification failed.</param>
    /// <param name="parameters">The parsed signature parameters, if available.</param>
    internal static VerificationResult Failure(string errorMessage, SignatureParameters? parameters = null) =>
        new(false, parameters, errorMessage);
}
