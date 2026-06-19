#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$ROOT_DIR/TabletUtilityPack"
OTD_APP="/Applications/OpenTabletDriver.app/Contents/MacOS"
DOTNET_COMMAND="${DOTNET:-dotnet}"
VERSION="$(sed -n 's/.*"PluginVersion": "\([^"]*\)".*/\1/p' "$PROJECT_DIR/metadata.json")"
DIST_DIR="$ROOT_DIR/dist"
PACKAGE_NAME="OTD-macOS-Companion-v$VERSION"
STAGE_DIR="$DIST_DIR/$PACKAGE_NAME"
ZIP_PATH="$DIST_DIR/$PACKAGE_NAME.zip"

if [[ ! -d "$OTD_APP" ]]; then
  echo "OpenTabletDriver.app was not found in /Applications." >&2
  exit 1
fi

rm -rf "$STAGE_DIR" "$ZIP_PATH"
mkdir -p "$STAGE_DIR"

"$DOTNET_COMMAND" build "$PROJECT_DIR/TabletUtilityPack.csproj" -c Release

xcrun clang -O2 -arch arm64 -mmacosx-version-min=13.0 \
  -Wall -Wextra -Werror \
  -framework ApplicationServices \
  "$PROJECT_DIR/MacNativeKeyHelper.c" \
  -o "$STAGE_DIR/MacNativeKeyHelper"

xcrun clang -O2 -arch arm64 -mmacosx-version-min=13.0 \
  -fobjc-arc -Wall -Wextra -Werror \
  -framework Cocoa \
  -framework ApplicationServices \
  "$PROJECT_DIR/MacCrosshairOverlay.m" \
  -o "$STAGE_DIR/MacCrosshairOverlay"

cp "$PROJECT_DIR/bin/Release/net8.0/TabletUtilityPack.dll" "$STAGE_DIR/"
cp "$PROJECT_DIR/metadata.json" "$STAGE_DIR/"
cp "$ROOT_DIR/README.md" "$STAGE_DIR/"
cp "$ROOT_DIR/LICENSE" "$STAGE_DIR/"

(
  cd "$STAGE_DIR"
  /usr/bin/zip -X -q -r "$ZIP_PATH" .
)

shasum -a 256 "$ZIP_PATH"
echo "$ZIP_PATH"
