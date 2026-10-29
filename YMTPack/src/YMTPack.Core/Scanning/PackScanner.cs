using YMTPack.Core.Model;
using YMTPack.Core.Naming;

namespace YMTPack.Core.Scanning;

/// <param name="SourceGroup">Files from the same folder and prefix belong to the same source pack.</param>
public sealed record ScannedAsset(
    AssetName Name,
    Gender Gender,
    string SourcePath,
    string RelativePath,
    string SourceGroup);

public sealed record ScanResult(IReadOnlyList<ScannedAsset> Assets, IReadOnlyList<Diagnostic> Diagnostics);

public static class PackScanner
{
    private static readonly HashSet<string> ClothingExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".ydd", ".ytd", ".yld" };

    public static ScanResult Scan(string inputRoot, Gender? fallbackGender)
    {
        ArgumentNullException.ThrowIfNull(inputRoot);
        string root = Path.GetFullPath(inputRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Input folder not found: {root}");
        }

        var assets = new List<ScannedAsset>();
        var diagnostics = new List<Diagnostic>();

        foreach (string path in EnumerateFiles(root, diagnostics))
        {
            string relative = Path.GetRelativePath(root, path);
            if (!ClothingExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            AssetName? name = AssetNameParser.TryParse(Path.GetFileName(path));
            if (name is null)
            {
                diagnostics.Add(new Diagnostic(Severity.Warning, "unrecognized clothing file name, skipped", relative));
                continue;
            }

            string relativeDir = Path.GetDirectoryName(relative) ?? "";
            Gender? gender = GenderResolver.Resolve(name, relativeDir, fallbackGender);
            if (gender is null)
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Error,
                    "cannot tell male/female; put it under a male/ or female/ folder or pass --gender",
                    relative));
                continue;
            }

            string group = $"{relativeDir}|{name.Prefix}";
            assets.Add(new ScannedAsset(name, gender.Value, path, relative, group));
        }

        return new ScanResult(assets, diagnostics);
    }

    // Walks the tree by hand so symlinked folders are reported instead of followed:
    // a link back up the tree would otherwise repeat every file until ELOOP.
    private static IEnumerable<string> EnumerateFiles(string root, List<Diagnostic> diagnostics)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        var files = new List<string>();
        var pending = new Stack<DirectoryInfo>([new DirectoryInfo(root)]);

        while (pending.Count > 0)
        {
            foreach (FileSystemInfo entry in pending.Pop().EnumerateFileSystemInfos("*", options))
            {
                if (IsJunk(entry.Name))
                {
                    continue;
                }

                if (entry is not DirectoryInfo folder)
                {
                    files.Add(entry.FullName);
                }
                else if (folder.LinkTarget is not null)
                {
                    diagnostics.Add(new Diagnostic(Severity.Warning, "symlinked folder not followed", Path.GetRelativePath(root, folder.FullName)));
                }
                else
                {
                    pending.Push(folder);
                }
            }
        }

        return files.Order(StringComparer.Ordinal);
    }

    // Dot-files and macOS archive metadata (__MACOSX/, ._foo resource forks).
    private static bool IsJunk(string name) => name.StartsWith('.') || name == "__MACOSX";
}
