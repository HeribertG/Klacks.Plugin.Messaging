// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

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
