#!/usr/bin/env bash
# Build self-contained, single-file HomeStock executables for all supported platforms and
# package them as release artifacts (zip for Windows, tar.gz for Linux/macOS) with checksums.
#
# Usage:  scripts/publish-all.sh [VERSION] [OUTPUT_DIR]
#   VERSION     defaults to 0.0.0-dev
#   OUTPUT_DIR  defaults to ./artifacts
#
# Each artifact contains the app plus the install/upgrade scripts and a README so a user can
# unpack and run without the .NET SDK installed (the runtime is bundled).
set -euo pipefail

VERSION="${1:-0.0.0-dev}"
OUT="${2:-artifacts}"
PROJECT="src/HomeStock.Web/HomeStock.Web.csproj"
RIDS=("linux-x64" "linux-arm64" "win-x64" "osx-x64" "osx-arm64")

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
mkdir -p "$OUT"

echo "Publishing HomeStock $VERSION for: ${RIDS[*]}"

for RID in "${RIDS[@]}"; do
  echo ">> $RID"
  PUBDIR="$(mktemp -d)"
  dotnet publish "$PROJECT" \
    -c Release -r "$RID" --self-contained true \
    -p:Version="$VERSION" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$PUBDIR" >/dev/null

  # Bundle the platform's install/upgrade helper(s) and a short README alongside the binary.
  cp scripts/install.sh scripts/upgrade.sh "$PUBDIR/" 2>/dev/null || true
  cp scripts/install.ps1 "$PUBDIR/" 2>/dev/null || true
  cp scripts/RUNME.txt "$PUBDIR/" 2>/dev/null || true

  NAME="homestock-$VERSION-$RID"
  case "$RID" in
    win-*)
      (cd "$PUBDIR" && zip -qr "$ROOT/$OUT/$NAME.zip" .)
      ;;
    *)
      tar -czf "$OUT/$NAME.tar.gz" -C "$PUBDIR" .
      ;;
  esac
  rm -rf "$PUBDIR"
done

# Checksums for integrity verification by the installer/upgrader.
( cd "$OUT" && sha256sum homestock-"$VERSION"-* > "SHA256SUMS-$VERSION.txt" )

echo "Artifacts written to $OUT:"
ls -1 "$OUT"
