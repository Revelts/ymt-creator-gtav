using System.Xml.Linq;
using YMTPack.Core.FiveM;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Planning;
using YMTPack.Core.Scanning;
using YMTPack.Core.Ymt;

namespace YMTPack.Tests;

public sealed class YmtXmlBuilderTests
{
    private static ScannedAsset Asset(string file)
    {
        AssetName name = AssetNameParser.TryParse(file)!;
        return new ScannedAsset(name, Gender.Male, "/in/" + file, file, "|");
    }

    private static PedVariationPlan SamplePlan()
    {
        ScannedAsset[] assets =
        [
            Asset("jbib_000_u.ydd"), Asset("jbib_diff_000_a_uni.ytd"), Asset("jbib_diff_000_b_uni.ytd"), Asset("jbib_000_u.yld"),
            Asset("jbib_001_r.ydd"), Asset("jbib_diff_001_a_whi.ytd"),
            Asset("lowr_000_u.ydd"), Asset("lowr_diff_000_a_uni.ytd"),
            Asset("p_head_000.ydd"), Asset("p_head_diff_000_a.ytd"), Asset("p_head_diff_000_b.ytd"),
            Asset("p_lwrist_000.ydd"), Asset("p_lwrist_diff_000_a.ytd"),
        ];
        return PackPlanner.Plan(assets, "mypack").Variations.Single();
    }

    private static XElement Build() => YmtXmlBuilder.Build(SamplePlan()).Root!;

    [Fact]
    public void Header_UsesDlcName()
    {
        XElement root = Build();

        Assert.Equal("CPedVariationInfo", root.Name.LocalName);
        Assert.Equal("mypack", root.Attribute("name")!.Value);
        Assert.Equal("mypack", root.Element("dlcName")!.Value);
        Assert.Equal("false", root.Element("bHasTexVariations")!.Attribute("value")!.Value);
    }

    [Fact]
    public void AvailComp_MapsSlotIdToComponentOrder()
    {
        Assert.Equal("255 255 255 255 0 255 255 255 255 255 255 1", Build().Element("availComp")!.Value);
    }

    [Fact]
    public void ComponentData_HasDrawablesTexturesPropMaskAndCloth()
    {
        XElement[] comps = Build().Element("aComponentData3")!.Elements("Item").ToArray();
        Assert.Equal(2, comps.Length);

        XElement jbib = comps[1];
        Assert.Equal("3", jbib.Element("numAvailTex")!.Attribute("value")!.Value);
        XElement[] drawables = jbib.Element("aDrawblData3")!.Elements("Item").ToArray();
        Assert.Equal("1", drawables[0].Element("propMask")!.Attribute("value")!.Value);
        Assert.Equal("true", drawables[0].Element("clothData")!.Element("ownsCloth")!.Attribute("value")!.Value);
        Assert.Equal(2, drawables[0].Element("aTexData")!.Elements("Item").Count());
        Assert.Equal("17", drawables[1].Element("propMask")!.Attribute("value")!.Value);
        Assert.Equal("1", drawables[1].Element("aTexData")!.Element("Item")!.Element("texId")!.Attribute("value")!.Value);
    }

    [Fact]
    public void CompInfos_OnePerDrawable_WithComponentIdAndIndex()
    {
        XElement[] infos = Build().Element("compInfos")!.Elements("Item").ToArray();

        Assert.Equal(
            ["4:0", "11:0", "11:1"],
            infos.Select(i => $"{i.Element("hash_D12F579D")!.Attribute("value")!.Value}:{i.Element("hash_FA1F27BF")!.Attribute("value")!.Value}"));
        Assert.Equal("PV_COMP_HEAD", infos[0].Element("hash_6032815C")!.Value);
    }

    [Fact]
    public void PropInfo_ListsPropsAndAnchors()
    {
        XElement propInfo = Build().Element("propInfo")!;

        Assert.Equal("2", propInfo.Element("numAvailProps")!.Attribute("value")!.Value);
        XElement[] meta = propInfo.Element("aPropMetaData")!.Elements("Item").ToArray();
        Assert.Equal(["0", "6"], meta.Select(m => m.Element("anchorId")!.Attribute("value")!.Value));
        Assert.Equal(["0", "1"], meta[0].Element("texData")!.Elements("Item").Select(t => t.Element("texId")!.Attribute("value")!.Value));
        XElement[] anchors = propInfo.Element("aAnchors")!.Elements("Item").ToArray();
        Assert.Equal(["2", "1"], anchors.Select(a => a.Element("props")!.Value));
        Assert.Equal(["ANCHOR_HEAD", "ANCHOR_LEFT_WRIST"], anchors.Select(a => a.Element("anchor")!.Value));
    }

    [Fact]
    public void BuiltXml_SurvivesBinaryRoundTrip()
    {
        byte[] ymt = YmtCodec.XmlToYmt(YmtXmlBuilder.Build(SamplePlan()));
        XElement back = YmtCodec.YmtToXml(ymt).Root!;

        Assert.Equal(3, back.Descendants("compInfos").Elements("Item").Count());
        Assert.Equal(2, back.Descendants("aPropMetaData").Elements("Item").Count());
        Assert.Equal("mypack", back.Element("dlcName")!.Value);
    }
}

public sealed class FiveMResourceFilesTests
{
    [Theory]
    [InlineData(Gender.Male, "mp_m_freemode_01", "SCR_CHAR_MULTIPLAYER")]
    [InlineData(Gender.Female, "mp_f_freemode_01", "SCR_CHAR_MULTIPLAYER_F")]
    public void ShopMeta_MatchesCfxTutorialShape(Gender gender, string pedName, string character)
    {
        var plan = new PedVariationPlan(gender, "mypack_2", [], []);

        XElement root = FiveMResourceFiles.ShopPedApparel(plan).Root!;

        Assert.Equal("ShopPedApparel", root.Name.LocalName);
        Assert.Equal(pedName, root.Element("pedName")!.Value);
        Assert.Equal("mypack_2", root.Element("dlcName")!.Value);
        Assert.Equal($"{pedName}_mypack_2", root.Element("fullDlcName")!.Value);
        Assert.Equal(character, root.Element("eCharacter")!.Value);
        Assert.NotNull(root.Element("pedComponents"));
    }

    [Fact]
    public void FxManifest_RegistersEveryMetaFile()
    {
        string manifest = FiveMResourceFiles.FxManifest(["mp_m_freemode_01_a.meta", "mp_f_freemode_01_a.meta"]);

        Assert.Contains("fx_version 'cerulean'", manifest);
        Assert.Contains("game 'gta5'", manifest);
        Assert.Contains("    'mp_f_freemode_01_a.meta',", manifest);
        Assert.Contains("data_file 'SHOP_PED_APPAREL_META_FILE' 'mp_m_freemode_01_a.meta'", manifest);
        Assert.Contains("data_file 'SHOP_PED_APPAREL_META_FILE' 'mp_f_freemode_01_a.meta'", manifest);
    }
}
