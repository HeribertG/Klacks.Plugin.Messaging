// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Resolves the IMessagingProviderAdapter for a given provider type, so Application-layer services
/// can query optional adapter capabilities (e.g. IPairingInstructionsProvider) without depending on
/// the concrete Infrastructure factory directly.
/// </summary>
/// <param name="providerType">The provider type constant from MessagingConstants</param>
using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessagingProviderAdapterFactory
{
    IMessagingProviderAdapter Create(string providerType);
}
