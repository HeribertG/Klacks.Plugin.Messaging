// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Skill returning the deterministic messenger setup diagnosis unchanged, so the assistant can explain
/// the next open commissioning step of each provider. Admin restriction comes from the skill seed.
/// </summary>
/// <param name="diagnostics">Composes the setup report for all configured providers</param>
using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Interfaces;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("diagnose_messaging_setup")]
public class DiagnoseMessagingSetupSkill : BaseSkillImplementation
{
    private readonly IMessagingSetupDiagnosticsService _diagnostics;

    public DiagnoseMessagingSetupSkill(IMessagingSetupDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var report = await _diagnostics.DiagnoseAsync(cancellationToken);
        var openCount = report.Providers.Count(provider => provider.NextStep != null);

        return SkillResult.SuccessResult(
            report,
            $"{report.Providers.Count} messaging provider(s) diagnosed, {openCount} need(s) action.");
    }
}
