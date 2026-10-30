#!/bin/sh
# Build OpenDejenol's release packages: the Windows installer, the full update package, the
# update feed and the portable zip (docs/RELEASING.md). The release workflow
# (.github/workflows/release.yml) runs exactly this; run it by hand to see what a release would hold.
#
#   sh tools/pack-release.sh <output folder>
#
# What it does:
#   1. reads the version from src/Mordor.App/Mordor.App.csproj (<Version>, the one place it is set)
#      and, when RELEASE_TAG is set (the workflow sets it to the pushed tag), refuses a tag that is
#      not "v" + that version;
#   2. publishes the program, Release, self-contained for win-x64 (not single-file: the mods are
#      loaded from the mods folder beside the program), into <output folder>/publish;
#   3. checks the publish: OpenDejenol.exe, each bundled mod's dll and mod.json, LICENSE.txt, no
#      file of the game's and no path of the builder's user folder;
#   4. packs it with Velopack (vpk, the version in .config/dotnet-tools.json) into
#      <output folder>/releases: OpenDejenol.Game-win-Setup.exe, OpenDejenol.Game-<version>-full.nupkg,
#      OpenDejenol.Game-win-Portable.zip, RELEASES and releases.win.json (a previous release's
#      packages already in that folder - vpk download github - give a delta package as well),
#      with the program's icon on the installer and its 256-pixel picture as the installer's
#      splash (src/Mordor.App/Assets/Icon);
#   5. lists what it made, with sizes and SHA-256.
# It uploads nothing, and never runs the program or the installer.
#
# The package id OpenDejenol.Game is fixed for good: installed copies find their updates by it,
# and it names the install folder, %LOCALAPPDATA%\OpenDejenol.Game - kept apart from the player's
# own %LOCALAPPDATA%\OpenDejenol (the picture cache) and %APPDATA%\OpenDejenol (settings, saves),
# which an update or an uninstall never touches.
#
# Optional: VPK_SIGN_PARAMS or VPK_SIGN_TEMPLATE are passed to vpk as --signParams/--signTemplate
# (code signing, off unless set: docs/RELEASING.md); RELEASE_NOTES names a Markdown file for the
# release's notes. The .NET SDK is found as tools/check.sh finds it.

set -u
[ $# -eq 1 ] || { echo "usage: sh tools/pack-release.sh <output folder>"; exit 2; }
OUT_IN=$1
case "$OUT_IN" in /*|[A-Za-z]:*) ;; *) OUT_IN="$(pwd)/$OUT_IN" ;; esac

cd "$(dirname "$0")/.." || exit 1
ROOT=$(pwd)

PACK_ID=OpenDejenol.Game
TITLE=OpenDejenol
AUTHORS="OpenDejenol contributors"
APP_PROJ=src/Mordor.App/Mordor.App.csproj
ICON=src/Mordor.App/Assets/Icon/opendejenol.ico
SPLASH=src/Mordor.App/Assets/Icon/opendejenol-256.png
MODS="QoL:Mordor.Mod.QoL Kaizen:Mordor.Mod.Kaizen DpsMeter:Mordor.Mod.DpsMeter Cartographer:Mordor.Mod.Cartographer AutoCombat:Mordor.Mod.AutoCombat"

if [ -z "${DOTNET:-}" ]; then
    for d in "${DOTNET_ROOT:-}" "${USERPROFILE:+$(cygpath -u "$USERPROFILE" 2>/dev/null)/.dotnet}" "$HOME/.dotnet"; do
        [ -n "$d" ] || continue
        if [ -x "$d/dotnet.exe" ]; then DOTNET="$d/dotnet.exe"; break; fi
        if [ -x "$d/dotnet" ]; then DOTNET="$d/dotnet"; break; fi
    done
    DOTNET="${DOTNET:-dotnet}"
fi
if [ -z "${DOTNET_ROOT:-}" ] && [ "$DOTNET" != "dotnet" ]; then DOTNET_ROOT=$(dirname "$DOTNET"); export DOTNET_ROOT; fi

die() { echo "  FAIL  $*"; echo; echo "NOT PACKED"; exit 1; }
# a path as the Windows tools want it, where cygpath is there to make one
winpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }

# 1. the version
VERSION=$("$DOTNET" msbuild "$APP_PROJ" -nologo -getProperty:Version 2>/dev/null | tr -d "\r" | tail -n 1 | tr -d " ")
case "$VERSION" in
    [0-9]*.[0-9]*.[0-9]*) ;;
    *) die "no version in $APP_PROJ (read \"$VERSION\")" ;;
esac
echo "== OpenDejenol $VERSION"
if [ -n "${RELEASE_TAG:-}" ] && [ "$RELEASE_TAG" != "v$VERSION" ]; then
    die "the tag $RELEASE_TAG is not v$VERSION, the version in $APP_PROJ: change one of them"
fi

mkdir -p "$OUT_IN" || die "cannot make $OUT_IN"
OUT=$(cd "$OUT_IN" && pwd)
case "$OUT/" in "$ROOT/"*) die "the output folder is inside the tree ($OUT): choose one outside it" ;; esac
PUB="$OUT/publish"
REL="$OUT/releases"
rm -rf "$PUB"
mkdir -p "$REL"

# 2. the publish. ContinuousIntegrationBuild maps the source paths compiled into the program to
# /_/, and the symbols are embedded in each dll (DebugType embedded): a crash log's stack trace
# keeps its line numbers in an installed copy (Velopack leaves separate .pdb files out), and no
# folder of the machine that built it is named in what is packed - a separate pdb's path, which
# Avalonia's XAML step writes unmapped, is not there to be named
echo "== publishing (Release, win-x64, self-contained)"
PUB_OUT=$("$DOTNET" publish "$APP_PROJ" -c Release -r win-x64 --self-contained true \
          -p:PublishSingleFile=false -p:ContinuousIntegrationBuild=true -p:DebugType=embedded \
          -o "$(winpath "$PUB")" --nologo 2>&1)
PUB_RC=$?
echo "$PUB_OUT" | grep -E "warning|error|->" | sed "s/^/  /"
[ "$PUB_RC" -eq 0 ] || die "the publish failed (exit $PUB_RC)"
if echo "$PUB_OUT" | grep -q -E ": warning "; then die "the publish has warnings"; fi

# 3. what the publish holds
[ -f "$PUB/OpenDejenol.exe" ] || die "no OpenDejenol.exe in the publish"
[ -f "$PUB/LICENSE.txt" ] || die "no LICENSE.txt in the publish"
for m in $MODS; do
    folder=${m%%:*}; dll=${m#*:}
    [ -f "$PUB/mods/$folder/$dll.dll" ] && [ -f "$PUB/mods/$folder/mod.json" ] \
        || die "the bundled mod $folder is not in the publish (mods/$folder/$dll.dll and mod.json)"
done
echo "  ok    OpenDejenol.exe, LICENSE.txt, and mods/{QoL,Kaizen,DpsMeter,Cartographer,AutoCombat} with dll and mod.json"
GAMEFILES=$(cd "$PUB" && find . -type f | grep -i -E "\.(png|bmp|ico|cur|gif|jpg|wav|mid|midi|mdr|hlp|chm|vbx|ini|wri|mak)$|(^|/)(MORDOR\.EXE|MDATA[0-9]+\.MDR|MG11\.DLL|WAVMIX16\.DLL)$" | head -5)
[ -z "$GAMEFILES" ] || die "files of the game's in the publish: $GAMEFILES"
echo "  ok    no file of the game's (pictures, sounds, music, data, help)"
WHO="${USERNAME:-${USER:-}}"
if [ -n "$WHO" ]; then
    PERSONAL=$(grep -r -l -a -i -E "users[\\/]+$WHO([\\/]|\$)" "$PUB" 2>/dev/null | grep -v -i "\.pdb$" | head -5)
    [ -z "$PERSONAL" ] || die "the builder's user folder (Users\\$WHO) is named in: $PERSONAL"
    echo "  ok    no path of the builder's user folder in any file packed"
fi

# 4. the packages
echo "== packing with Velopack"
"$DOTNET" tool restore >/dev/null || die "dotnet tool restore failed (vpk, .config/dotnet-tools.json)"
[ -f "$ICON" ] || die "no icon at $ICON"
[ -f "$SPLASH" ] || die "no splash picture at $SPLASH"
set -- pack --packId "$PACK_ID" --packVersion "$VERSION" --runtime win-x64 --channel win \
    --packDir "$(winpath "$PUB")" --mainExe OpenDejenol.exe --packTitle "$TITLE" --packAuthors "$AUTHORS" \
    --icon "$(winpath "$ROOT/$ICON")" --splashImage "$(winpath "$ROOT/$SPLASH")" \
    --outputDir "$(winpath "$REL")"
[ -n "${RELEASE_NOTES:-}" ] && set -- "$@" --releaseNotes "$(winpath "$RELEASE_NOTES")"
[ -n "${VPK_SIGN_PARAMS:-}" ] && set -- "$@" --signParams "$VPK_SIGN_PARAMS"
[ -n "${VPK_SIGN_TEMPLATE:-}" ] && set -- "$@" --signTemplate "$VPK_SIGN_TEMPLATE"
PACK_OUT=$("$DOTNET" vpk "$@" --skip-updates --legacyConsole 2>&1)
PACK_RC=$?
echo "$PACK_OUT" | sed "s/^/  /"
for f in "$PACK_ID-win-Setup.exe" "$PACK_ID-$VERSION-full.nupkg" "$PACK_ID-win-Portable.zip" RELEASES releases.win.json; do
    [ -f "$REL/$f" ] || die "vpk made no $f"
done
[ "$PACK_RC" -eq 0 ] || die "vpk pack failed"

# 5. what was made
echo "== $REL"
for f in "$REL"/*; do
    [ -f "$f" ] || continue
    size=$(wc -c < "$f" | tr -d " ")
    sum=$(sha256sum "$f" | cut -d " " -f 1)
    printf "  %-44s %11s bytes  sha256 %s\n" "$(basename "$f")" "$size" "$sum"
done
echo
echo "packed: OpenDejenol $VERSION in $REL (nothing uploaded)"
