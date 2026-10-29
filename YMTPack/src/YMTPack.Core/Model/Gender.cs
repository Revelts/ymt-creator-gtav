namespace YMTPack.Core.Model;

public enum Gender
{
    Male,
    Female,
}

public static class GenderExtensions
{
    public static string PedName(this Gender gender) => gender switch
    {
        Gender.Male => "mp_m_freemode_01",
        Gender.Female => "mp_f_freemode_01",
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };

    /// <summary>ShopPedApparel eCharacter value (cfx.re addon clothing tutorial).</summary>
    public static string ShopCharacter(this Gender gender) => gender switch
    {
        Gender.Male => "SCR_CHAR_MULTIPLAYER",
        Gender.Female => "SCR_CHAR_MULTIPLAYER_F",
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };

    public static string Letter(this Gender gender) => gender switch
    {
        Gender.Male => "m",
        Gender.Female => "f",
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };

    public static string FolderName(this Gender gender) => gender switch
    {
        Gender.Male => "male",
        Gender.Female => "female",
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };
}
