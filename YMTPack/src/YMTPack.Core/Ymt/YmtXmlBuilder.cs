using System.Globalization;
using System.Xml.Linq;
using YMTPack.Core.Model;
using YMTPack.Core.Naming;
using YMTPack.Core.Planning;

namespace YMTPack.Core.Ymt;

/// <summary>
/// Builds CPedVariationInfo XML for a fresh addon YMT. Port of YMTEditor's
/// XMLHandler.YMTXML_Schema with the defaults YMTEditor uses for new drawables.
/// </summary>
public static class YmtXmlBuilder
{
    private const int UnusedComponent = 255;
    private const int Distribution = 255;
    private const int UniversalPropMask = 1; // _u drawables
    private const int RacePropMask = 17;     // _r drawables
    private const int ByteWrap = 256;        // numAvailTex / numAvailProps are bytes

    public static XDocument Build(PedVariationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var root = new XElement("CPedVariationInfo",
            new XAttribute("name", plan.DlcName),
            ValueElement("bHasTexVariations", false),
            ValueElement("bHasDrawblVariations", false),
            ValueElement("bHasLowLODs", false),
            ValueElement("bIsSuperLOD", false),
            new XElement("availComp", AvailComp(plan.Components)),
            new XElement("aComponentData3", new XAttribute("itemType", "CPVComponentData"),
                plan.Components.Select(ComponentData)),
            new XElement("aSelectionSets", new XAttribute("itemType", "CPedSelectionSet")),
            new XElement("compInfos", new XAttribute("itemType", "CComponentInfo"),
                plan.Components.SelectMany(c => c.Drawables.Select(d => CompInfo(c.SlotId, d)))),
            PropInfo(plan.Props),
            new XElement("dlcName", plan.DlcName));

        return new XDocument(root);
    }

    private static XElement ValueElement(string name, object value) =>
        new(name, new XAttribute("value", value));

    private static string AvailComp(IReadOnlyList<SlotPlan> components)
    {
        int[] avail = Enumerable.Repeat(UnusedComponent, PedSlots.MaxComponentSlots).ToArray();
        for (int i = 0; i < components.Count; i++)
        {
            avail[components[i].SlotId] = i;
        }

        return string.Join(' ', avail);
    }

    private static XElement ComponentData(SlotPlan component) =>
        new("Item",
            ValueElement("numAvailTex", component.Drawables.Sum(d => d.Textures.Count) % ByteWrap),
            new XElement("aDrawblData3", new XAttribute("itemType", "CPVDrawblData"),
                component.Drawables.Select(DrawableData)));

    private static XElement DrawableData(DrawablePlan drawable) =>
        new("Item",
            ValueElement("propMask", drawable.Variant == DrawableVariant.Race ? RacePropMask : UniversalPropMask),
            ValueElement("numAlternatives", 0),
            new XElement("aTexData", new XAttribute("itemType", "CPVTextureData"),
                drawable.Textures.Select(t => new XElement("Item",
                    ValueElement("texId", t.TexId),
                    ValueElement("distribution", Distribution)))),
            new XElement("clothData", ValueElement("ownsCloth", drawable.Cloth is not null)));

    // hash_2FD08CEF is the walking-sound audio id; hash_07AE529D the five expressionMods.
    // Heels: audio "shoe_heels" (cfx.re heels tutorial) and the height in the 5th value,
    // which creature metadata drives through pedCompExpressionIndex 4 (grzyClothTool).
    private static XElement CompInfo(int componentId, DrawablePlan drawable) =>
        new("Item",
            new XElement("hash_2FD08CEF", drawable.HeelHeight is null ? "none" : "shoe_heels"),
            new XElement("hash_FC507D28", "none"),
            new XElement("hash_07AE529D", ExpressionMods(f4: drawable.HeelHeight ?? 0)),
            ValueElement("flags", 0),
            new XElement("inclusions", "0"),
            new XElement("exclusions", "0"),
            new XElement("hash_6032815C", "PV_COMP_HEAD"),
            ValueElement("hash_7E103C8B", 0),
            ValueElement("hash_D12F579D", componentId),
            ValueElement("hash_FA1F27BF", drawable.Index));

    private static string ExpressionMods(float f0 = 0, float f4 = 0) =>
        string.Join(' ', new[] { f0, 0, 0, 0, f4 }.Select(v => v.ToString("0.######", CultureInfo.InvariantCulture)));

    private static XElement PropInfo(IReadOnlyList<SlotPlan> props) =>
        new("propInfo",
            ValueElement("numAvailProps", props.Sum(p => p.Drawables.Count) % ByteWrap),
            new XElement("aPropMetaData", new XAttribute("itemType", "CPedPropMetaData"),
                props.SelectMany(p => p.Drawables.Select(d => PropMetaData(p.SlotId, d)))),
            new XElement("aAnchors", new XAttribute("itemType", "CAnchorProps"),
                props.Select(p => new XElement("Item",
                    new XElement("props", string.Join(' ', p.Drawables.Select(d => d.Textures.Count))),
                    new XElement("anchor", PedSlots.PropById(p.SlotId).AnchorName)))));

    // Hats store the hair scale negated in the 1st value (pedPropExpressionIndex 0).
    private static XElement PropMetaData(int anchorId, DrawablePlan prop) =>
        new("Item",
            new XElement("audioId", "none"),
            new XElement("expressionMods", ExpressionMods(f0: prop.HairScale is { } scale ? -scale : 0)),
            new XElement("texData", new XAttribute("itemType", "CPedPropTexData"),
                prop.Textures.Select((_, i) => new XElement("Item",
                    new XElement("inclusions", "0"),
                    new XElement("exclusions", "0"),
                    ValueElement("texId", i),
                    ValueElement("inclusionId", 0),
                    ValueElement("exclusionId", 0),
                    ValueElement("distribution", Distribution)))),
            new XElement("renderFlags"),
            ValueElement("propFlags", 0),
            ValueElement("flags", 0),
            ValueElement("anchorId", anchorId),
            ValueElement("propId", prop.Index),
            ValueElement("hash_AC887A91", 0));
}
