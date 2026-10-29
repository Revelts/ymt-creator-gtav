using YMTPack.Core;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Planning;
using YMTPack.Core.Scanning;

namespace YMTPack.Tests;

public sealed class PackPlannerTests
{
    private static ScannedAsset Asset(string relativePath, Gender gender = Gender.Male)
    {
        string file = Path.GetFileName(relativePath);
        AssetName name = AssetNameParser.TryParse(file) ?? throw new ArgumentException(file);
        string dir = Path.GetDirectoryName(relativePath) ?? "";
        return new ScannedAsset(name, gender, "/in/" + relativePath, relativePath, $"{dir}|{name.Prefix}");
    }

    private static IEnumerable<ScannedAsset> Drawable(string dir, string slot, int number, params string[] letters)
    {
        yield return Asset($"{dir}/{slot}_{number:000}_u.ydd");
        foreach (string letter in letters)
        {
            yield return Asset($"{dir}/{slot}_diff_{number:000}_{letter}_uni.ytd");
        }
    }

    [Fact]
    public void Merges_SourcePacks_IntoSequentialIndices()
    {
        ScannedAsset[] assets =
        [
            .. Drawable("packB", "jbib", 0, "a"),
            .. Drawable("packA", "jbib", 5, "a"),
            .. Drawable("packA", "jbib", 2, "a"),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        PedVariationPlan male = Assert.Single(plan.Variations);
        SlotPlan jbib = Assert.Single(male.Components);
        Assert.Equal([0, 1, 2], jbib.Drawables.Select(d => d.Index));
        Assert.Equal(["packA/jbib_002_u.ydd", "packA/jbib_005_u.ydd", "packB/jbib_000_u.ydd"],
            jbib.Drawables.Select(d => d.Source.RelativePath));
    }

    [Fact]
    public void Renumbers_TextureLetters_AndKeepsRaceTexId()
    {
        ScannedAsset[] assets =
        [
            Asset("p/jbib_000_r.ydd"),
            Asset("p/jbib_diff_000_c_bla.ytd"),
            Asset("p/jbib_diff_000_a_whi.ytd"),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        DrawablePlan d = plan.Variations[0].Components[0].Drawables[0];
        Assert.Equal(DrawableVariant.Race, d.Variant);
        Assert.Equal(['a', 'b'], d.Textures.Select(t => t.Letter));
        Assert.Equal([1, 2], d.Textures.Select(t => t.TexId));
    }

    [Fact]
    public void Cloth_MarksDrawableAsOwningCloth()
    {
        ScannedAsset[] assets = [.. Drawable("p", "accs", 0, "a"), Asset("p/accs_000_u.yld")];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        DrawablePlan d = plan.Variations[0].Components[0].Drawables[0];
        Assert.NotNull(d.Cloth);
    }

    [Fact]
    public void Skips_DrawablesWithoutTextures_AndOrphanTextures()
    {
        ScannedAsset[] assets =
        [
            Asset("p/jbib_000_u.ydd"),
            Asset("p/lowr_diff_003_a_uni.ytd"),
            .. Drawable("p", "feet", 1, "a"),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        SlotPlan only = Assert.Single(plan.Variations[0].Components);
        Assert.Equal(6, only.SlotId);
        Assert.Contains(plan.Diagnostics, d => d.Severity == Severity.Warning && d.Path == "p/jbib_000_u.ydd");
        Assert.Contains(plan.Diagnostics, d => d.Severity == Severity.Warning && d.Path == "p/lowr_diff_003_a_uni.ytd");
    }

    [Fact]
    public void Splits_IntoExtraYmts_Past128DrawablesPerSlot()
    {
        ScannedAsset[] assets = Enumerable.Range(0, 130).SelectMany(i => Drawable("p", "jbib", i % 1000, "a")).ToArray();

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        Assert.Equal(["mypack", "mypack_2"], plan.Variations.Select(v => v.DlcName));
        Assert.Equal(128, plan.Variations[0].Components[0].Drawables.Count);
        Assert.Equal([0, 1], plan.Variations[1].Components[0].Drawables.Select(d => d.Index));
    }

    [Fact]
    public void SeparatesGenders_AndNamesStreamFiles()
    {
        ScannedAsset[] assets =
        [
            Asset("m/jbib_000_u.ydd"),
            Asset("m/jbib_diff_000_a_uni.ytd"),
            Asset("m/jbib_000_u.yld"),
            Asset("f/p_head_003.ydd", Gender.Female),
            Asset("f/p_head_diff_003_a.ytd", Gender.Female),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        PedVariationPlan male = plan.Variations.Single(v => v.Gender == Gender.Male);
        PedVariationPlan female = plan.Variations.Single(v => v.Gender == Gender.Female);
        Assert.Equal("mp_m_freemode_01_mypack", male.FullName);
        Assert.Equal(
            ["mp_m_freemode_01_mypack^jbib_000_u.ydd", "mp_m_freemode_01_mypack^jbib_000_u.yld", "mp_m_freemode_01_mypack^jbib_diff_000_a_uni.ytd"],
            male.StreamFiles().Select(f => f.TargetFileName));
        Assert.Equal(
            ["mp_f_freemode_01_p_mypack^p_head_000.ydd", "mp_f_freemode_01_p_mypack^p_head_diff_000_a.ytd"],
            female.StreamFiles().Select(f => f.TargetFileName));
    }

    [Fact]
    public void KeepsFirstDrawable_WhenUniversalAndRaceShareANumber()
    {
        ScannedAsset[] assets = [.. Drawable("p", "jbib", 0, "a"), Asset("p/jbib_000_r.ydd")];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        Assert.Single(plan.Variations[0].Components[0].Drawables);
        Assert.Contains(plan.Diagnostics, d => d.Path == "p/jbib_000_r.ydd");
    }

    [Fact]
    public void Warns_OnDuplicateTextureLetter_KeepingOne()
    {
        ScannedAsset[] assets =
        [
            Asset("p/jbib_000_r.ydd"),
            Asset("p/jbib_diff_000_a_whi.ytd"),
            Asset("p/jbib_diff_000_a_bla.ytd"),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        Assert.Single(plan.Variations[0].Components[0].Drawables[0].Textures);
        Assert.Contains(plan.Diagnostics, d => d.Message.Contains("duplicate texture letter"));
    }

    [Fact]
    public void UniversalAndRaceSharingANumber_KeepOnlyUniversalTextures()
    {
        ScannedAsset[] assets =
        [
            Asset("p/jbib_000_u.ydd"), Asset("p/jbib_diff_000_a_uni.ytd"),
            Asset("p/jbib_000_r.ydd"), Asset("p/jbib_diff_000_a_whi.ytd"), Asset("p/jbib_diff_000_b_whi.ytd"),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        DrawablePlan d = plan.Variations[0].Components[0].Drawables[0];
        Assert.Equal(DrawableVariant.Universal, d.Variant);
        TexturePlan only = Assert.Single(d.Textures);
        Assert.Equal(0, only.TexId);
        Assert.Contains(plan.Diagnostics, x => x.Path == "p/jbib_diff_000_b_whi.ytd");
    }

    [Fact]
    public void Cloth_PrefersMatchingVariant_AndWarnsOnExtras()
    {
        ScannedAsset[] assets =
        [
            .. Drawable("p", "jbib", 0, "a"),
            Asset("p/jbib_000_r.yld"),
            Asset("p/jbib_000_u.yld"),
        ];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        Assert.Equal("p/jbib_000_u.yld", plan.Variations[0].Components[0].Drawables[0].Cloth!.RelativePath);
        Assert.Contains(plan.Diagnostics, x => x.Path == "p/jbib_000_r.yld");
    }

    [Fact]
    public void Warns_WhenUniversalDrawableHasRaceTexture()
    {
        ScannedAsset[] assets = [Asset("p/jbib_000_u.ydd"), Asset("p/jbib_diff_000_a_whi.ytd")];

        PackPlan plan = PackPlanner.Plan(assets, "mypack");

        Assert.Single(plan.Variations);
        Assert.Contains(plan.Diagnostics, d => d.Path == "p/jbib_diff_000_a_whi.ytd" && d.Message.Contains("_uni"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("My Pack")]
    [InlineData("UPPER")]
    [InlineData("a_name_that_is_way_too_long_for_a_dlc")]
    public void Rejects_InvalidPackNames(string packName)
    {
        Assert.Throws<ArgumentException>(() => PackPlanner.Plan([], packName));
    }
}
