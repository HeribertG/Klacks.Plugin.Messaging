// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Advisory skill: how well a group or a list of client id numbers can be contacted by messenger. The advice is computed in
/// MessagingReachAdvisor and returned unchanged; it carries counts and the names of visible people, never an address. Non-admins
/// see only their group visibility, and a hidden audience reads like an empty one.
/// </summary>
/// <param name="groupId">Group of the audience; its subgroups belong to it</param>
/// <param name="idNumbers">Client id numbers of the audience, instead of a group</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation(MessagingConstants.SkillEvaluateMessagingReach)]
public class EvaluateMessagingReachSkill : BaseSkillImplementation
{
    private readonly IMessagingReachService _reachService;

    public EvaluateMessagingReachSkill(IMessagingReachService reachService)
    {
        _reachService = reachService;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        BroadcastAudienceParameters audience;
        try
        {
            audience = BroadcastAudienceParameters.Parse(parameters);
        }
        catch (ArgumentException ex)
        {
            return SkillResult.Error(ex.Message);
        }

        var isAdmin = context.UserPermissions.Contains(MessagingConstants.RoleAdmin);
        var advice = await _reachService.EvaluateAsync(audience, isAdmin, cancellationToken);
        return SkillResult.SuccessResult(advice, advice.Recommendation);
    }
}
