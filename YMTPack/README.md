# ymtpack

macOS/Linux command-line tool that turns a folder of GTA V addon clothing
(any mix of packs) into a ready FiveM resource with a freshly generated `.ymt`.

It reuses `../packages/CodeWalker.Core.dll`, the same build YMTEditor ships,
for the binary `.ymt` encoding. That DLL targets .NET Standard 2.0, so it runs
on .NET 10 on macOS and Linux.

## Build

Needs the .NET 10 SDK. Without Homebrew or sudo:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

Then:

```bash
./publish.sh                 # osx-arm64 osx-x64 linux-x64 linux-arm64
./publish.sh osx-arm64       # just one
```

Each `dist/<rid>/ymtpack` is a self-contained single file (~37 MB). The target
machine does not need .NET.

Development:

```bash
dotnet test
dotnet run --project src/YMTPack.Cli -- build ./input --name mypack --dry-run
```

## Use

```bash
ymtpack build ./input --name ephorize_clothes --out ./resources/ephorize_clothes
ymtpack build ./input --name ephorize_clothes --dry-run     # show the plan, write nothing
ymtpack ymt2xml mp_m_freemode_01_ephorize_clothes.ymt       # → .ymt.xml
ymtpack xml2ymt mp_m_freemode_01_ephorize_clothes.ymt.xml   # → .ymt
```

### Input

Put addon packs anywhere under the input folder. Nested folders are fine.
Recognized files, with or without a `something^` prefix (case-insensitive):

| What | Pattern | Example |
|---|---|---|
| Component drawable | `<comp>_NNN_<u\|r>.ydd` | `jbib_004_u.ydd` |
| Component texture | `<comp>_diff_NNN_<a-z>_<race>.ytd` | `jbib_diff_004_a_uni.ytd` |
| Cloth physics | `<comp>_NNN_<u\|r>.yld` | `jbib_004_u.yld` |
| Prop drawable | `<prop>_NNN.ydd` | `p_head_002.ydd` |
| Prop texture | `<prop>_diff_NNN_<a-z>.ytd` | `p_head_diff_002_a.ytd` |

- Components: `head berd hair uppr lowr hand feet teef accs task decl jbib`
- Props: `p_head p_eyes p_ears p_mouth p_lhand p_rhand p_lwrist p_rwrist p_hip p_lfoot p_rfoot p_ph_l_hand p_ph_r_hand`
- Races: `uni whi bla chi lat ara bal jam kor ita pak`

Other `.ydd/.ytd/.yld` names produce a warning and are skipped. Other file
types (`.ymt`, `.meta`, `fxmanifest.lua`, …) are ignored, as are dot-files and
`__MACOSX/`.

Gender comes from, in order:

1. a `mp_m_freemode_01…^` or `mp_f_freemode_01…^` prefix,
2. the closest folder whose name says it. The words `male`, `men`,
   `female`, and `women` count anywhere in a folder name (`[MALE] hoodies`,
   `Women Pack v2`), and so does `mp_f_freemode_01`. Short forms (`m`, `f`,
   `man`, `boy`, `girl`) count only when they are the whole folder name, such as
   `m/` or `[F]`, so a folder like `Man_Bun_Hair` is not treated as male.
3. `--gender male|female`.

If none of these apply to a file, the build fails and nothing is written.
Symlinked folders are reported and are not followed. Set `YMTPACK_DEBUG=1` to
get full stack traces when something unexpected fails.

### What it does

- Pairs each drawable with the textures and cloth of the same number, prefix
  and folder. Textures without a drawable, and drawables without textures,
  are skipped with a warning.
- Renumbers drawables `000…` per gender and slot across all packs, in a stable
  order (folder, then original number). It also renumbers texture letters
  `a…` with no gaps. Race texIds are kept.
- Starts a new YMT for every 128 drawables per slot (`mypack`, `mypack_2`, …),
  since one YMT holds at most 128 drawables (0–127) per component.
- Writes YMT values the same way YMTEditor does for new drawables: `_u` uses
  propMask 1, `_r` uses 17, `ownsCloth` is set when there is a `.yld`, compInfo
  and prop metadata use defaults, and the prop `texId` is the texture index.

### Output (FiveM resource)

```
ephorize_clothes/
  fxmanifest.lua                              # data_file SHOP_PED_APPAREL_META_FILE per .meta
  mp_m_freemode_01_ephorize_clothes.meta      # ShopPedApparel
  mp_f_freemode_01_ephorize_clothes.meta
  ymtpack-report.json                         # source file -> new stream name, all warnings
  stream/male/mp_m_freemode_01_ephorize_clothes.ymt
  stream/male/mp_m_freemode_01_ephorize_clothes^jbib_000_u.ydd
  stream/male/mp_m_freemode_01_ephorize_clothes^jbib_diff_000_a_uni.ytd
  stream/male/mp_m_freemode_01_p_ephorize_clothes^p_head_000.ydd
  stream/female/...
```

Add `ensure ephorize_clothes` to `server.cfg`. Use `ymtpack-report.json` to see
which original item became which drawable number.

The output folder must be missing or empty. ymtpack builds everything in a
temporary folder next to it and moves it into place only at the end.

## Heels and hats that hide hair (CreatureMetadata)

Put `heels.txt` and/or `hats.txt` in the input folder, or pass `--heels FILE` /
`--hats FILE`. Each line is `<glob> [value]`. The glob is matched against the
source `.ydd` path inside the input folder, not the new name, because you only
know the source name up front.

- `*` matches within one folder and `**` matches across folders.
- A pattern without `/` matches the file name only.
- `[` and `]` are literal.
- `#` starts a comment.
- When several lines match the same file, the last one wins.

```
# heels.txt: feet drawables only, value = heel height (default 1.0)
Women Pack v2/feet_003_u.ydd   1.5
[F] Heels/**

# hats.txt: p_head props only, value = how much hair shrinks, 0-1 (default 0.5, 1 = hide)
*p_head_002.ydd   1.0
```

For each YMT that uses these effects, ymtpack does what grzyClothTool and
YMTEditor do:

- Heels: sets compInfo `hash_07AE529D` (expressionMods) to `0 0 0 0 <height>`
  and audio `hash_2FD08CEF` to `shoe_heels`.
- Hats: sets prop `expressionMods` to `-<value> 0 0 0 0`.
- Writes `stream/<gender>/mp_creaturemetadata_<m|f>_<dlc>.ymt` (RBF format) and
  puts that name in the `.meta` `<creatureMetaData>` tag.
- Once any feet drawable in a YMT has heels, every feet drawable in that YMT
  gets an expression entry. Once any hat scales hair, every p_head gets one,
  after a single `-1` header entry. Items with value 0 are not affected.

Rules that match nothing, or that match the wrong slot (heels on a non-feet
item, hair scale on a non-p_head prop), produce warnings. Bad values stop the
build.

Hair scaling only works on hair models that have the `MH_Hair_Scale` bone. The
cfx.re tutorial says most custom hair doesn't.

`ymt2xml` and `xml2ymt` also read and write creature metadata files.

## Not done (yet)

- **Other per-item flags** such as audio for non-heels, or prop renderFlags. Edit
  the YMT with `ymt2xml`, change the XML, then run `xml2ymt`.
- `numAlternatives` is always 0. Alternate drawables (`*_1.ydd`) are skipped.
- File contents are not validated. A broken `.ydd` is copied as is.
