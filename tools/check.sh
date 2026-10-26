#!/bin/sh
# Run everything that can say it is wrong.
#
# Two passes, and they answer different questions:
#
#   selfcheck  - do the numbers still match what dejenol.com, the help and the MG11.DLL resources
#                say they should be? This one asserts, and its exit code means something.
#   the sweep  - does every other probe still run at all? They print rather than assert, so this
#                catches a crash or a signature that rotted, and nothing finer.
#
# Neither touches saves or the game files; the probes only read.
#
#   sh tools/check.sh [gamedir]      (gamedir defaults to ../game_files)
#
# The game folder is the player's own copy of Mordor (docs/GAME_FILES.md): nothing of the game's
# is in this tree. The .NET SDK is $DOTNET if set, else $DOTNET_ROOT/dotnet, else
# ~/.dotnet/dotnet, else the dotnet on the PATH.
#
# Without the decompiled source (decompiled/, decompiled-named/) or the saved dejenol.com pages
# (docs/dejenol-formulas/) - as in the public tree, which has none of them - the checks that read
# them print "skipped: decompiled source not present" and are not counted; everything else runs.

# No `set -e`: a failing check is the thing this script is for, and aborting on the first one
# would skip the sweep and report less than it knows.
cd "$(dirname "$0")/.." || exit 1
GAME="${1:-../game_files}"
if [ -z "${DOTNET:-}" ]; then
    for d in "${DOTNET_ROOT:-}" "${USERPROFILE:+$(cygpath -u "$USERPROFILE" 2>/dev/null)/.dotnet}" "$HOME/.dotnet"; do
        [ -n "$d" ] || continue
        if [ -x "$d/dotnet.exe" ]; then DOTNET="$d/dotnet.exe"; break; fi
        if [ -x "$d/dotnet" ]; then DOTNET="$d/dotnet"; break; fi
    done
    DOTNET="${DOTNET:-dotnet}"
fi
# the apphost of a private SDK (not in Program Files) finds the runtime through DOTNET_ROOT
if [ -z "${DOTNET_ROOT:-}" ] && [ "$DOTNET" != "dotnet" ]; then export DOTNET_ROOT="$(dirname "$DOTNET")"; fi

if [ -d decompiled ] && [ -d decompiled-named ] && [ -d docs/dejenol-formulas ]; then
    echo "the decompiled source and the dejenol.com pages are here: every check runs"
else
    echo "skipped: decompiled source not present (decompiled/, decompiled-named/, docs/dejenol-formulas/):"
    echo "         the checks that read it say so below and are not counted"
fi

echo "building the probe harness"
# the build's own exit status, not the grep's: a failed build would otherwise leave the last
# good exe in place, and every check below would run against code that is not this tree's
BUILD_OUT=$("$DOTNET" build -c Release Mordor.DataDump/Mordor.DataDump.csproj 2>&1)
BUILD_RC=$?
echo "$BUILD_OUT" | grep -E "error|Build succeeded" | head -5
if [ "$BUILD_RC" -ne 0 ]; then
    echo "  FAIL  the probe harness did not build (exit $BUILD_RC): nothing was checked"
    echo; echo "SOMETHING IS WRONG"; exit 1
fi
EXE=./Mordor.DataDump/bin/Release/net8.0/Mordor.DataDump.exe

echo
echo "== the numbers that have a source to be checked against =="
if "$EXE" "$GAME" selfcheck; then CHECKED=0; else CHECKED=1; fi

echo
echo "== the mod system: loader, default answers, the Quality of Life mod (docs/MODDING.md) =="
if "$EXE" "$GAME" mods; then MODS=0; else MODS=1; fi

echo
echo "== the windows, headless: the Automap resized, paged and enlarged (Mordor.UiCheck) =="
# Avalonia's headless platform: the real controls laid out and real pointer input routed through
# them, with nothing put on screen and nothing written, so it runs under MORDOR_CHECK_NO_UI too
UI_BUILD=$("$DOTNET" build -c Release Mordor.UiCheck/Mordor.UiCheck.csproj 2>&1)
UI_RC=$?
echo "$UI_BUILD" | grep -E " error |Build succeeded" | head -5
if [ "$UI_RC" -ne 0 ]; then
    echo "  FAIL  the window checks did not build (exit $UI_RC)"
    UI=1
elif timeout 120 ./Mordor.UiCheck/bin/Release/net8.0/Mordor.UiCheck.exe "$GAME"; then UI=0; else UI=1; fi

echo
echo "== every other probe, for a crash =="
# left out: extract/art/pics/wallart write files, autoload needs a saves directory and a leader,
# play is a simulation with no end condition
BROKE=""
for c in abilities adcheck adinterval ailments aligns areaid areas asmodeus audio bits castgate spellhelp lairs rarity traptable shadow hands racetable crestspells spellzero recordzero locks reach classspells orphans commands acid drain stateleak fate uncurse deepest classdump handover citations helpquotes \
         chest combat companionpage companions depth drops dropsim effects equip exchange \
         exchangeswap gamedata gearcheck gold goldbug goldfactor goldsim groups guildad guildlog \
         guildmaster hall itemlayout itemspells itemtypes join library lost magic map maze mdata1 \
         mflags misid mix monsterattack orient pack party password perceive price quests raise \
         rating rescue resist respawn rooms rules rulescheck save seer slotroll spawnsim spellcaps \
         spellgate spellinfo spells spellstuff statmods store swings teleports trade \
         trapread traps use uselevel; do
    if ! out=$(timeout 90 "$EXE" "$GAME" "$c" 2>&1); then
        BROKE="$BROKE $c"
        echo "  CRASHED  $c"
        echo "$out" | grep -iE "exception|error" | head -2
    fi
done

if [ -z "$BROKE" ]; then echo "  every probe ran"; fi

echo
echo "== start-up smoke: the Release app in town and in the dungeon, for a crash =="
# The probes never open a window, so a crash in a window's start-up (a theme resource of the
# wrong type, say, which took down every tab strip once) got past all of the above. This starts
# a Release copy of the app in MORDOR_TEST=city and then MORDOR_TEST=dungeon, gives each ten
# seconds, and fails if it is gone by then or wrote an exception to its crash log.
#
# It may run while the game is being played, so it keeps to what it made itself:
#   - it runs a copy of the Release build from a scratch folder (the game is played from Debug,
#     and the copy never holds the build folder's files open);
#   - saves go to a new scratch folder (MORDOR_SAVES), never the real saves, with a settings.ini
#     there that switches the Quality of Life mod on, and MORDOR_MODS names the copy's own mods;
#   - the crash log is a scratch file of its own (MORDOR_CRASHLOG), so the real crash.log is
#     neither written nor read;
#   - it stops only the processes it started, by their own process ids - and only while that id
#     is still the scratch copy's OpenDejenol.exe: the id file is deleted as soon as the app is
#     seen to have exited, and before any kill the id's executable path is checked against the
#     scratch copy (and taskkill filtered to OpenDejenol.exe), since Windows hands a freed id to
#     the next process started, which may be the game being played.
# MORDOR_CHECK_NO_UI=1 skips it, with a line saying so: for a run that must not put windows up.
#
# How it starts and stops the app: Git Bash's own `&`/`$!`/`wait` were not enough. The pid `$!`
# names is the shell's, not Windows's, /proc/<pid>/winpid was not always there to read, and a
# `wait` on a job whose Windows process was never killed hung check.sh for good. So PowerShell
# starts it (Start-Process -PassThru hands back the Windows process id itself), writes that id
# to a file before anything else, polls it for ten seconds, and stops that id alone. The whole
# PowerShell step runs under `timeout`, and if even that is killed the id in the file is
# stopped from here: nothing waits on anything that may never end.
SMOKE=0
SMOKE_PIDFILES=""
SMOKE_EXE_M=""   # the scratch copy's exe, as cygpath -m writes it, lower case
smoke_stop() {
    for f in $SMOKE_PIDFILES; do
        # no file: smoke.ps1 saw the app exit (or stopped it and saw it go) and deleted it
        [ -f "$f" ] || continue
        w=$(tr -dc 0-9 < "$f" 2>/dev/null)
        rm -f "$f"
        [ -n "$w" ] && [ -n "$SMOKE_EXE_M" ] || continue
        # the id still names the scratch copy? Anything else has taken a freed id, and is left alone
        path=$(powershell.exe -NoProfile -NonInteractive -Command \
                   "try { (Get-Process -Id $w -ErrorAction Stop).Path } catch { }" < /dev/null 2>/dev/null | tr -d "\r")
        [ -n "$path" ] || continue
        if [ "$(cygpath -m "$path" | tr "[:upper:]" "[:lower:]")" = "$SMOKE_EXE_M" ]; then
            taskkill //F //PID "$w" //FI "IMAGENAME eq OpenDejenol.exe" >/dev/null 2>&1
        else
            echo "  note  process $w is no longer the smoke copy ($path): left alone"
        fi
    done
    SMOKE_PIDFILES=""
}
trap smoke_stop EXIT INT TERM

smoke_run() {
    echo "building the app (Release)"
    # the build's exit status is kept: a failed or locked build would leave the last exe in place,
    # and the smoke would pass on an app that is not this tree's
    build_out=$("$DOTNET" build -c Release Mordor.App/Mordor.App.csproj 2>&1)
    build_rc=$?
    echo "$build_out" | grep -E " error |Build succeeded" | head -5
    if [ "$build_rc" -ne 0 ]; then
        if echo "$build_out" | grep -qE "MSB3026|MSB3027|MSB3021|being used by another process"; then
            echo "  FAIL  the Release build could not write its files: they are locked, most likely because the"
            echo "        Release app (Mordor.App/bin/Release) is running. Close it and run check.sh again."
        else
            echo "  FAIL  the Release build failed (exit $build_rc): the smoke would test a stale exe"
        fi
        return 1
    fi
    OUT=./Mordor.App/bin/Release/net8.0
    if [ ! -f "$OUT/OpenDejenol.exe" ] || [ ! -d "$OUT/mods" ]; then
        echo "  FAIL  no Release build with its mods folder at $OUT"
        return 1
    fi
    SCRATCH=$(mktemp -d "${TMPDIR:-/tmp}/mordor-smoke.XXXXXX") || { echo "  FAIL  no scratch folder"; return 1; }
    cp -r "$OUT" "$SCRATCH/app" || { echo "  FAIL  could not copy the build"; return 1; }
    SMOKE_EXE_M=$(cygpath -m "$SCRATCH/app/OpenDejenol.exe" | tr "[:upper:]" "[:lower:]")
    # the PowerShell side: start, record the id, poll, stop that id, say what it saw. Once the
    # process is seen gone the id file is deleted, so nothing afterwards stops a reused id; the
    # Stop-Process goes through the Process object it started, which names that process only
    cat > "$SCRATCH/smoke.ps1" <<"PS1"
param([string]$Exe, [string]$PidFile, [int]$Seconds = 10)
$p = Start-Process -FilePath $Exe -PassThru
Set-Content -Path $PidFile -Value $p.Id -Encoding ascii
$end = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $end -and -not $p.HasExited) { Start-Sleep -Milliseconds 250 }
if ($p.HasExited) {
    Remove-Item -LiteralPath $PidFile -ErrorAction SilentlyContinue
    "gone " + $p.ExitCode
}
else {
    "alive"
    try { $p.Kill() } catch { }
    if ($p.WaitForExit(10000)) { Remove-Item -LiteralPath $PidFile -ErrorAction SilentlyContinue }
    else { "left " + $p.Id }
}
PS1
    # the saves the game itself uses (docs/GAME_FILES.md, "Saves"), one to a line: beside the game
    # folder the test modes read, beside the folder this tree is in, the per-user ones under
    # %APPDATA% (OpenDejenol, and Mordor Port from before the rename), and MORDOR_REAL_SAVES if set
    REAL_SAVES="$(cd "$GAME/.." 2>/dev/null && pwd -W 2>/dev/null)/saves
$(cd .. 2>/dev/null && pwd -W 2>/dev/null)/saves"
    if [ -n "${APPDATA:-}" ]; then
        REAL_SAVES="$REAL_SAVES
$(cygpath -m "$APPDATA")/OpenDejenol/saves
$(cygpath -m "$APPDATA")/Mordor Port/saves"
    fi
    [ -n "${MORDOR_REAL_SAVES:-}" ] && REAL_SAVES="$REAL_SAVES
$(cygpath -m "$MORDOR_REAL_SAVES")"
    failed=0
    for mode in city dungeon; do
        saves="$SCRATCH/saves-$mode"
        log="$SCRATCH/crash-$mode.log"
        pidfile="$SCRATCH/pid-$mode"
        mkdir -p "$saves"
        : > "$log"
        saves_w=$(cygpath -w "$saves")
        scratch_m=$(cygpath -m "$saves" | tr "[:upper:]" "[:lower:]")
        while IFS= read -r real; do
            real=$(printf "%s" "$real" | tr "[:upper:]" "[:lower:]")
            case "$real" in ""|/saves) continue ;; esac
            case "$scratch_m" in
                "$real"|"$real"/*) echo "  FAIL  the scratch saves folder is the real one: $saves_w"; return 1 ;;
            esac
        done <<EOF
$REAL_SAVES
EOF
        printf "[Mod:QoL]\nEnabled=True\n" > "$saves/settings.ini"
        SMOKE_PIDFILES="$pidfile"
        # MORDOR_GAMEDIR: the copy runs from a scratch folder, where looking beside the port
        # (GameFolder, docs/GAME_FILES.md) would find no game_files
        said=$(MORDOR_TEST="$mode" MORDOR_MUTE=1 MORDOR_SAVES="$saves_w" MORDOR_GAMEDIR="$(cd "$GAME" && pwd -W)" \
            MORDOR_MODS="$(cygpath -w "$SCRATCH/app/mods")" MORDOR_CRASHLOG="$(cygpath -w "$log")" \
            MORDOR_ART_CACHE="$(cygpath -w "$SCRATCH/art")" \
            timeout 45 powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass \
                -File "$(cygpath -w "$SCRATCH/smoke.ps1")" \
                -Exe "$(cygpath -w "$SCRATCH/app/OpenDejenol.exe")" -PidFile "$(cygpath -w "$pidfile")" \
                < /dev/null 2>&1)
        # whatever PowerShell managed, the id it recorded is stopped again (a no-op if it is gone)
        smoke_stop
        alive=0
        case "$said" in *alive*) alive=1 ;; esac
        case "$said" in *left*) echo "  FAIL  $mode: the app would not stop ($said)"; failed=1 ;; esac
        if ! grep -q "Enabled=True" "$saves/settings.ini" 2>/dev/null; then
            echo "  note  $mode: settings.ini no longer switches QoL on"
        fi
        if [ "$alive" -ne 1 ]; then
            echo "  FAIL  $mode: the app was not up for ten seconds ($(echo $said))"
            failed=1
        fi
        if grep -qi "exception" "$log"; then
            echo "  FAIL  $mode: the crash log has an exception in it"
            grep -i -m 3 "exception" "$log" | sed "s/^/          /"
            failed=1
        fi
        if [ "$alive" -eq 1 ] && ! grep -qi "exception" "$log"; then
            echo "  ok    $mode: up for ten seconds, nothing in its crash log, stopped"
        fi
    done
    # the copy may take a moment to let go of its files after it is stopped
    for try in 1 2 3 4 5; do
        rm -rf "$SCRATCH" 2>/dev/null
        [ -e "$SCRATCH" ] || break
        sleep 1
    done
    [ -e "$SCRATCH" ] && echo "  note  could not remove $SCRATCH"
    return $failed
}


if [ "${MORDOR_CHECK_NO_UI:-}" = "1" ]; then
    echo "  skipped: MORDOR_CHECK_NO_UI=1 is set, so the app was not started (nothing failed)"
else
    smoke_run || SMOKE=1
fi

if [ -n "$BROKE" ] || [ "$CHECKED" -ne 0 ] || [ "$MODS" -ne 0 ] || [ "$UI" -ne 0 ] || [ "$SMOKE" -ne 0 ]; then echo; echo "SOMETHING IS WRONG"; exit 1; fi
echo
echo "all good"
