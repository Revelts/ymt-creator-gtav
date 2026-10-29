using System.Xml.Linq;
using YMTPack.Core.FiveM;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Planning;
using YMTPack.Core.Scanning;
using YMTPack.Core.Ymt;

namespace YMTPack.Tests;

public sealed class CreatureMetadataTests
{
    private static ScannedAsset Asset(string file)
    {
        AssetName name = AssetNameParser.TryParse(file)!;
        return new ScannedAsset(name, Gender.Female, "/in/" + file, file, "|");
    }

    private static PedVariationPlan Plan(float? heel = null, float? hair = null)
    {
        PedVariationPlan plan = PackPlanner.Plan(
            [
                Asset("feet_000_u.ydd"), Asset("feet_diff_000_a_uni.ytd"),
                Asset("feet_001_u.ydd"), Asset("feet_diff_001_a_uni.ytd"),
                Asset("p_head_000.ydd"), Asset("p_head_diff_000_a.ytd"),
            ],
            "cm").Variations.Single();

        return plan with
        {
            Components = plan.Components.Select(c => c with
            {
                Drawables = c.Drawables.Select(d => d.Index == 1 ? d with { HeelHeight = heel } : d).ToList(),
            }).ToList(),
            Props = plan.Props.Select(p => p with
            {
                Drawables = p.Drawables.Select(d => d with { HairScale = hair }).ToList(),
            }).ToList(),
        };
    }

    [Fact]
    public void NoEffects_NoCreatureMetadata()
    {
        PedVariationPlan plan = Plan();

        Assert.False(plan.HasCreatureMetadata);
        Assert.Null(CreatureMetadataBuilder.Build(plan));
        Assert.Equal("", FiveMResourceFiles.ShopPedApparel(plan).Root!.Element("creatureMetaData")!.Value);
    }

    [Fact]
    public void Heels_AddExpressionForEveryFeetDrawable()
    {
        XElement root = CreatureMetadataBuilder.Build(Plan(heel: 1.5f))!.Root!;

        XElement[] items = root.Element("pedCompExpressions")!.Elements("Item").ToArray();
        Assert.Equal(["0x0", "0x1"], items.Select(i => i.Element("pedCompVarIndex")!.Attribute("value")!.Value));
        Assert.All(items, i =>
        {
            Assert.Equal("0x6", i.Element("pedCompID")!.Attribute("value")!.Value);
            Assert.Equal("0x4", i.Element("pedCompExpressionIndex")!.Attribute("value")!.Value);
            Assert.Equal("28462", i.Element("ids")!.Value);
        });
        Assert.Empty(root.Element("pedPropExpressions")!.Elements());
    }

    [Fact]
    public void HairScale_AddsHeaderEntryThenOnePerHat()
    {
        XElement root = CreatureMetadataBuilder.Build(Plan(hair: 0.5f))!.Root!;

        XElement[] items = root.Element("pedPropExpressions")!.Elements("Item").ToArray();
        Assert.Equal(["0xFFFFFFFF", "0x0"], items.Select(i => i.Element("pedPropVarIndex")!.Attribute("value")!.Value));
        Assert.Equal(["0xFFFFFFFF", "0x0"], items.Select(i => i.Element("pedPropExpressionIndex")!.Attribute("value")!.Value));
        Assert.All(items, i => Assert.Equal("13201", i.Element("ids")!.Value));
        Assert.Empty(root.Element("pedCompExpressions")!.Elements());
    }

    [Fact]
    public void YmtValues_CarryHeelHeightAudioAndHairScale()
    {
        XElement ymt = YmtXmlBuilder.Build(Plan(heel: 1.5f, hair: 0.5f)).Root!;

        XElement heelInfo = ymt.Element("compInfos")!.Elements("Item").Single(i => i.Element("hash_FA1F27BF")!.Attribute("value")!.Value == "1");
        Assert.Equal("0 0 0 0 1.5", heelInfo.Element("hash_07AE529D")!.Value);
        Assert.Equal("shoe_heels", heelInfo.Element("hash_2FD08CEF")!.Value);
        XElement flatInfo = ymt.Element("compInfos")!.Elements("Item").First();
        Assert.Equal("0 0 0 0 0", flatInfo.Element("hash_07AE529D")!.Value);
        Assert.Equal("none", flatInfo.Element("hash_2FD08CEF")!.Value);
        Assert.Equal("-0.5 0 0 0 0", ymt.Descendants("aPropMetaData").Elements("Item").Single().Element("expressionMods")!.Value);
    }

    [Fact]
    public void ShopMeta_ReferencesPerGenderCreatureFile()
    {
        PedVariationPlan plan = Plan(heel: 1.0f);

        Assert.Equal("mp_creaturemetadata_f_cm", plan.CreatureMetadataName);
        Assert.Equal("mp_creaturemetadata_f_cm", FiveMResourceFiles.ShopPedApparel(plan).Root!.Element("creatureMetaData")!.Value);
    }

    [Fact]
    public void Creature_SurvivesRbfRoundTrip()
    {
        byte[] rbf = RbfCodec.XmlToRbf(CreatureMetadataBuilder.Build(Plan(heel: 1.0f, hair: 0.5f))!);

        Assert.True(RbfCodec.IsRbf(rbf));
        XElement back = RbfCodec.RbfToXml(rbf).Root!;
        Assert.Equal("CCreatureMetaData", back.Name.LocalName);
        Assert.Equal(2, back.Element("pedCompExpressions")!.Elements("Item").Count());
        Assert.Equal(2, back.Element("pedPropExpressions")!.Elements("Item").Count());
    }

    [Fact]
    public void RbfToXml_RejectsOtherBytes()
    {
        Assert.False(RbfCodec.IsRbf([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => RbfCodec.RbfToXml([1, 2, 3, 4, 5]));
    }
}
