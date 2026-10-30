#!/bin/sh
# The macOS app: OpenDejenol.app, for Apple Silicon, to run on this Mac.
#
#   sh tools/pack-macos.sh [out folder]      (defaults to dist/macos; the folder is gitignored)
#
# It
#   1. publishes the program, Release, self-contained for osx-arm64 (not single-file: the mods are
#      loaded from the mods folder beside the program, as in a Windows build - docs/MODDING.md);
#   2. lays it out as an app bundle: the publish folder is Contents/MacOS, the icon (made from the
#      port's own PNGs in Assets/Icon, not the game's) is Contents/Resources/OpenDejenol.icns;
#   3. signs it ad hoc (codesign -s -), which is what Apple Silicon needs to run it at all.
#
# Ad hoc is not a Developer ID: this Mac runs the app, and another Mac's Gatekeeper refuses it
# until it is allowed by hand (right-click > Open, or xattr -dr com.apple.quarantine). Notarised
# releases are not made here. Nothing of the game's is in the bundle: the player points it at their
# own copy at first run, as on Windows (docs/GAME_FILES.md).
#
# No Velopack: an app not installed by Velopack's installer never asks for updates (Updates.cs).

cd "$(dirname "$0")/.." || exit 1
ROOT=$(pwd)
OUT="${1:-dist/macos}"
DOTNET="${DOTNET:-dotnet}"
APP_PROJ=src/Mordor.App/Mordor.App.csproj
ICON_DIR=src/Mordor.App/Assets/Icon

die() { echo "  FAIL  $1"; exit 1; }

[ "$(uname -s)" = Darwin ] || die "this makes a macOS app bundle: run it on a Mac"
for t in codesign iconutil plutil; do
    command -v "$t" >/dev/null || die "no $t (the Xcode command line tools)"
done

# the version is the program's, set in the project and nowhere else (docs/RELEASING.md)
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$APP_PROJ" | head -1)
[ -n "$VERSION" ] || die "no <Version> in $APP_PROJ"

APP="$OUT/OpenDejenol.app"
PUB="$OUT/publish"
rm -rf "$APP" "$PUB"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" || die "could not make $APP"

echo "== publishing (Release, osx-arm64, self-contained)"
"$DOTNET" publish "$APP_PROJ" -c Release -r osx-arm64 --self-contained true -o "$PUB" -nologo \
    || die "dotnet publish failed"
[ -f "$PUB/OpenDejenol" ] && [ -d "$PUB/mods" ] || die "the publish has no OpenDejenol or no mods folder"
cp -R "$PUB/." "$APP/Contents/MacOS/"
rm -rf "$PUB"

echo "== icon"
# iconutil wants the Apple names; the port has 16 to 256 pixels, so 256@2x (512) is left out
SET="$OUT/OpenDejenol.iconset"
rm -rf "$SET"; mkdir -p "$SET"
cp "$ICON_DIR/opendejenol-16.png"  "$SET/icon_16x16.png"
cp "$ICON_DIR/opendejenol-32.png"  "$SET/icon_16x16@2x.png"
cp "$ICON_DIR/opendejenol-32.png"  "$SET/icon_32x32.png"
cp "$ICON_DIR/opendejenol-64.png"  "$SET/icon_32x32@2x.png"
cp "$ICON_DIR/opendejenol-128.png" "$SET/icon_128x128.png"
cp "$ICON_DIR/opendejenol-256.png" "$SET/icon_128x128@2x.png"
cp "$ICON_DIR/opendejenol-256.png" "$SET/icon_256x256.png"
iconutil -c icns "$SET" -o "$APP/Contents/Resources/OpenDejenol.icns" || die "iconutil failed"
rm -rf "$SET"

echo "== Info.plist"
cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>               <string>OpenDejenol</string>
    <key>CFBundleDisplayName</key>        <string>OpenDejenol</string>
    <key>CFBundleIdentifier</key>         <string>org.opendejenol.OpenDejenol</string>
    <key>CFBundleExecutable</key>         <string>OpenDejenol</string>
    <key>CFBundleIconFile</key>           <string>OpenDejenol</string>
    <key>CFBundlePackageType</key>        <string>APPL</string>
    <key>CFBundleShortVersionString</key> <string>$VERSION</string>
    <key>CFBundleVersion</key>            <string>$VERSION</string>
    <key>LSMinimumSystemVersion</key>     <string>12.0</string>
    <key>LSApplicationCategoryType</key>  <string>public.app-category.role-playing-games</string>
    <key>NSHighResolutionCapable</key>    <true/>
    <key>NSHumanReadableCopyright</key>   <string>GPL-3.0. Mordor: The Depths of Dejenol is not included.</string>
</dict>
</plist>
EOF
plutil -lint "$APP/Contents/Info.plist" >/dev/null || die "Info.plist does not parse"

echo "== signing (ad hoc)"
codesign --force --deep --sign - "$APP" || die "codesign failed"
codesign --verify --deep --strict "$APP" || die "the signature does not verify"

echo "made $ROOT/$APP ($VERSION)"
