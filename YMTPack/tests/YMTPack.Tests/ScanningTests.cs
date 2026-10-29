using YMTPack.Core;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Scanning;

namespace YMTPack.Tests;

public sealed class GenderResolverTests
{
    private static AssetName Parse(string file) => AssetNameParser.TryParse(file)!;

    [Fact]
    public void PrefixGender_WinsOverFolder()
    {
        Gender? g = GenderResolver.Resolve(Parse("mp_f_freemode_01_x^jbib_000_u.ydd"), "male/pack", fallback: null);

        Assert.Equal(Gender.Female, g);
    }

    [Theory]
    [InlineData("Female/tops", Gender.Female)]
    [InlineData("[women] cool pack", Gender.Female)]
    [InlineData("packs/MALE", Gender.Male)]
    [InlineData("men-shoes/v2", Gender.Male)]
    [InlineData("stream/mp_f_freemode_01_old", Gender.Female)]
    [InlineData("female/oops_male_subfolder", Gender.Male)] // closest folder wins
    public void FolderNames_DecideGender(string relativeDir, Gender expected)
    {
        Assert.Equal(expected, GenderResolver.Resolve(Parse("jbib_000_u.ydd"), relativeDir, fallback: null));
    }

    [Theory]
    [InlineData("female/Man_Bun_Hair", Gender.Female)] // "man" inside a name is not a gender
    [InlineData("female/Hat F", Gender.Female)]         // lone letter only counts as the whole name
    [InlineData("[M]", Gender.Male)]
    [InlineData("f", Gender.Female)]
    public void LooseWords_DoNotOverrideRealGenderFolders(string relativeDir, Gender expected)
    {
        Assert.Equal(expected, GenderResolver.Resolve(Parse("jbib_000_u.ydd"), relativeDir, fallback: null));
    }

    [Fact]
    public void FallsBackWhenNothingMatches()
    {
        Assert.Equal(Gender.Male, GenderResolver.Resolve(Parse("jbib_000_u.ydd"), "stuff/pack1", Gender.Male));
        Assert.Null(GenderResolver.Resolve(Parse("jbib_000_u.ydd"), "stuff/pack1", fallback: null));
    }
}

public sealed class PackScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ymtpack-scan-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Touch(string relativePath)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0]);
    }

    [Fact]
    public void Scan_CollectsClothingFiles_AndReportsProblems()
    {
        Touch("male/jbib_000_u.ydd");
        Touch("male/jbib_diff_000_a_uni.ytd");
        Touch("male/readme.txt");
        Touch("male/jbib_000_u_1.ydd");          // unrecognized clothing name
        Touch("unknown/lowr_000_u.ydd");         // no gender anywhere
        Touch("__MACOSX/male/._jbib_000_u.ydd"); // macOS archive junk
        Touch("male/.hidden_000_u.ydd");

        ScanResult result = PackScanner.Scan(_root, fallbackGender: null);

        Assert.Equal(2, result.Assets.Count);
        Assert.All(result.Assets, a => Assert.Equal(Gender.Male, a.Gender));
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Warning && d.Path == Path.Combine("male", "jbib_000_u_1.ydd"));
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Path == Path.Combine("unknown", "lowr_000_u.ydd"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Path?.Contains("MACOSX") == true);
    }

    [Fact]
    public void Scan_GroupsByDirectoryAndPrefix()
    {
        Touch("female/a/jbib_000_u.ydd");
        Touch("female/b/jbib_000_u.ydd");

        ScanResult result = PackScanner.Scan(_root, fallbackGender: null);

        Assert.Equal(2, result.Assets.Select(a => a.SourceGroup).Distinct().Count());
    }

    [Fact]
    public void Scan_DoesNotFollowSymlinkedFolders()
    {
        Touch("male/jbib_000_u.ydd");
        Touch("male/jbib_diff_000_a_uni.ytd");
        Directory.CreateSymbolicLink(Path.Combine(_root, "loop"), _root);

        ScanResult result = PackScanner.Scan(_root, fallbackGender: null);

        Assert.Equal(2, result.Assets.Count);
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Warning && d.Path == "loop");
    }

    [Fact]
    public void Scan_ThrowsForMissingInput()
    {
        Assert.Throws<DirectoryNotFoundException>(() => PackScanner.Scan(Path.Combine(_root, "nope"), null));
    }
}
