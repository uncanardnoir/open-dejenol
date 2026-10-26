#!/bin/sh
# Regenerates src/Mordor.Core/MazeSelect.g.cs and MazeSelectPrune.g.cs from the decompiled source.
#
# The slice is sub0980 in MODULE3.BAS (MAZEMOD), the procedure that decides what the dungeon view
# shows. Boundaries, all verified against the listing:
#
#   846-1288   the selection proper, starting at the Select Case that turns the facing into the
#              forward and sideways steps, and ending at the last GoTo L14A56
#   1061       GoSub L14530, replaced by the PRUNECALL sentinel the translator turns into Prune(v)
#   1291-1312  the L14530 block: drops squares that nearer squares hide
#   1315-1351  L14A56, which walks the frustum and turns it into the eight slot words
#
# Line 845 and before is set-up the caller does; 1352 onwards is the blitting, which MazeView does.
set -e
here=$(dirname "$0")
src="$here/../decompiled/MODULE3.BAS"
out="$here/../src/Mordor.Core"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

awk 'NR>=846 && NR<=1288 { if (NR==1061) print "PRUNECALL"; else print }
     NR>=1315 && NR<=1351' "$src" > "$tmp/sel_main.vb"
awk 'NR>=1291 && NR<=1312' "$src" > "$tmp/sel_prune.vb"

dotnet run --project "$here/VbToCs" -- "$tmp/sel_main.vb"  "$out/MazeSelect.g.cs"      Select --entry
dotnet run --project "$here/VbToCs" -- "$tmp/sel_prune.vb" "$out/MazeSelectPrune.g.cs" Prune
