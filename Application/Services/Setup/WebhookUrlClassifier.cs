// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Classifies a configured webhook URL by HTTPS and public-reachability plausibility, without making
/// any network call.
/// </summary>
using System.Net;
using System.Net.Sockets;
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public static class WebhookUrlClassifier
{
    private const string LocalSuffix = ".local";
    private const string LocalhostSuffix = ".localhost";
    private const string InternalSuffix = ".internal";
    private const string LanSuffix = ".lan";
    private const string HomeArpaSuffix = ".home.arpa";
    private const string Localhost = "localhost";
    private const char TrailingDot = '.';
    private const char HostLabelSeparator = '.';
    private const char Ipv6BracketOpen = '[';
    private const char Ipv6BracketClose = ']';
    private const byte ClassAPrivateFirstOctet = 10;
    private const byte ClassBPrivateFirstOctet = 172;
    private const byte ClassBPrivateSecondOctetStart = 16;
    private const byte ClassBPrivateSecondOctetEnd = 31;
    private const byte ClassCPrivateFirstOctet = 192;
    private const byte ClassCPrivateSecondOctet = 168;
    private const byte LinkLocalFirstOctet = 169;
    private const byte LinkLocalSecondOctet = 254;
    private const byte CarrierGradeNatFirstOctet = 100;
    private const byte CarrierGradeNatSecondOctetStart = 64;
    private const byte CarrierGradeNatSecondOctetEnd = 127;

    public static WebhookUrlVerdict Classify(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return WebhookUrlVerdict.Missing;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return WebhookUrlVerdict.Invalid;

        if (uri.Scheme != Uri.UriSchemeHttps)
            return WebhookUrlVerdict.NotHttps;

        var host = uri.Host.TrimEnd(TrailingDot);

        if (IsNonPublicHostName(host))
            return WebhookUrlVerdict.NotPublic;

        if (IPAddress.TryParse(host.Trim(Ipv6BracketOpen, Ipv6BracketClose), out var ip))
            return IsPrivate(ip) ? WebhookUrlVerdict.NotPublic : WebhookUrlVerdict.Public;

        return host.Contains(HostLabelSeparator) ? WebhookUrlVerdict.Public : WebhookUrlVerdict.NotPublic;
    }

    private static bool IsNonPublicHostName(string host)
    {
        return host.Equals(Localhost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(LocalSuffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(LocalhostSuffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(InternalSuffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(LanSuffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(HomeArpaSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;

        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
            return true;

        if (ip.AddressFamily != AddressFamily.InterNetwork && !ip.IsIPv4MappedToIPv6)
            return false;

        var bytes = ip.MapToIPv4().GetAddressBytes();
        return bytes[0] == ClassAPrivateFirstOctet
            || (bytes[0] == ClassBPrivateFirstOctet && bytes[1] >= ClassBPrivateSecondOctetStart && bytes[1] <= ClassBPrivateSecondOctetEnd)
            || (bytes[0] == ClassCPrivateFirstOctet && bytes[1] == ClassCPrivateSecondOctet)
            || (bytes[0] == LinkLocalFirstOctet && bytes[1] == LinkLocalSecondOctet)
            || (bytes[0] == CarrierGradeNatFirstOctet && bytes[1] >= CarrierGradeNatSecondOctetStart && bytes[1] <= CarrierGradeNatSecondOctetEnd);
    }
}
