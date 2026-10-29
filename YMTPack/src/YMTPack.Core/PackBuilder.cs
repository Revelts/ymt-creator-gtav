using YMTPack.Core.Effects;
using YMTPack.Core.Model;
using YMTPack.Core.Output;
using YMTPack.Core.Planning;
using YMTPack.Core.Scanning;

namespace YMTPack.Core;

/// <param name="FallbackGender">Gender for files whose prefix and folders don't say.</param>
/// <param name="HeelsFile">Heels rules; defaults to heels.txt in the input folder when present.</param>
/// <param name="HatsFile">Hat hair-scale rules; defaults to hats.txt in the input folder when present.</param>
public sealed record BuildOptions(
    string InputDir,
    string OutputDir,
    string PackName,
    Gender? FallbackGender,
    bool DryRun,
    string? HeelsFile = null,
    string? HatsFile = null);

/// <param name="Plan">Null when scanning found errors.</param>
public sealed record BuildResult(PackPlan? Plan, IReadOnlyList<Diagnostic> Diagnostics, bool Written);

/// <summary>Scan → plan → write. Nothing is written when there are errors or on a dry run.</summary>
public static class PackBuilder
{
    public static BuildResult Build(BuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        PackPlanner.ValidatePackName(options.PackName);

        string input = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.InputDir));
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.OutputDir));
        if (IsSameOrInside(output, input))
        {
            throw new ArgumentException("Output folder must not be inside the input folder.", nameof(options));
        }

        ScanResult scan = PackScanner.Scan(input, options.FallbackGender);
        EffectRulesLoadResult effects = EffectRules.Load(input, options.HeelsFile, options.HatsFile);
        Diagnostic[] early = [.. scan.Diagnostics, .. effects.Diagnostics];
        if (early.Any(d => d.Severity == Severity.Error))
        {
            return new BuildResult(null, early, Written: false);
        }

        PackPlan plan = EffectApplier.Apply(PackPlanner.Plan(scan.Assets, options.PackName), effects.Rules);
        Diagnostic[] diagnostics = [.. early, .. plan.Diagnostics];
        if (options.DryRun || plan.Variations.Count == 0)
        {
            return new BuildResult(plan, diagnostics, Written: false);
        }

        PackWriter.Write(plan, diagnostics, output);
        return new BuildResult(plan, diagnostics, Written: true);
    }

    private static bool IsSameOrInside(string path, string folder)
    {
        // macOS (APFS default) and Windows file systems are case-insensitive.
        StringComparison comparison = OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.Equals(folder, comparison)
            || path.StartsWith(folder + Path.DirectorySeparatorChar, comparison);
    }
}
