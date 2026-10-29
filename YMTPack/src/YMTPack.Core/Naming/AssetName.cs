using YMTPack.Core.Model;

namespace YMTPack.Core.Naming;

public enum AssetKind
{
    Drawable, // .ydd
    Texture,  // .ytd
    Cloth,    // .yld
}

public enum DrawableVariant
{
    Universal, // _u, textures _uni, propMask 1
    Race,      // _r, textures _whi/_bla/..., propMask 17
}

/// <summary>A clothing stream file name broken into its parts.</summary>
/// <param name="Prefix">Text before '^' (e.g. "mp_m_freemode_01_mypack"), or null.</param>
/// <param name="PrefixGender">Gender implied by the prefix, or null.</param>
/// <param name="Variant">Component drawable/cloth u/r variant; null for textures and props.</param>
/// <param name="TextureLetter">Texture letter a-z; null for drawables and cloth.</param>
/// <param name="TexId">Race texId for component textures; null otherwise.</param>
public sealed record AssetName(
    AssetKind Kind,
    bool IsProp,
    int SlotId,
    int Number,
    string? Prefix,
    Gender? PrefixGender,
    DrawableVariant? Variant,
    char? TextureLetter,
    int? TexId);
