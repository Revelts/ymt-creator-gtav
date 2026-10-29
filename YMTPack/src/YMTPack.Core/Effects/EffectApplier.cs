using YMTPack.Core.Planning;

namespace YMTPack.Core.Effects;

/// <summary>
/// Sets heel heights on feet drawables and hair scales on p_head props whose source .ydd
/// matches a rule. The last matching rule wins. Matches on other slots and rules that
/// match nothing become warnings.
/// </summary>
public static class EffectApplier
{
    private const int FeetComponentId = 6;
    private const int HeadPropAnchorId = 0;

    public static PackPlan Apply(PackPlan plan, EffectRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.IsEmpty)
        {
            return plan;
        }

        var diagnostics = new List<Diagnostic>(plan.Diagnostics);
        var matched = new HashSet<EffectRule>();

        List<PedVariationPlan> variations = plan.Variations.Select(v => v with
        {
            Components = v.Components.Select(slot => slot with
            {
                Drawables = slot.Drawables.Select(d =>
                    d with { HeelHeight = Resolve(rules.Heels, d, slot.SlotId == FeetComponentId, "heels only apply to feet", matched, diagnostics) ?? d.HeelHeight })
                    .ToList(),
            }).ToList(),
            Props = v.Props.Select(slot => slot with
            {
                Drawables = slot.Drawables.Select(d =>
                    d with { HairScale = Resolve(rules.Hats, d, slot.SlotId == HeadPropAnchorId, "hair scale only applies to p_head props", matched, diagnostics) ?? d.HairScale })
                    .ToList(),
            }).ToList(),
        }).ToList();

        // Heels rules are never checked against props (and hats against components),
        // so check them here too before calling a rule unmatched.
        WarnOnOtherSlotMatches(plan, rules, matched, diagnostics);
        diagnostics.AddRange(rules.Heels.Concat(rules.Hats)
            .Where(r => !matched.Contains(r))
            .Select(r => new Diagnostic(Severity.Warning, $"rule '{r.Pattern}' matched nothing", r.Location)));

        return plan with { Variations = variations, Diagnostics = diagnostics };
    }

    private static float? Resolve(
        IReadOnlyList<EffectRule> rules,
        DrawablePlan drawable,
        bool eligible,
        string notEligibleMessage,
        HashSet<EffectRule> matched,
        List<Diagnostic> diagnostics)
    {
        EffectRule? last = null;
        foreach (EffectRule rule in rules.Where(r => Glob.IsMatch(r.Pattern, drawable.Source.RelativePath)))
        {
            matched.Add(rule);
            last = rule;
        }

        if (last is null)
        {
            return null;
        }

        if (!eligible)
        {
            diagnostics.Add(new Diagnostic(Severity.Warning, $"{notEligibleMessage}; {last.Location} ignored here", drawable.Source.RelativePath));
            return null;
        }

        return last.Value;
    }

    private static void WarnOnOtherSlotMatches(PackPlan plan, EffectRuleSet rules, HashSet<EffectRule> matched, List<Diagnostic> diagnostics)
    {
        foreach (PedVariationPlan v in plan.Variations)
        {
            foreach (DrawablePlan d in v.Props.SelectMany(s => s.Drawables))
            {
                Resolve(rules.Heels, d, eligible: false, "heels only apply to feet", matched, diagnostics);
            }

            foreach (DrawablePlan d in v.Components.SelectMany(s => s.Drawables))
            {
                Resolve(rules.Hats, d, eligible: false, "hair scale only applies to p_head props", matched, diagnostics);
            }
        }
    }
}
