// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Reason codes an ICredentialDiagnoser can return next to its Valid/IsValid verdict.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class CredentialReasonCodes
{
    public const string Valid = "Valid";
    public const string MissingToken = "MissingToken";
    public const string Rejected = "Rejected";
    public const string MissingScope = "MissingScope";
    public const string Unreachable = "Unreachable";
    public const string UnexpectedResponse = "UnexpectedResponse";
}
