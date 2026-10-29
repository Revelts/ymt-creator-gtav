namespace YMTPack.Core.Effects;

public enum EffectKind
{
    Heels,     // feet drawables lift the ped
    HairScale, // p_head props shrink the hair under them
}

/// <param name="Pattern">Glob on the source .ydd path relative to the input folder.</param>
/// <param name="SourceFile">Rules file label used in diagnostics.</param>
public sealed record EffectRule(EffectKind Kind, string Pattern, float Value, string SourceFile, int Line)
{
    public string Location => $"{SourceFile}:{Line}";
}

public sealed record EffectRuleSet(IReadOnlyList<EffectRule> Heels, IReadOnlyList<EffectRule> Hats)
{
    public static readonly EffectRuleSet Empty = new([], []);

    public bool IsEmpty => Heels.Count == 0 && Hats.Count == 0;
}

public sealed record EffectRulesLoadResult(EffectRuleSet Rules, IReadOnlyList<Diagnostic> Diagnostics);

public static class EffectRules
{
    public const string DefaultHeelsFile = "heels.txt";
    public const string DefaultHatsFile = "hats.txt";

    /// <summary>
    /// Reads heels/hats rules. Explicit files must exist; otherwise heels.txt / hats.txt
    /// in the input folder are used when present.
    /// </summary>
    public static EffectRulesLoadResult Load(string inputDir, string? heelsFile, string? hatsFile)
    {
        ArgumentNullException.ThrowIfNull(inputDir);

        EffectRulesParseResult heels = Read(inputDir, heelsFile, DefaultHeelsFile, EffectKind.Heels);
        EffectRulesParseResult hats = Read(inputDir, hatsFile, DefaultHatsFile, EffectKind.HairScale);
        return new EffectRulesLoadResult(
            new EffectRuleSet(heels.Rules, hats.Rules),
            [.. heels.Diagnostics, .. hats.Diagnostics]);
    }

    private static EffectRulesParseResult Read(string inputDir, string? explicitFile, string defaultName, EffectKind kind)
    {
        if (explicitFile is not null)
        {
            if (!File.Exists(explicitFile))
            {
                throw new FileNotFoundException($"Rules file not found: {explicitFile}", explicitFile);
            }

            return EffectRulesParser.Parse(File.ReadAllText(explicitFile), explicitFile, kind);
        }

        string path = Path.Combine(inputDir, defaultName);
        return File.Exists(path)
            ? EffectRulesParser.Parse(File.ReadAllText(path), defaultName, kind)
            : new EffectRulesParseResult([], []);
    }
}
