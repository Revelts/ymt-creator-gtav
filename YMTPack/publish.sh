#!/usr/bin/env bash
# Build self-contained single-file ymtpack binaries (no .NET needed on the target machine).
# Usage: ./publish.sh [rid ...]    default: osx-arm64 osx-x64 linux-x64 linux-arm64
set -euo pipefail

cd "$(dirname "$0")"

DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
if [[ ! -x "$DOTNET" ]]; then
  echo "dotnet not found. Install the .NET 10 SDK (e.g. dotnet-install.sh --channel 10.0) or set DOTNET=/path/to/dotnet." >&2
  exit 1
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

RIDS=("$@")
if [[ ${#RIDS[@]} -eq 0 ]]; then
  RIDS=(osx-arm64 osx-x64 linux-x64 linux-arm64)
fi

for rid in "${RIDS[@]}"; do
  echo "==> $rid"
  "$DOTNET" publish src/YMTPack.Cli/YMTPack.Cli.csproj \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -o "dist/$rid" \
    --nologo -v quiet
  echo "    dist/$rid/ymtpack"
done
