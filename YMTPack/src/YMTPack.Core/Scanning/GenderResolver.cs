using YMTPack.Core.Model;
using YMTPack.Core.Naming;

namespace YMTPack.Core.Scanning;

/// <summary>
/// Decides the ped gender of a clothing file: "mp_X_freemode_01" prefix first,
/// then the closest input folder whose name says male/female, then the fallback.
/// </summary>
public static class GenderResolver
{
    // Words that mean a gender anywhere in a folder name ("[MALE] hoodies", "Women Pack v2").
    private static readonly HashSet<string> FemaleWords = ["female", "females", "women", "womens"];
    private static readonly HashSet<string> MaleWords = ["male", "males", "men", "mens"];

    // Short forms that only count as the whole folder name ("f", "[M]"), not "Hat F" or "Man_Bun_Hair".
    private static readonly HashSet<string> FemaleNames = ["f", "mpf", "woman", "girl", "girls"];
    private static readonly HashSet<string> MaleNames = ["m", "mpm", "man", "boy", "boys"];

    public static Gender? Resolve(AssetName name, string relativeDirectory, Gender? fallback)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(relativeDirectory);

        if (name.PrefixGender is { } prefixGender)
        {
            return prefixGender;
        }

        string[] folders = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string folder in folders.Reverse())
        {
            if (FromFolderName(folder) is { } folderGender)
            {
                return folderGender;
            }
        }

        return fallback;
    }

    private static Gender? FromFolderName(string folder)
    {
        string lower = folder.ToLowerInvariant();
        if (lower.Contains("mp_f_freemode_01", StringComparison.Ordinal))
        {
            return Gender.Female;
        }

        if (lower.Contains("mp_m_freemode_01", StringComparison.Ordinal))
        {
            return Gender.Male;
        }

        string[] tokens = lower.Split(
            lower.Where(c => !char.IsAsciiLetterOrDigit(c)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries);
        string? wholeName = tokens.Length == 1 ? tokens[0] : null;

        bool female = tokens.Any(FemaleWords.Contains) || (wholeName is not null && FemaleNames.Contains(wholeName));
        bool male = tokens.Any(MaleWords.Contains) || (wholeName is not null && MaleNames.Contains(wholeName));
        return (female, male) switch
        {
            (true, false) => Gender.Female,
            (false, true) => Gender.Male,
            _ => null,
        };
    }
}
