using System.Globalization;
using System.Xml.Linq;
using YMTPack.Core.Planning;

namespace YMTPack.Core.Ymt;

/// <summary>
/// CCreatureMetaData that wires heel heights and hat hair scales to the ped skeleton.
/// Port of YMTEditor's XMLHandler.Creature_Schema as reused by grzyClothTool:
/// once any feet drawable has heels, every feet drawable gets an entry (expression
/// index 4); once any hat scales hair, every p_head prop gets one (expression index 0)
/// after a single "-1" header entry. Drawables with value 0 are unaffected.
/// </summary>
public static class CreatureMetadataBuilder
{
    private const int FeetComponentId = 6;
    private const int HeadPropAnchorId = 0;
    private const int HeelExpressionIndex = 4;
    private const int HairExpressionIndex = 0;
    private const int NoIndex = -1;
    private const int Tracks = 33;
    private const int HeelBoneId = 28462;
    private const int HairBoneId = 13201;
    private const int Types = 2;
    private const int Components = 1;

    public static XDocument? Build(PedVariationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.HasCreatureMetadata)
        {
            return null;
        }

        var compExpressions = new XElement("pedCompExpressions");
        if (plan.HeelCount > 0)
        {
            compExpressions.Add(Feet(plan).Select(d =>
                Expression("pedComp", FeetComponentId, d.Index, HeelExpressionIndex, HeelBoneId)));
        }

        var propExpressions = new XElement("pedPropExpressions");
        if (plan.HairScaleCount > 0)
        {
            // Every base-game file has this header entry; YMTEditor saw FiveM crash without it.
            propExpressions.Add(Expression("pedProp", HeadPropAnchorId, NoIndex, NoIndex, HairBoneId));
            propExpressions.Add(Hats(plan).Select(d =>
                Expression("pedProp", HeadPropAnchorId, d.Index, HairExpressionIndex, HairBoneId)));
        }

        return new XDocument(new XElement("CCreatureMetaData", compExpressions, propExpressions));
    }

    private static IEnumerable<DrawablePlan> Feet(PedVariationPlan plan) =>
        plan.Components.Where(c => c.SlotId == FeetComponentId).SelectMany(c => c.Drawables);

    private static IEnumerable<DrawablePlan> Hats(PedVariationPlan plan) =>
        plan.Props.Where(p => p.SlotId == HeadPropAnchorId).SelectMany(p => p.Drawables);

    private static XElement Expression(string kind, int id, int varIndex, int expressionIndex, int boneId) =>
        new("Item",
            Hex($"{kind}ID", id),
            Hex($"{kind}VarIndex", varIndex),
            Hex($"{kind}ExpressionIndex", expressionIndex),
            new XElement("tracks", new XAttribute("content", "char_array"), Tracks),
            new XElement("ids", new XAttribute("content", "short_array"), boneId),
            new XElement("types", new XAttribute("content", "char_array"), Types),
            new XElement("components", new XAttribute("content", "char_array"), Components));

    // Same "0x{0:X}" formatting as YMTEditor; -1 becomes 0xFFFFFFFF.
    private static XElement Hex(string name, int value) =>
        new(name, new XAttribute("value", "0x" + value.ToString("X", CultureInfo.InvariantCulture)));
}
