// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// One selectable action offered together with an outbound message. The recipient answers by picking
/// an entry instead of writing free text, so the reply is always a value from a fixed set: there is
/// nothing to interpret, no model is involved on the answering side, and the stored answer is an
/// exact record rather than a summary.
/// </summary>
/// <param name="Id">Stable machine value identifying the choice. An incoming answer is matched against this, and this is what an audit record keeps. Not meant to be shown to the recipient.</param>
/// <param name="Label">Human-readable text shown to the recipient, already localized by the caller.</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record MessageAction(string Id, string Label);
