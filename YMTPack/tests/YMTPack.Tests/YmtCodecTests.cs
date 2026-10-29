using System.Xml.Linq;
using YMTPack.Core.Ymt;

namespace YMTPack.Tests;

public sealed class YmtCodecTests
{
    // Minimal YMT XML in the exact shape YMTEditor's XMLHandler writes.
    private const string MinimalYmtXml = """
        <CPedVariationInfo name="codec_test">
          <bHasTexVariations value="false" />
          <bHasDrawblVariations value="false" />
          <bHasLowLODs value="false" />
          <bIsSuperLOD value="false" />
          <availComp>255 255 255 255 255 255 255 255 255 255 255 0</availComp>
          <aComponentData3 itemType="CPVComponentData">
            <Item>
              <numAvailTex value="2" />
              <aDrawblData3 itemType="CPVDrawblData">
                <Item>
                  <propMask value="1" />
                  <numAlternatives value="0" />
                  <aTexData itemType="CPVTextureData">
                    <Item><texId value="0" /><distribution value="255" /></Item>
                    <Item><texId value="0" /><distribution value="255" /></Item>
                  </aTexData>
                  <clothData><ownsCloth value="false" /></clothData>
                </Item>
              </aDrawblData3>
            </Item>
          </aComponentData3>
          <aSelectionSets itemType="CPedSelectionSet" />
          <compInfos itemType="CComponentInfo">
            <Item>
              <hash_2FD08CEF>none</hash_2FD08CEF>
              <hash_FC507D28>none</hash_FC507D28>
              <hash_07AE529D>0 0 0 0 0</hash_07AE529D>
              <flags value="0" />
              <inclusions>0</inclusions>
              <exclusions>0</exclusions>
              <hash_6032815C>PV_COMP_HEAD</hash_6032815C>
              <hash_7E103C8B value="0" />
              <hash_D12F579D value="11" />
              <hash_FA1F27BF value="0" />
            </Item>
          </compInfos>
          <propInfo>
            <numAvailProps value="0" />
            <aPropMetaData itemType="CPedPropMetaData" />
            <aAnchors itemType="CAnchorProps" />
          </propInfo>
          <dlcName>codec_test</dlcName>
        </CPedVariationInfo>
        """;

    [Fact]
    public void XmlToYmt_ProducesRsc7Resource()
    {
        byte[] ymt = YmtCodec.XmlToYmt(XDocument.Parse(MinimalYmtXml));

        Assert.True(ymt.Length > 16);
        Assert.Equal("RSC7"u8.ToArray(), ymt[..4]);
    }

    [Fact]
    public void YmtToXml_RoundTripsComponentAndDlcData()
    {
        byte[] ymt = YmtCodec.XmlToYmt(XDocument.Parse(MinimalYmtXml));

        XDocument roundTripped = YmtCodec.YmtToXml(ymt);

        XElement root = roundTripped.Root!;
        Assert.Equal("CPedVariationInfo", root.Name.LocalName);
        Assert.Equal("codec_test", root.Element("dlcName")!.Value);
        Assert.Equal("255 255 255 255 255 255 255 255 255 255 255 0", root.Element("availComp")!.Value.Trim());
        XElement drawable = root.Element("aComponentData3")!.Element("Item")!.Element("aDrawblData3")!.Element("Item")!;
        Assert.Equal(2, drawable.Element("aTexData")!.Elements("Item").Count());
        XElement compInfo = root.Element("compInfos")!.Element("Item")!;
        Assert.Equal("11", compInfo.Element("hash_D12F579D")!.Attribute("value")!.Value);
    }

    [Theory]
    [InlineData("/x/mp_f_freemode_01_my_pack.ymt", "my_pack")]
    [InlineData("MP_M_FREEMODE_01_Abc.ymt", "abc")]
    [InlineData("custom_ped.ymt", "custom_ped")]
    public void GuessDlcName_StripsFreemodePrefix(string path, string expected)
    {
        Assert.Equal(expected, YmtCodec.GuessDlcName(path));
    }

    [Fact]
    public void YmtToXml_RejectsNonResourceBytes()
    {
        Assert.Throws<InvalidDataException>(() => YmtCodec.YmtToXml([1, 2, 3, 4, 5, 6, 7, 8]));
    }
}
