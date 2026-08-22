// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants for the admin-initiated messenger pairing invite sent to an AppUser by email.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class UserInviteConstants
{
    public const string InvitationSubject = "Klacks messenger invitation";
    public const string FallbackRecipientName = "there";

    public const string InvitationBodyTemplate =
        "Hello {0},\n\n" +
        "An administrator has invited you to link a messenger account with Klacks.\n" +
        "{1}\n\n" +
        "This invitation expires in {2} hours.";
}
