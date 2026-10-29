# ymt-creator-gtav

`ymtpack` is a macOS and Linux command-line tool for packing GTA V addon
clothing. Point it at a folder of clothing packs and it writes a ready-to-use
FiveM resource that contains a new `.ymt`.

```
input/ (any mix of packs)                     my_clothes/ (FiveM resource)
├── [MALE] Hoodies/jbib_000_u.ydd      ──▶    ├── fxmanifest.lua
├── [MALE] Hoodies/jbib_diff_000_a_uni.ytd    ├── mp_m_freemode_01_my_clothes.meta
├── Women Pack v2/feet_003_u.ydd              ├── mp_f_freemode_01_my_clothes.meta
├── Women Pack v2/feet_diff_003_a_uni.ytd     ├── ymtpack-report.json
├── heels.txt  (optional)                     └── stream/
└── hats.txt   (optional)                         ├── male/   *.ymt, *.ydd, *.ytd, *.yld
                                                  └── female/ *.ymt, *.ydd, *.ytd, creaturemetadata
```

## Features

- Merges any number of addon packs. Gender is detected automatically.
- Renumbers drawables `000…` for each gender and slot, and renumbers texture
  letters `a…` so there are no gaps.
- Renames everything to FiveM stream names, for example
  `mp_m_freemode_01_<pack>^jbib_000_u.ydd`. Props use
  `mp_m_freemode_01_p_<pack>^…`.
- Generates the ped variation `.ymt`, the `ShopPedApparel` `.meta` files, and
  `fxmanifest.lua`.
- Starts a new YMT for every 128 drawables per slot (`pack`, `pack_2`, …).
- Optional **heels** and **hats that hide hair**, using creature metadata.
- Converts `.ymt` ↔ XML so you can edit a YMT by hand.
- Self-contained binaries for macOS (arm64/x64) and Linux (x64/arm64). The
  machine that runs them does not need .NET.

---

## Install

### Option A: build the binary (recommended)

You need the .NET 10 SDK, but only on the machine that builds. To install it
without Homebrew or sudo:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

Then build:

```bash
git clone https://github.com/Revelts/ymt-creator-gtav.git
cd ymt-creator-gtav/YMTPack
./publish.sh                    # all of: osx-arm64 osx-x64 linux-x64 linux-arm64
./publish.sh osx-arm64          # or just the one you need
```

Each result is one file of about 37 MB at `YMTPack/dist/<platform>/ymtpack`. To
use it from anywhere, put it on your `PATH`:

```bash
mkdir -p ~/.local/bin
cp dist/osx-arm64/ymtpack ~/.local/bin/      # pick your platform
# make sure ~/.local/bin is in PATH (e.g. in ~/.zshrc: export PATH="$HOME/.local/bin:$PATH")
```

For a Linux server, copy `dist/linux-x64/ymtpack` (or `linux-arm64`) to the
server and run `chmod +x ymtpack`.

### Option B: run from source

```bash
cd YMTPack
dotnet run --project src/YMTPack.Cli -- build ./input --name my_clothes --dry-run
```

---

## Quick start

```bash
# 1. Put your clothing packs in one folder (nested folders are fine)
ls input/
#   [MALE] Hoodies/   Women Pack v2/   random_pack/

# 2. Preview what will happen; nothing is written
ymtpack build input --name my_clothes --dry-run

# 3. Build the FiveM resource
ymtpack build input --name my_clothes --out ~/server/resources/[clothes]/my_clothes

# 4. Add it to server.cfg
#    ensure my_clothes
```

Example output:

```
Warning: Women Pack v2/feet_005_u.ydd: drawable has no textures (.ytd), skipped
1 warning(s): some files were skipped or adjusted; check them before shipping.
male   mp_m_freemode_01_my_clothes.ymt  jbibx2 p_headx1
female mp_f_freemode_01_my_clothes.ymt  lowrx2 feetx1
       mp_creaturemetadata_f_my_clothes.ymt  heels 1, hats hiding hair 0
Wrote FiveM resource: /…/my_clothes
```

Read every warning before you ship. Each one is a file that was skipped or
changed.

---

## Usage

### Commands

```
ymtpack build <input-dir> --name <pack> [--out <dir>] [--gender male|female]
              [--heels <file>] [--hats <file>] [--dry-run]
ymtpack ymt2xml <file.ymt> [--out <file.ymt.xml>] [--force]
ymtpack xml2ymt <file.ymt.xml> [--out <file.ymt>] [--force]
ymtpack --help
```

| Option | Meaning |
|---|---|
| `--name` | DLC/pack name. Lowercase `a-z`, `0-9`, `_`, at most 32 characters. Used in every file name. |
| `--out` | Resource folder to create. Default is `./<name>`. It must be missing or empty, and it cannot be inside the input folder. |
| `--gender` | Gender for files whose prefix and folder names don't give one. |
| `--heels` / `--hats` | Rule files for heels and hats. By default `heels.txt` / `hats.txt` in the input folder are used if present. |
| `--dry-run` | Show the plan and warnings without writing anything. |
| `--force` | Lets `ymt2xml` / `xml2ymt` overwrite an existing output file. |

Exit codes are `0` for success, `1` when the build or conversion failed, and
`2` for wrong usage. Set `YMTPACK_DEBUG=1` to get a full stack trace when
something unexpected fails.

### Input file names

Files can have any `something^` prefix or none, and names are case-insensitive.

| What | Pattern | Example |
|---|---|---|
| Component drawable | `<comp>_NNN_<u\|r>.ydd` | `jbib_004_u.ydd` |
| Component texture | `<comp>_diff_NNN_<a-z>_<race>.ytd` | `jbib_diff_004_a_uni.ytd` |
| Cloth physics | `<comp>_NNN_<u\|r>.yld` | `jbib_004_u.yld` |
| Prop drawable | `<prop>_NNN.ydd` | `p_head_002.ydd` |
| Prop texture | `<prop>_diff_NNN_<a-z>.ytd` | `p_head_diff_002_a.ytd` |

- **Components:** `head berd hair uppr lowr hand feet teef accs task decl jbib`
- **Props:** `p_head p_eyes p_ears p_mouth p_lhand p_rhand p_lwrist p_rwrist p_hip p_lfoot p_rfoot p_ph_l_hand p_ph_r_hand`
- **Races:** `uni whi bla chi lat ara bal jam kor ita pak`. `_u` drawables use
  `_uni`; `_r` drawables use a race texture.
- Other `.ydd/.ytd/.yld` names produce a warning and are skipped. Other files
  are ignored, such as `.ymt`, `.meta`, `fxmanifest.lua`, dot-files, and
  `__MACOSX/`.
- A drawable is paired with textures and cloth that have the same number and
  prefix **in the same folder**.

### Gender detection

ymtpack checks these in order:

1. a `mp_m_freemode_01…^` or `mp_f_freemode_01…^` prefix on the file,
2. the closest folder whose name says it. The words `male`, `men`, `female`,
   and `women` count anywhere in the name (`[MALE] Hoodies`, `Women Pack v2`),
   and so does `mp_f_freemode_01`. Short forms (`m`, `f`, `man`, `girl`, …)
   count only when they are the whole folder name, such as `m/` or `[F]`.
3. `--gender male|female`.

If none of these applies to a file, the build stops and nothing is written.

### Heels and hats that hide hair

Create `heels.txt` and/or `hats.txt` in the input folder. Each line is
`<pattern> [value]`. The pattern is matched against the **original** `.ydd`
path inside the input folder, because the new numbers aren't known until the
build runs.

```
# heels.txt: feet only. value = heel height (default 1.0)
Women Pack v2/feet_003_u.ydd   1.5
[F] Heels/**

# hats.txt: p_head only. value = how much the hair shrinks, 0-1 (default 0.5, 1 = hide)
*p_head_002.ydd   1.0
```

- `*` matches within one folder, `**` matches across folders, and `?` matches
  one character. A pattern without `/` matches the file name only.
- Spaces and `[...]` in folder names work as-is. `#` starts a comment. When
  several lines match the same file, the last one wins.
- Heels set the walking sound to `shoe_heels` and the heel height in the YMT.
- Hats set the hair scale in the YMT.
- ymtpack then writes `mp_creaturemetadata_<m|f>_<pack>.ymt` to `stream/` and
  references it in the `.meta` file.
- Hair scaling only works on hair models that have the `MH_Hair_Scale` bone.
  Many custom hairs don't.

### Output

```
my_clothes/
  fxmanifest.lua                                # registers each .meta as SHOP_PED_APPAREL_META_FILE
  mp_m_freemode_01_my_clothes.meta              # ShopPedApparel (male)
  mp_f_freemode_01_my_clothes.meta              # ShopPedApparel (female)
  ymtpack-report.json                           # original file -> new stream name, all warnings
  stream/male/mp_m_freemode_01_my_clothes.ymt
  stream/male/mp_m_freemode_01_my_clothes^jbib_000_u.ydd
  stream/male/mp_m_freemode_01_my_clothes^jbib_diff_000_a_uni.ytd
  stream/male/mp_m_freemode_01_p_my_clothes^p_head_000.ydd
  stream/female/mp_creaturemetadata_f_my_clothes.ymt   # only when heels/hats are used
  …
```

To find out which original item became which drawable number (for a clothing
shop or menu), open `ymtpack-report.json`.

### Editing a YMT by hand

```bash
ymtpack ymt2xml stream/male/mp_m_freemode_01_my_clothes.ymt        # → .ymt.xml
# edit the XML (flags, audio, expressionMods, …)
ymtpack xml2ymt stream/male/mp_m_freemode_01_my_clothes.ymt.xml --force
```

This works for ped variation YMTs and for creature metadata files. Float values
can show as, for example, `1.20000005`. That is normal float32 precision.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `cannot tell male/female` | Put the files under a `male/` or `female/` folder, or pass `--gender`. |
| `drawable has no textures` | The `.ytd` is missing or sits in a different folder from the `.ydd`. Put them together. |
| `Output folder is not empty` | Pick a new `--out` or empty the folder. ymtpack never overwrites a resource. |
| `rule '…' matched nothing` | Check the pattern against the paths in the dry-run output or in `ymtpack-report.json`. |
| Over 128 drawables in one slot | Expected. ymtpack creates `pack_2`, `pack_3`, … automatically. |
| Unexpected error | Run again with `YMTPACK_DEBUG=1` and read the stack trace. |

## Known limits

- Items haven't been checked in-game in an automated way. Test new packs on a
  dev server first.
- `numAlternatives` is always 0, and alternate drawables (`*_1.ydd`) are
  skipped.
- File contents are not validated. A broken `.ydd` is copied as-is.
- Other per-item flags, such as prop `renderFlags` or audio for non-heels
  items, can only be changed with `ymt2xml` / `xml2ymt`.

Developer notes (project layout, tests, architecture) are in
[YMTPack/README.md](YMTPack/README.md).

---

## Credits

This project stands on work by these people and projects.

### Code and data

- **[YMTEditor](https://github.com/grzybeek/YMTEditor)** by **grzybeek**
  - This repo started from its source and keeps its git history.
  - ymtpack's YMT generator is a port of YMTEditor's `XMLHandler` schema:
    component, prop, and anchor tables, texId/race list, propMask defaults, and
    compInfo defaults.
  - The creature metadata generator is also from YMTEditor.
  - YMTEditor also shipped the `CodeWalker.Core.dll` build used here.
- **[grzyClothTool](https://github.com/grzybeek/grzyClothTool)** by **grzybeek**
  - The heels and hats settings follow it: heel height in the 5th
    `expressionMods` value, hair scale stored as a negative 1st value, the
    default values 1.0 and 0.5, and the `mp_creaturemetadata_<m|f>_<name>`
    naming.
- **[CodeWalker](https://github.com/dexyfex/CodeWalker)** by **dexyfex**
  - The bundled `CodeWalker.Core.dll` does the binary `.ymt` work: RSC7/Meta
    for ped variations and RBF for creature metadata.
  - CodeWalker includes third-party code by **Neodymium** (2015, MIT),
    **Cameron Berry** (2017, MIT), and **Hamish Milne**
    ([FbxWriter](https://github.com/hamish-milne/FbxWriter), 2015).
  - The full notice is in [`YMTPack/lib/CodeWalker.Notice.txt`](YMTPack/lib/CodeWalker.Notice.txt).

### Documentation and research

- cfx.re forum tutorials (linked from YMTEditor's README) provided the stream
  naming, the `_p_` prop prefix, the ShopPedApparel `.meta`/fxmanifest layout,
  `eCharacter` values, the 128 drawables per component limit, `shoe_heels`, and
  the `MH_Hair_Scale` caveat:
  - [How to stream clothes and props as addons for mp freemode models](https://forum.cfx.re/t/how-to-stream-clothes-and-props-as-addons-for-mp-freemode-models/3345474)
  - [How to create addon heels or hide hair with addon hat](https://forum.cfx.re/t/how-to-create-addon-heels-or-hide-hair-with-addon-hat/4209989)

### Libraries and tools

- [.NET](https://dotnet.microsoft.com/) by Microsoft (runtime, SDK, `dotnet-install.sh`).
- [SharpDX.Mathematics](https://www.nuget.org/packages/SharpDX.Mathematics) 4.2.0, a dependency of CodeWalker.Core.
- [xUnit](https://xunit.net/), [coverlet](https://github.com/coverlet-coverage/coverlet), and Microsoft.NET.Test.Sdk for tests.
- [REA](https://github.com/morluto/rea) was used to inspect the bundled
  `CodeWalker.Core.dll`. It confirmed .NET Standard 2.0, no native or Windows
  calls, and the exact API used.

### License note

This is a private repo for personal use. YMTEditor has no license file.
CodeWalker's notice states copyright by dexyfex without a license grant for its
own code. Clear both before making this repo public or sharing the binaries.
