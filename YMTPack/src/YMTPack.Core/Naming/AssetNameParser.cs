using System.Text.RegularExpressions;
using YMTPack.Core.Model;

namespace YMTPack.Core.Naming;

/// <summary>
/// Parses GTA V ped clothing stream file names, with or without a "prefix^" part:
///   jbib_000_u.ydd, jbib_diff_000_a_uni.ytd, jbib_000_u.yld,
///   p_head_000.ydd, p_head_diff_000_a.ytd
/// </summary>
public static class AssetNameParser
{
    private const string PrefixPattern = @"^(?:(?<prefix>[^\^]+)\^)?";

    private static readonly string ComponentSlots =
        string.Join('|', PedSlots.Components.Select(c => c.Name));

    // Longest names first so "p_ph_l_hand" is not shadowed by a shorter alternative.
    private static readonly string PropSlots =
        string.Join('|', PedSlots.Props.Select(p => p.Name).OrderByDescending(n => n.Length));

    private static readonly string Races = string.Join('|', PedSlots.RaceTexIds.Keys);

    private static readonly Regex ComponentModel = new(
        $@"{PrefixPattern}(?<slot>{ComponentSlots})_(?<num>\d{{3}})_(?<variant>[ur])\.(?<ext>ydd|yld)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ComponentTexture = new(
        $@"{PrefixPattern}(?<slot>{ComponentSlots})_diff_(?<num>\d{{3}})_(?<letter>[a-z])_(?<race>{Races})\.ytd$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PropModel = new(
        $@"{PrefixPattern}(?<slot>{PropSlots})_(?<num>\d{{3}})\.ydd$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PropTexture = new(
        $@"{PrefixPattern}(?<slot>{PropSlots})_diff_(?<num>\d{{3}})_(?<letter>[a-z])\.ytd$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GenderPrefix = new(
        @"^mp_(?<g>[mf])_freemode_01(?:_|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static AssetName? TryParse(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        string name = fileName.ToLowerInvariant();

        Match m = ComponentModel.Match(name);
        if (m.Success)
        {
            AssetKind kind = m.Groups["ext"].Value == "yld" ? AssetKind.Cloth : AssetKind.Drawable;
            DrawableVariant variant = m.Groups["variant"].Value == "r" ? DrawableVariant.Race : DrawableVariant.Universal;
            return Build(m, kind, isProp: false, ComponentId(m), variant, letter: null, texId: null);
        }

        m = ComponentTexture.Match(name);
        if (m.Success)
        {
            int texId = PedSlots.RaceTexIds[m.Groups["race"].Value];
            return Build(m, AssetKind.Texture, isProp: false, ComponentId(m), variant: null, Letter(m), texId);
        }

        m = PropModel.Match(name);
        if (m.Success)
        {
            return Build(m, AssetKind.Drawable, isProp: true, PropId(m), variant: null, letter: null, texId: null);
        }

        m = PropTexture.Match(name);
        if (m.Success)
        {
            return Build(m, AssetKind.Texture, isProp: true, PropId(m), variant: null, Letter(m), texId: null);
        }

        return null;
    }

    private static AssetName Build(
        Match m, AssetKind kind, bool isProp, int slotId, DrawableVariant? variant, char? letter, int? texId)
    {
        string? prefix = m.Groups["prefix"].Success ? m.Groups["prefix"].Value : null;
        int number = int.Parse(m.Groups["num"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return new AssetName(kind, isProp, slotId, number, prefix, GenderOf(prefix), variant, letter, texId);
    }

    private static int ComponentId(Match m) =>
        PedSlots.Components.First(c => c.Name == m.Groups["slot"].Value).Id;

    private static int PropId(Match m) =>
        PedSlots.Props.First(p => p.Name == m.Groups["slot"].Value).Id;

    private static char Letter(Match m) => m.Groups["letter"].Value[0];

    private static Gender? GenderOf(string? prefix)
    {
        if (prefix is null)
        {
            return null;
        }

        Match g = GenderPrefix.Match(prefix);
        if (!g.Success)
        {
            return null;
        }

        return g.Groups["g"].Value == "m" ? Gender.Male : Gender.Female;
    }
}
