using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Scanning;

namespace YMTPack.Core.Planning;

public sealed record TexturePlan(char Letter, int TexId, ScannedAsset Source);

/// <param name="Index">Drawable index inside its YMT (000-127).</param>
/// <param name="Variant">u/r for components; null for props.</param>
/// <param name="Cloth">.yld physics file, if any.</param>
/// <param name="HeelHeight">High-heel lift for feet drawables (expressionMods f4), or null.</param>
/// <param name="HairScale">How much a hat shrinks hair, 0-1 (prop expressionMods f0 = -value), or null.</param>
public sealed record DrawablePlan(
    int Index,
    DrawableVariant? Variant,
    ScannedAsset Source,
    ScannedAsset? Cloth,
    IReadOnlyList<TexturePlan> Textures,
    float? HeelHeight = null,
    float? HairScale = null);

public sealed record SlotPlan(bool IsProp, int SlotId, IReadOnlyList<DrawablePlan> Drawables)
{
    public string SlotName => PedSlots.SlotName(IsProp, SlotId);
}

public sealed record StreamFile(ScannedAsset Source, string TargetFileName);

/// <summary>One generated .ymt: a gender plus a DLC name, with its components and props.</summary>
public sealed record PedVariationPlan(
    Gender Gender,
    string DlcName,
    IReadOnlyList<SlotPlan> Components,
    IReadOnlyList<SlotPlan> Props)
{
    /// <summary>YMT / fullDlcName, e.g. mp_m_freemode_01_mypack.</summary>
    public string FullName => $"{Gender.PedName()}_{DlcName}";

    /// <summary>Stream prefix for props, e.g. mp_m_freemode_01_p_mypack.</summary>
    public string PropPrefix => $"{Gender.PedName()}_p_{DlcName}";

    /// <summary>Per-gender name used by grzyClothTool, e.g. mp_creaturemetadata_f_mypack.</summary>
    public string CreatureMetadataName => $"mp_creaturemetadata_{Gender.Letter()}_{DlcName}";

    public int HeelCount => Components.Sum(c => c.Drawables.Count(d => d.HeelHeight is not null));

    public int HairScaleCount => Props.Sum(p => p.Drawables.Count(d => d.HairScale is not null));

    public bool HasCreatureMetadata => HeelCount > 0 || HairScaleCount > 0;

    public IEnumerable<StreamFile> StreamFiles()
    {
        foreach (SlotPlan slot in Components)
        {
            foreach (DrawablePlan d in slot.Drawables)
            {
                string variant = d.Variant == DrawableVariant.Race ? "r" : "u";
                string model = $"{FullName}^{slot.SlotName}_{d.Index:000}_{variant}";
                yield return new StreamFile(d.Source, model + ".ydd");
                if (d.Cloth is not null)
                {
                    yield return new StreamFile(d.Cloth, model + ".yld");
                }

                foreach (TexturePlan t in d.Textures)
                {
                    string race = PedSlots.RaceTexIds.First(r => r.Value == t.TexId).Key;
                    yield return new StreamFile(t.Source, $"{FullName}^{slot.SlotName}_diff_{d.Index:000}_{t.Letter}_{race}.ytd");
                }
            }
        }

        foreach (SlotPlan slot in Props)
        {
            foreach (DrawablePlan d in slot.Drawables)
            {
                yield return new StreamFile(d.Source, $"{PropPrefix}^{slot.SlotName}_{d.Index:000}.ydd");
                foreach (TexturePlan t in d.Textures)
                {
                    yield return new StreamFile(t.Source, $"{PropPrefix}^{slot.SlotName}_diff_{d.Index:000}_{t.Letter}.ytd");
                }
            }
        }
    }
}

public sealed record PackPlan(
    string PackName,
    IReadOnlyList<PedVariationPlan> Variations,
    IReadOnlyList<Diagnostic> Diagnostics);
