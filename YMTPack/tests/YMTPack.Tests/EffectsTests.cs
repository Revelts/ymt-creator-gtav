using YMTPack.Core;
using YMTPack.Core.Effects;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Planning;
using YMTPack.Core.Scanning;

namespace YMTPack.Tests;

public sealed class GlobTests
{
    [Theory]
    [InlineData("feet_003_u.ydd", "Women Pack v2/feet_003_u.ydd", true)]      // no '/' → file name only
    [InlineData("*feet_003_u.ydd", "p/mp_f_freemode_01_x^feet_003_u.ydd", true)]
    [InlineData("Women Pack v2/*", "Women Pack v2/feet_003_u.ydd", true)]
    [InlineData("Women Pack v2/*", "Women Pack v2/sub/feet_003_u.ydd", false)] // '*' stays in one folder
    [InlineData("heels/**", "heels/a/b/feet_000_u.ydd", true)]
    [InlineData("**/feet_00?_u.ydd", "x/y/feet_007_u.ydd", true)]
    [InlineData("[MALE] Hats/*.ydd", "[male] hats/p_head_000.ydd", true)]      // brackets literal, case-insensitive
    [InlineData("./heels/*", "heels/feet_000_u.ydd", true)]
    [InlineData("heels/*", "other/feet_000_u.ydd", false)]
    public void Matches(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, Glob.IsMatch(pattern, path));
    }
}

public sealed class EffectRulesParserTests
{
    [Fact]
    public void Parses_PatternsValuesCommentsAndSpaces()
    {
        const string text = """
            # heels for the women pack
            Women Pack v2/feet_003_u.ydd 1.5

            heels/**
            [F] Boots v2/*   0.8
            """;

        EffectRulesParseResult result = EffectRulesParser.Parse(text, "heels.txt", EffectKind.Heels);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(["Women Pack v2/feet_003_u.ydd", "heels/**", "[F] Boots v2/*"], result.Rules.Select(r => r.Pattern));
        Assert.Equal([1.5f, EffectRulesParser.DefaultHeelHeight, 0.8f], result.Rules.Select(r => r.Value));
        Assert.Equal([2, 4, 5], result.Rules.Select(r => r.Line));
    }

    [Theory]
    [InlineData(EffectKind.Heels, "x.ydd -1")]
    [InlineData(EffectKind.Heels, "x.ydd 0")]
    [InlineData(EffectKind.HairScale, "x.ydd 1.5")]
    [InlineData(EffectKind.HairScale, "x.ydd NaN")]
    public void Rejects_OutOfRangeValues(EffectKind kind, string line)
    {
        EffectRulesParseResult result = EffectRulesParser.Parse(line, "f.txt", kind);

        Assert.Empty(result.Rules);
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Path == "f.txt:1");
    }

    [Fact]
    public void Load_ReadsDefaultFilesFromInputRoot_AndRequiresExplicitOnes()
    {
        string dir = Directory.CreateTempSubdirectory("ymtpack-fx-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "hats.txt"), "p_head_000.ydd 1.0\n");

            EffectRulesLoadResult loaded = EffectRules.Load(dir, heelsFile: null, hatsFile: null);

            Assert.Empty(loaded.Rules.Heels);
            Assert.Equal(1.0f, Assert.Single(loaded.Rules.Hats).Value);
            Assert.Throws<FileNotFoundException>(() => EffectRules.Load(dir, Path.Combine(dir, "missing.txt"), null));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public sealed class EffectApplierTests
{
    private static ScannedAsset Asset(string relativePath)
    {
        AssetName name = AssetNameParser.TryParse(Path.GetFileName(relativePath))!;
        string dir = Path.GetDirectoryName(relativePath) ?? "";
        return new ScannedAsset(name, Gender.Female, "/in/" + relativePath, relativePath, $"{dir}|{name.Prefix}");
    }

    private static PackPlan Plan() => PackPlanner.Plan(
        [
            Asset("w/feet_000_u.ydd"), Asset("w/feet_diff_000_a_uni.ytd"),
            Asset("w/feet_001_u.ydd"), Asset("w/feet_diff_001_a_uni.ytd"),
            Asset("w/jbib_000_u.ydd"), Asset("w/jbib_diff_000_a_uni.ytd"),
            Asset("h/p_head_000.ydd"), Asset("h/p_head_diff_000_a.ytd"),
            Asset("h/p_eyes_000.ydd"), Asset("h/p_eyes_diff_000_a.ytd"),
        ],
        "fx");

    private static EffectRule Rule(EffectKind kind, string pattern, float value, int line = 1) =>
        new(kind, pattern, value, "rules.txt", line);

    [Fact]
    public void Heels_ApplyToMatchingFeetOnly_LaterRulesWin()
    {
        var rules = new EffectRuleSet(
            [Rule(EffectKind.Heels, "w/feet_*", 1.0f, 1), Rule(EffectKind.Heels, "feet_001_u.ydd", 2.0f, 2), Rule(EffectKind.Heels, "w/jbib_*", 1.0f, 3)],
            []);

        PackPlan applied = EffectApplier.Apply(Plan(), rules);

        SlotPlan feet = applied.Variations[0].Components.Single(c => c.SlotId == 6);
        Assert.Equal([1.0f, 2.0f], feet.Drawables.Select(d => d.HeelHeight!.Value));
        Assert.All(applied.Variations[0].Components.Single(c => c.SlotId == 11).Drawables, d => Assert.Null(d.HeelHeight));
        Assert.Contains(applied.Diagnostics, d => d.Severity == Severity.Warning && d.Path == "w/jbib_000_u.ydd");
    }

    [Fact]
    public void HairScale_AppliesToHatsOnly_AndUnmatchedRulesWarn()
    {
        var rules = new EffectRuleSet([], [Rule(EffectKind.HairScale, "h/*", 0.7f, 1), Rule(EffectKind.HairScale, "nothing/*", 0.5f, 2)]);

        PackPlan applied = EffectApplier.Apply(Plan(), rules);

        Assert.Equal(0.7f, applied.Variations[0].Props.Single(p => p.SlotId == 0).Drawables[0].HairScale);
        Assert.Null(applied.Variations[0].Props.Single(p => p.SlotId == 1).Drawables[0].HairScale);
        Assert.Contains(applied.Diagnostics, d => d.Path == "h/p_eyes_000.ydd");
        Assert.Contains(applied.Diagnostics, d => d.Path == "rules.txt:2" && d.Message.Contains("matched nothing"));
    }

    [Fact]
    public void NoRules_LeavesPlanUnchanged()
    {
        PackPlan plan = Plan();

        Assert.Same(plan, EffectApplier.Apply(plan, EffectRuleSet.Empty));
    }
}
