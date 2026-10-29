using YMTPack.Core;
using YMTPack.Core.Model;
using YMTPack.Core.Output;
using YMTPack.Core.Planning;

namespace YMTPack.Cli.Commands;

public static class BuildCommand
{
    private const int MaxPrintedDiagnostics = 50;

    public static int Run(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        string input = args.Positionals.Count == 1
            ? args.Positionals[0]
            : throw new UsageException("build needs exactly one <input-dir>.");
        string name = args.Option("name") ?? throw new UsageException("build needs --name <pack>.");
        string output = args.Option("out") ?? Path.Combine(Directory.GetCurrentDirectory(), name);
        bool dryRun = args.Flag("dry-run");

        var options = new BuildOptions(
            input, output, name, ParseGender(args.Option("gender")), dryRun, args.Option("heels"), args.Option("hats"));
        BuildResult result = PackBuilder.Build(options);

        PrintDiagnostics(result.Diagnostics, stderr);
        if (result.Plan is null || result.Diagnostics.Any(d => d.Severity == Severity.Error))
        {
            stderr.WriteLine("Build failed: fix the errors above. Nothing was written.");
            return CliApp.ExitFailed;
        }

        if (result.Plan.Variations.Count == 0)
        {
            stderr.WriteLine("No usable clothing found (need .ydd drawables with matching .ytd textures).");
            return CliApp.ExitFailed;
        }

        PrintPlan(result.Plan, stdout);
        stdout.WriteLine(result.Written
            ? $"Wrote FiveM resource: {Path.GetFullPath(output)}"
            : "dry run: nothing written");
        return CliApp.ExitOk;
    }

    private static Gender? ParseGender(string? value) => value?.ToLowerInvariant() switch
    {
        null => null,
        "male" or "m" => Gender.Male,
        "female" or "f" => Gender.Female,
        _ => throw new UsageException($"--gender must be male or female (got '{value}')."),
    };

    private static void PrintDiagnostics(IReadOnlyList<Diagnostic> diagnostics, TextWriter stderr)
    {
        List<Diagnostic> ordered = diagnostics.OrderByDescending(d => d.Severity).ToList();
        foreach (Diagnostic d in ordered.Take(MaxPrintedDiagnostics))
        {
            stderr.WriteLine(d);
        }

        if (ordered.Count > MaxPrintedDiagnostics)
        {
            stderr.WriteLine($"... and {ordered.Count - MaxPrintedDiagnostics} more (all listed in {PackWriter.ReportFileName}).");
        }

        int warnings = ordered.Count(d => d.Severity == Severity.Warning);
        if (warnings > 0)
        {
            stderr.WriteLine($"{warnings} warning(s): some files were skipped or adjusted; check them before shipping.");
        }
    }

    private static void PrintPlan(PackPlan plan, TextWriter stdout)
    {
        foreach (PedVariationPlan v in plan.Variations)
        {
            string slots = string.Join(' ', v.Components.Concat(v.Props).Select(s => $"{s.SlotName}x{s.Drawables.Count}"));
            stdout.WriteLine($"{v.Gender.FolderName(),-6} {v.FullName}.ymt  {slots}");
            if (v.HasCreatureMetadata)
            {
                stdout.WriteLine($"       {v.CreatureMetadataName}.ymt  heels {v.HeelCount}, hats hiding hair {v.HairScaleCount}");
            }
        }
    }
}
