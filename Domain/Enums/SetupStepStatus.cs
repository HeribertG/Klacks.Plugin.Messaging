// Copyright (c) Heribert Gasparoli Private. All rights reserved.

using System.Text.Json.Serialization;

namespace Klacks.Plugin.Messaging.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<SetupStepStatus>))]
public enum SetupStepStatus
{
    Ok,
    ActionRequired,
    Error,
    NotChecked
}
