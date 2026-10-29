using YMTPack.Core.Model;
using YMTPack.Core.Naming;

namespace YMTPack.Tests;

public sealed class AssetNameParserTests
{
    [Fact]
    public void Parses_UnprefixedComponentDrawable()
    {
        AssetName? name = AssetNameParser.TryParse("jbib_007_u.ydd");

        Assert.NotNull(name);
        Assert.Equal(AssetKind.Drawable, name.Kind);
        Assert.False(name.IsProp);
        Assert.Equal(11, name.SlotId);
        Assert.Equal(7, name.Number);
        Assert.Equal(DrawableVariant.Universal, name.Variant);
        Assert.Null(name.Prefix);
        Assert.Null(name.PrefixGender);
    }

    [Fact]
    public void Parses_PrefixedRaceDrawable_AndGenderFromPrefix()
    {
        AssetName? name = AssetNameParser.TryParse("mp_f_freemode_01_some_pack^LOWR_012_R.ydd");

        Assert.NotNull(name);
        Assert.Equal(4, name.SlotId);
        Assert.Equal(12, name.Number);
        Assert.Equal(DrawableVariant.Race, name.Variant);
        Assert.Equal("mp_f_freemode_01_some_pack", name.Prefix);
        Assert.Equal(Gender.Female, name.PrefixGender);
    }

    [Fact]
    public void Parses_ComponentTexture_WithRace()
    {
        AssetName? name = AssetNameParser.TryParse("mp_m_freemode_01^jbib_diff_003_c_whi.ytd");

        Assert.NotNull(name);
        Assert.Equal(AssetKind.Texture, name.Kind);
        Assert.Equal(11, name.SlotId);
        Assert.Equal(3, name.Number);
        Assert.Equal('c', name.TextureLetter);
        Assert.Equal(1, name.TexId);
        Assert.Equal(Gender.Male, name.PrefixGender);
    }

    [Fact]
    public void Parses_ClothPhysics()
    {
        AssetName? name = AssetNameParser.TryParse("accs_001_u.yld");

        Assert.NotNull(name);
        Assert.Equal(AssetKind.Cloth, name.Kind);
        Assert.Equal(8, name.SlotId);
    }

    [Fact]
    public void Parses_PropDrawableAndTexture()
    {
        AssetName? drawable = AssetNameParser.TryParse("mp_m_freemode_01_p_hats^p_head_004.ydd");
        AssetName? texture = AssetNameParser.TryParse("p_rwrist_diff_010_b.ytd");

        Assert.NotNull(drawable);
        Assert.True(drawable.IsProp);
        Assert.Equal(0, drawable.SlotId);
        Assert.Equal(4, drawable.Number);
        Assert.Equal(Gender.Male, drawable.PrefixGender);

        Assert.NotNull(texture);
        Assert.True(texture.IsProp);
        Assert.Equal(AssetKind.Texture, texture.Kind);
        Assert.Equal(7, texture.SlotId);
        Assert.Equal('b', texture.TextureLetter);
    }

    [Theory]
    [InlineData("jbib_000_u.ytd")]          // texture extension with drawable shape
    [InlineData("jbib_diff_000_a_xyz.ytd")] // unknown race
    [InlineData("shoe_000_u.ydd")]          // unknown component
    [InlineData("p_head_000_u.ydd")]        // props have no u/r variant
    [InlineData("jbib_0001_u.ydd")]         // four-digit number
    [InlineData("fxmanifest.lua")]
    [InlineData("mp_m_freemode_01_pack.ymt")]
    public void Rejects_UnrecognizedNames(string fileName)
    {
        Assert.Null(AssetNameParser.TryParse(fileName));
    }
}
