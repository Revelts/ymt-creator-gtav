using System.Text.Json;
using YMTPack.Core.FiveM;
using YMTPack.Core.Model;
using YMTPack.Core.Planning;
using YMTPack.Core.Ymt;

namespace YMTPack.Core.Output;

/// <summary>
/// Writes a FiveM resource:
///   fxmanifest.lua, {ped}_{dlc}.meta, ymtpack-report.json,
///   stream/{male|female}/{ped}_{dlc}.ymt and renamed .ydd/.ytd/.yld files.
/// Everything is staged in a sibling temp folder and moved into place at the end.
/// </summary>
public static class PackWriter
{
    public const string ReportFileName = "ymtpack-report.json";
    private const string StreamFolder = "stream";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record ReportFile(string Source, string Target);

    private sealed record ReportVariation(
        string Gender, string DlcName, string Ymt, int Drawables, int Props, string? CreatureMetadata, int Heels, int HairScaled);

    private sealed record Report(
        string Pack,
        DateTimeOffset GeneratedAt,
        IReadOnlyList<ReportVariation> Ymts,
        IReadOnlyList<ReportFile> Files,
        IReadOnlyList<string> Diagnostics);

    public static void Write(PackPlan plan, IReadOnlyList<Diagnostic> diagnostics, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(diagnostics);
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDir));
        EnsureWritableTarget(output);

        string parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Output needs a parent folder.", nameof(outputDir));
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".{Path.GetFileName(output)}.ymtpack-tmp-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(staging);
            WriteResource(plan, diagnostics, staging);
            if (Directory.Exists(output))
            {
                Directory.Delete(output); // verified empty above; non-recursive on purpose
            }

            Directory.Move(staging, output);
        }
        finally
        {
            RemoveStaging(staging);
        }
    }

    // Best effort: a failed cleanup must not hide the error that got us here.
    private static void RemoveStaging(string staging)
    {
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"warning: could not remove temporary folder {staging}: {ex.Message}");
        }
    }

    private static void EnsureWritableTarget(string output)
    {
        if (File.Exists(output))
        {
            throw new IOException($"Output path is a file: {output}");
        }

        if (new DirectoryInfo(output).LinkTarget is not null)
        {
            throw new IOException($"Output path is a symlink; pass the real folder instead: {output}");
        }

        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            throw new IOException($"Output folder is not empty: {output}");
        }
    }

    private static void WriteResource(PackPlan plan, IReadOnlyList<Diagnostic> diagnostics, string root)
    {
        var files = new List<ReportFile>();
        var ymts = new List<ReportVariation>();

        foreach (PedVariationPlan variation in plan.Variations)
        {
            string relativeDir = Path.Combine(StreamFolder, variation.Gender.FolderName());
            string streamDir = Path.Combine(root, relativeDir);
            Directory.CreateDirectory(streamDir);

            foreach (StreamFile file in variation.StreamFiles())
            {
                File.Copy(file.Source.SourcePath, Path.Combine(streamDir, file.TargetFileName), overwrite: false);
                files.Add(new ReportFile(file.Source.RelativePath, ReportPath(relativeDir, file.TargetFileName)));
            }

            string ymtName = variation.FullName + ".ymt";
            File.WriteAllBytes(Path.Combine(streamDir, ymtName), YmtCodec.XmlToYmt(YmtXmlBuilder.Build(variation)));
            XmlFiles.SaveWithoutBom(FiveMResourceFiles.ShopPedApparel(variation), Path.Combine(root, FiveMResourceFiles.ShopMetaFileName(variation)));
            string? creature = WriteCreatureMetadata(variation, streamDir);

            ymts.Add(new ReportVariation(
                variation.Gender.FolderName(),
                variation.DlcName,
                ReportPath(relativeDir, ymtName),
                variation.Components.Sum(c => c.Drawables.Count),
                variation.Props.Sum(p => p.Drawables.Count),
                creature is null ? null : ReportPath(relativeDir, creature),
                variation.HeelCount,
                variation.HairScaleCount));
        }

        string[] metas = plan.Variations.Select(FiveMResourceFiles.ShopMetaFileName).ToArray();
        File.WriteAllText(Path.Combine(root, "fxmanifest.lua"), FiveMResourceFiles.FxManifest(metas));

        var report = new Report(plan.PackName, DateTimeOffset.UtcNow, ymts, files, diagnostics.Select(d => d.ToString()).ToArray());
        File.WriteAllText(Path.Combine(root, ReportFileName), JsonSerializer.Serialize(report, JsonOptions));
    }

    /// <returns>The written file name, or null when the YMT needs no creature metadata.</returns>
    private static string? WriteCreatureMetadata(PedVariationPlan variation, string streamDir)
    {
        if (CreatureMetadataBuilder.Build(variation) is not { } creature)
        {
            return null;
        }

        string fileName = variation.CreatureMetadataName + ".ymt";
        File.WriteAllBytes(Path.Combine(streamDir, fileName), RbfCodec.XmlToRbf(creature));
        return fileName;
    }

    // Report paths always use '/', so the report reads the same on every OS.
    private static string ReportPath(string relativeDir, string fileName) =>
        Path.Combine(relativeDir, fileName).Replace(Path.DirectorySeparatorChar, '/');
}
