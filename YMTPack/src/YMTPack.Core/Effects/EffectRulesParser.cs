using System.Globalization;

namespace YMTPack.Core.Effects;

public sealed record EffectRulesParseResult(IReadOnlyList<EffectRule> Rules, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// One rule per line: "&lt;glob&gt; [value]". '#' starts a comment line. The value is the
/// last whitespace-separated token when it is a number, so paths may contain spaces.
/// </summary>
public static class EffectRulesParser
{
    /// <summary>grzyClothTool's default HighHeelsValue.</summary>
    public const float DefaultHeelHeight = 1.0f;

    /// <summary>grzyClothTool's default HairScaleValue (1.0 hides hair completely).</summary>
    public const float DefaultHairScale = 0.5f;

    public static EffectRulesParseResult Parse(string text, string fileName, EffectKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fileName);

        var rules = new List<EffectRule>();
        var diagnostics = new List<Diagnostic>();
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            (string pattern, float? value) = SplitValue(line);
            float effective = value ?? DefaultFor(kind);
            if (!IsValid(kind, effective))
            {
                diagnostics.Add(new Diagnostic(Severity.Error, $"{RangeHint(kind)} (got {effective.ToString(CultureInfo.InvariantCulture)})", $"{fileName}:{i + 1}"));
                continue;
            }

            rules.Add(new EffectRule(kind, pattern, effective, fileName, i + 1));
        }

        return new EffectRulesParseResult(rules, diagnostics);
    }

    private static (string Pattern, float? Value) SplitValue(string line)
    {
        int split = line.LastIndexOfAny([' ', '\t']);
        if (split > 0 && float.TryParse(line[(split + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return (line[..split].TrimEnd(), value);
        }

        return (line, null);
    }

    private static float DefaultFor(EffectKind kind) =>
        kind == EffectKind.Heels ? DefaultHeelHeight : DefaultHairScale;

    private static bool IsValid(EffectKind kind, float value) => kind switch
    {
        EffectKind.Heels => float.IsFinite(value) && value > 0,
        EffectKind.HairScale => float.IsFinite(value) && value > 0 && value <= 1,
        _ => false,
    };

    private static string RangeHint(EffectKind kind) =>
        kind == EffectKind.Heels ? "heel height must be a number above 0" : "hair scale must be above 0 and at most 1";
}
