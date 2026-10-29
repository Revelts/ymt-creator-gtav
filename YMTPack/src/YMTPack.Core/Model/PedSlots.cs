namespace YMTPack.Core.Model;

public sealed record ComponentSlot(int Id, string Name);

public sealed record PropAnchor(int Id, string Name, string AnchorName);

/// <summary>
/// Component, prop and texture-race tables. Ids and anchor names follow
/// YMTEditor's YMTTypes enums and XMLHandler aAnchors naming.
/// </summary>
public static class PedSlots
{
    public const int MaxComponentSlots = 12;

    public static readonly IReadOnlyList<ComponentSlot> Components =
    [
        new(0, "head"),
        new(1, "berd"),
        new(2, "hair"),
        new(3, "uppr"),
        new(4, "lowr"),
        new(5, "hand"),
        new(6, "feet"),
        new(7, "teef"),
        new(8, "accs"),
        new(9, "task"),
        new(10, "decl"),
        new(11, "jbib"),
    ];

    public static readonly IReadOnlyList<PropAnchor> Props =
    [
        new(0, "p_head", "ANCHOR_HEAD"),
        new(1, "p_eyes", "ANCHOR_EYES"),
        new(2, "p_ears", "ANCHOR_EARS"),
        new(3, "p_mouth", "ANCHOR_MOUTH"),
        new(4, "p_lhand", "ANCHOR_LEFT_HAND"),
        new(5, "p_rhand", "ANCHOR_RIGHT_HAND"),
        new(6, "p_lwrist", "ANCHOR_LEFT_WRIST"),
        new(7, "p_rwrist", "ANCHOR_RIGHT_WRIST"),
        new(8, "p_hip", "ANCHOR_HIP"),
        new(9, "p_lfoot", "ANCHOR_LEFT_FOOT"),
        new(10, "p_rfoot", "ANCHOR_RIGHT_FOOT"),
        new(11, "p_ph_l_hand", "ANCHOR_PH_L_HAND"),
        new(12, "p_ph_r_hand", "ANCHOR_PH_R_HAND"),
    ];

    /// <summary>Texture race suffix to texId, as listed in YMTEditor's texId dropdown.</summary>
    public static readonly IReadOnlyDictionary<string, int> RaceTexIds = new Dictionary<string, int>
    {
        ["uni"] = 0,
        ["whi"] = 1,
        ["bla"] = 2,
        ["chi"] = 3,
        ["lat"] = 4,
        ["ara"] = 5,
        ["bal"] = 6,
        ["jam"] = 7,
        ["kor"] = 8,
        ["ita"] = 9,
        ["pak"] = 10,
    };

    public static ComponentSlot ComponentById(int id) => Components[id];

    public static PropAnchor PropById(int id) => Props[id];

    public static string SlotName(bool isProp, int slotId) =>
        isProp ? PropById(slotId).Name : ComponentById(slotId).Name;
}
