using System.Text.RegularExpressions;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Scanning;

namespace YMTPack.Core.Planning;

/// <summary>
/// Turns scanned files from any number of source packs into fresh YMT layouts:
/// pairs drawables with textures/cloth, renumbers drawables 000.. per slot and gender,
/// renumbers texture letters a.., and splits into extra YMTs past the per-slot limit.
/// </summary>
public static partial class PackPlanner
{
    /// <summary>Drawables per component per YMT (cfx.re addon clothing tutorial: 0-127).</summary>
    public const int MaxDrawablesPerSlot = 128;

    /// <summary>Keeps "mp_m_freemode_01_p_{name}_NN" under YMTEditor's 60-character name limit.</summary>
    public const int MaxPackNameLength = 32;

    private sealed record Draft(
        Gender Gender,
        bool IsProp,
        int SlotId,
        string SourceGroup,
        int Number,
        ScannedAsset Model,
        ScannedAsset? Cloth,
        IReadOnlyList<TexturePlan> Textures);

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex ValidPackName();

    public static PackPlan Plan(IEnumerable<ScannedAsset> assets, string packName)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ValidatePackName(packName);

        var diagnostics = new List<Diagnostic>();
        List<Draft> drafts = assets
            .GroupBy(a => (a.Gender, a.Name.IsProp, a.Name.SlotId, a.SourceGroup, a.Name.Number))
            .OrderBy(g => g.Key.Gender)
            .ThenBy(g => g.Key.IsProp)
            .ThenBy(g => g.Key.SlotId)
            .ThenBy(g => g.Key.SourceGroup, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Number)
            .Select(g => ToDraft(g.Key, g.OrderBy(a => a.RelativePath, StringComparer.Ordinal).ToList(), diagnostics))
            .OfType<Draft>()
            .ToList();

        return new PackPlan(packName, BuildVariations(drafts, packName), diagnostics);
    }

    public static void ValidatePackName(string packName)
    {
        if (string.IsNullOrEmpty(packName) || packName.Length > MaxPackNameLength || !ValidPackName().IsMatch(packName))
        {
            throw new ArgumentException(
                $"Pack name must be 1-{MaxPackNameLength} characters of lowercase a-z, 0-9 or '_' (got \"{packName}\").",
                nameof(packName));
        }
    }

    private static Draft? ToDraft(
        (Gender Gender, bool IsProp, int SlotId, string SourceGroup, int Number) key,
        IReadOnlyList<ScannedAsset> files,
        List<Diagnostic> diagnostics)
    {
        // When both _u and _r exist for one number, keep the universal one.
        List<ScannedAsset> models = files
            .Where(f => f.Name.Kind == AssetKind.Drawable)
            .OrderBy(f => f.Name.Variant == DrawableVariant.Race)
            .ToList();
        if (models.Count == 0)
        {
            diagnostics.AddRange(files.Select(f =>
                new Diagnostic(Severity.Warning, "no matching drawable (.ydd) in the same folder, skipped", f.RelativePath)));
            return null;
        }

        ScannedAsset model = models[0];
        diagnostics.AddRange(models.Skip(1).Select(f =>
            new Diagnostic(Severity.Warning, $"duplicate drawable number, kept {model.RelativePath}", f.RelativePath)));

        IReadOnlyList<TexturePlan> textures = PlanTextures(model, files, diagnostics);
        if (textures.Count == 0)
        {
            diagnostics.Add(new Diagnostic(Severity.Warning, "drawable has no textures (.ytd), skipped", model.RelativePath));
            return null;
        }

        ScannedAsset? cloth = PickCloth(model, files, diagnostics);
        return new Draft(key.Gender, key.IsProp, key.SlotId, key.SourceGroup, key.Number, model, cloth, textures);
    }

    private static IReadOnlyList<TexturePlan> PlanTextures(
        ScannedAsset model, IReadOnlyList<ScannedAsset> files, List<Diagnostic> diagnostics)
    {
        IReadOnlyList<ScannedAsset> usable = TexturesForVariant(
            model, files.Where(f => f.Name.Kind == AssetKind.Texture).ToList(), diagnostics);

        // Same letter twice (e.g. a_whi + a_bla): keep the lowest race id.
        var byLetter = usable
            .GroupBy(f => f.Name.TextureLetter!.Value)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(f => f.Name.TexId ?? 0).ThenBy(f => f.RelativePath, StringComparer.Ordinal).ToList())
            .ToList();

        foreach (List<ScannedAsset> dupes in byLetter.Where(g => g.Count > 1))
        {
            diagnostics.AddRange(dupes.Skip(1).Select(f => new Diagnostic(
                Severity.Warning, $"duplicate texture letter '{f.Name.TextureLetter}', kept {dupes[0].RelativePath}", f.RelativePath)));
        }

        return byLetter
            .Select((g, i) => new TexturePlan((char)('a' + i), g[0].Name.TexId ?? 0, g[0]))
            .ToList();
    }

    // _u drawables use _uni textures and _r drawables use race textures. When both kinds sit
    // next to one drawable, the other kind belongs to a skipped sibling and is dropped; when
    // only the "wrong" kind exists, it is kept so the drawable is not lost.
    private static IReadOnlyList<ScannedAsset> TexturesForVariant(
        ScannedAsset model, IReadOnlyList<ScannedAsset> textures, List<Diagnostic> diagnostics)
    {
        if (model.Name.Variant is not { } variant)
        {
            return textures;
        }

        bool expectsUni = variant == DrawableVariant.Universal;
        string expected = expectsUni ? "_uni" : "a race texture (_whi, _bla, ...)";
        var matching = textures.Where(t => (t.Name.TexId == 0) == expectsUni).ToList();
        var other = textures.Except(matching).ToList();

        string message = matching.Count > 0
            ? $"{variant} drawable expects {expected}, skipped"
            : $"{variant} drawable expects {expected}, kept anyway";
        diagnostics.AddRange(other.Select(t => new Diagnostic(Severity.Warning, message, t.RelativePath)));
        return matching.Count > 0 ? matching : textures;
    }

    private static ScannedAsset? PickCloth(ScannedAsset model, IReadOnlyList<ScannedAsset> files, List<Diagnostic> diagnostics)
    {
        List<ScannedAsset> cloths = files
            .Where(f => f.Name.Kind == AssetKind.Cloth)
            .OrderBy(f => f.Name.Variant != model.Name.Variant)
            .ToList();
        if (cloths.Count == 0)
        {
            return null;
        }

        ScannedAsset cloth = cloths[0];
        if (cloth.Name.Variant != model.Name.Variant)
        {
            diagnostics.Add(new Diagnostic(Severity.Warning, $"cloth variant differs from {model.RelativePath}, used anyway", cloth.RelativePath));
        }

        diagnostics.AddRange(cloths.Skip(1).Select(f =>
            new Diagnostic(Severity.Warning, $"extra cloth file, kept {cloth.RelativePath}", f.RelativePath)));
        return cloth;
    }

    private static List<PedVariationPlan> BuildVariations(IReadOnlyList<Draft> drafts, string packName)
    {
        // Sequential index per (gender, prop?, slot); every 128 drawables start a new YMT part.
        var placed = drafts
            .GroupBy(d => (d.Gender, d.IsProp, d.SlotId))
            .SelectMany(slot => slot.Select((d, i) => (Draft: d, Part: i / MaxDrawablesPerSlot, Index: i % MaxDrawablesPerSlot)))
            .ToList();

        return placed
            .GroupBy(p => (p.Draft.Gender, p.Part))
            .OrderBy(g => g.Key.Gender)
            .ThenBy(g => g.Key.Part)
            .Select(g => new PedVariationPlan(
                g.Key.Gender,
                g.Key.Part == 0 ? packName : $"{packName}_{g.Key.Part + 1}",
                Slots(g, isProp: false),
                Slots(g, isProp: true)))
            .ToList();
    }

    private static List<SlotPlan> Slots(IEnumerable<(Draft Draft, int Part, int Index)> placed, bool isProp) =>
        placed
            .Where(p => p.Draft.IsProp == isProp)
            .GroupBy(p => p.Draft.SlotId)
            .OrderBy(g => g.Key)
            .Select(g => new SlotPlan(
                isProp,
                g.Key,
                g.OrderBy(p => p.Index)
                    .Select(p => new DrawablePlan(p.Index, p.Draft.Model.Name.Variant, p.Draft.Model, p.Draft.Cloth, p.Draft.Textures))
                    .ToList()))
            .ToList();
}
