# ymt-creator-gtav

Personal tooling for packing GTA V addon clothing on macOS and Linux.

**[YMTPack/](YMTPack/README.md)** contains `ymtpack`, a cross-platform CLI. It
merges addon clothing packs, renumbers and renames the stream files, and writes
a ready FiveM resource. The resource gets a fresh `.ymt` plus optional creature
metadata for heels and hats.

## Credits

- [YMTEditor](https://github.com/grzybeek/YMTEditor) by grzybeek. This repo
  started from its source and history. The Windows editor was removed in favor
  of `ymtpack`, and its source is still in the git history before that change.
- [grzyClothTool](https://github.com/grzybeek/grzyClothTool) by grzybeek. Its
  heels and hats creature metadata values are mirrored here.
- [CodeWalker](https://github.com/dexyfex/CodeWalker) by dexyfex. The bundled
  `CodeWalker.Core.dll` comes from it; see `YMTPack/lib/CodeWalker.Notice.txt`.
