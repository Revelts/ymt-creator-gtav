# ymtpack: developer notes

Installation, usage, and credits are in the [root README](../README.md). This
page covers building, testing, and how the code fits together.

## Build and test

You need the .NET 10 SDK (see the root README for an install without
Homebrew or sudo).

```bash
dotnet build
dotnet test                                     # xUnit, 111 tests
dotnet test --collect:"XPlat Code Coverage"     # coverlet (Cobertura XML in TestResults/)
dotnet format --verify-no-changes
./publish.sh [rid ...]                          # self-contained single-file binaries in dist/<rid>/
```

`bin/`, `obj/`, `dist/` and `TestResults/` are git-ignored.

## Layout

```
lib/
  CodeWalker.Core.dll         pinned CodeWalker build (see below)
  CodeWalker.Notice.txt       CodeWalker's copyright notice
src/YMTPack.Core/
  Naming/      AssetNameParser       file name -> slot, number, u/r, letter, race, prefix gender
  Scanning/    PackScanner           walk input (no symlinked folders), GenderResolver
  Planning/    PackPlanner           pair drawable+textures+cloth, renumber, split at 128
  Effects/     EffectRules/Applier   heels.txt / hats.txt parsing, glob matching, apply to plan
  Ymt/         YmtXmlBuilder         CPedVariationInfo XML (port of YMTEditor XMLHandler)
               CreatureMetadataBuilder  CCreatureMetaData XML (YMTEditor / grzyClothTool)
               YmtCodec / RbfCodec   XML <-> binary via CodeWalker (RSC7 / RBF)
  FiveM/       FiveMResourceFiles    ShopPedApparel .meta, fxmanifest.lua
  Output/      PackWriter            staged write + atomic move, ymtpack-report.json
  PackBuilder                        scan -> rules -> plan -> apply effects -> write
src/YMTPack.Cli/                     arg parsing, build / ymt2xml / xml2ymt commands
tests/YMTPack.Tests/                 unit + integration (real CodeWalker round trips)
```

The plan types (`PackPlan`, `PedVariationPlan`, `DrawablePlan`, …) are
immutable records. Each stage returns a new plan and does not change the one it
was given.

## Pinned CodeWalker.Core.dll

`lib/CodeWalker.Core.dll` (sha256 `0a081396…d18b`) is the build that YMTEditor
shipped. It targets .NET Standard 2.0, is IL-only, and has no P/Invoke, so it
runs unchanged on .NET 10 for macOS and Linux.

**Keep this exact build.** Its field names for ped YMTs (`hash_2FD08CEF`,
`hash_07AE529D`, `hash_D12F579D`, …) are the names `YmtXmlBuilder` writes.
Newer CodeWalker builds call them `pedXml_audioID`, `pedXml_expressionMods`,
and so on, and would reject this XML.

The DLL's `SharpDX.Mathematics` dependency comes from NuGet (4.2.0,
netstandard). The copy YMTEditor bundled was built for .NET Framework 4.5.

## Behaviour notes

- **Output safety:** the resource is built in a hidden sibling temp folder and
  moved into place only at the end. ymtpack refuses an output folder that is
  not empty or that is a symlink.
- **Determinism:** inputs are sorted (gender, slot, source folder and prefix,
  number), so the same input always gives the same numbering.
- **Hash fields:** `dlcName` is stored as a Jenkins hash. `ymt2xml` registers
  the DLC name guessed from the file name, so the field decodes as text and not
  `hash_XXXXXXXX`.
- **XML without BOM:** `.meta` and converted XML are written as UTF-8 without a
  byte-order mark.
- **Self-contained Linux builds:** these use `InvariantGlobalization`, so they
  need no `libicu`.
