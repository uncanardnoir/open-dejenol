# How the original draws the dungeon view

Read out of `MAZEMOD.BAS` (`mdrmazShowMaze`) and `MODULE1.BAS`. This is not a perspective
projection - it is a fixed composition of pre-drawn pieces, which is why the extracted artwork is
in odd 50x100 slabs rather than tileable textures.

## The canvas

A **100 x 100 offscreen buffer** (`gc1D12` x `gc1D14`), built up in place and then stretched to fill
the picture box at the end. Everything below is in that 100 x 100 space.

## The two background halves

The corridor behind everything is two 50-wide columns, each made of two 50x50 tiles stacked:

    left  half: sheet column (leftIndex  - 1), rows 2 and 3   -> buffer (0,0) and (0,50)
    right half: sheet column (rightIndex - 1), rows 0 and 1   -> buffer (50,0) and (50,50)

so the corridor sheet is a grid of 50x50 cells: **rows 0-1 are the right-hand wall, rows 2-3 the
left-hand wall**, and the column selects which variant. When an index is 0 a plain backdrop bitmap
is used for that half instead. When the square is dark (a bit tested on the square's flags) all
four quadrants are filled from a single dark tile and nothing else is drawn.

## The eight overlay slots

Eight wall pieces are then stamped on top, each a fixed size cut from one 118-wide strip:

| slot | width | height | source x |
|---|---|---|---|
| 0 | 11 | 79 | 0 |
| 1 | 5 | 53 | 11 |
| 2 | 40 | 53 | 16 |
| 3 | 10 | 28 | 56 |
| 4 | 7 | 28 | 66 |
| 5 | 18 | 28 | 73 |
| 6 | 9 | 52 | 91 |
| 7 | 18 | 28 | 100 |

Each slot has a left variant (bit 1 of its flag word) and a right variant (bit 2). The right
variant reads from a second strip that starts at `slot6.srcX + slot6.width`, i.e. 100 further along.

Placement, with `H = 100` the buffer height and `w`,`h` the slot's size:

| slot | left variant x | y |
|---|---|---|
| 0 | 0 | H - h |
| 1 | 0 | H - h - 16 |
| 2 | 50 - w/2 | H - h - 16 |
| 3 | 16 - w | H - h - 32 |
| 4 | 17 | H - h - 32 |
| 5 | w / 3 | H - h - 32 |
| 6 | 25 - w/2 | H - h - 20 |
| 7 | 50 - w/2 | H - h - 32 |

The right variants roughly mirror these about the buffer's centre line, with their own small
offsets, and take their source column 100 pixels along - that 100 is not a magic number but
`gv1BB6(6).M425F + gv1BB6(6).M4216`, the end of slot 6's artwork. Two details are easy to miss and
both are in the source rather than inferred:

- Slots 2 and 7 have **no case at all** in the right-hand `Select Case` (MODULE3.BAS:1412-1437),
  so they are never drawn on the right however the selection logic flags them.
- Slot 7's *left* variant draws slot 5's artwork: MODULE3.BAS:1408 passes `gv1BB6(5).M425F`, not
  its own source column. Slot 7's own column is therefore never used.

The divisions are floating-point in VB and round to even when they land in an `Integer`, which
changes exactly one placement: slot 6's left `x` is `25 - 9/2` = **20**, not 21.

## The artwork, identified

`MG11.DLL` is a 16-bit NE module whose resource table names each bitmap, so there is no need to
guess from dimensions. [`tools/NeRes`](../tools/NeRes) reads the table and prints it:

| name | size | what it is | bound at |
|---|---|---|---|
| `MAZE` | 1250 x 200 | the corridor sheet - 25 columns by 4 rows of 50x50 | MODULE12.BAS:81 |
| `DOOR` | 200 x 79 | the overlay strip - 100 wide of left-hand slabs then 100 of right | MODULE12.BAS:90 |
| `BLNK` | 100 x 100 | the plain backdrop used when a half has no column | MODULE12.BAS:102 |
| `FOG` | 50 x 50 | tiled four times when the square is unlit | MODULE12.BAS:99 |
| `WATR` | 50 x 20 | a strip across the bottom of both halves when the square is water | MODULE12.BAS:93 |
| `QSND` | 50 x 18 | the same, for quicksand | MODULE12.BAS:96 |
| `MONS` | 600 x 540 | the monster sprite sheet | - |
| `MISC`, `PICS`, `ASMO` | | interface furniture | - |

The port reads them out of the player's MG11.DLL at start (src/Mordor.Core/Extraction/GameArt.cs, docs/GAME_FILES.md)
as `maze_corridor.png`, `maze_overlays.png`, `maze_backdrop.png`, `maze_dark.png`,
`maze_water.png` and `maze_quicksand.png` in its art cache.

## Choosing what to draw

`sub0980` decides the frame in three stages, all of which the port now runs:

1. **Fill the frustum.** From the party's square it walks outwards using a forward step `m01B6`
   and a left step `m01B8` derived from the facing, and records what it finds in `v?01FE(1..22)`.
   A wall kind goes in as *kind + 10*, so a value of 10 to 15 names one of the six wall bits; a
   value of 1 marks the edge of the map. Squares off the 30x30 grid are simply read as whatever
   record the linear index lands on, and out-of-range indices are skipped.
2. **Prune.** The `GoSub L14530` block drops entries that nearer geometry hides - a wall in slot 5
   or 3 hides 10, 14 and 22, a near door hides what is behind it, and so on. Twenty-two rules.
3. **Reduce to what is drawable.** A long chain of tests turns the frustum into the two background
   column indices `m01D2(0..1)` and the eight slot words `gv1B72(1..8)`, each carrying bit 1 for
   the left-hand variant and bit 2 for the right.

That is about five hundred conditionals. Rather than transcribe it, [`tools/VbToCs`](../tools/VbToCs)
translates it mechanically into `src/Mordor.Core/MazeSelect.g.cs`; run
[`tools/gen-mazeselect.sh`](../tools/gen-mazeselect.sh) to regenerate. Anything the translator does
not recognise becomes an `UNTRANSLATED` comment rather than passing silently, and the slice
currently yields none.

Two shapes in the source are not plain comparisons and are handled explicitly, because VB's
`And`/`Or` are bitwise and a comparison yields -1 rather than 1:

- `... And v?01FE(7) Then` uses a raw value as a truth test. With every other operand being -1 or
  0 and the test being for non-zero, that is exactly `&& V[7] != 0`.
- `If v?01FE(10) > 0 = 0 And ...` folds left: it asks whether `V[10] > 0` is *False*.

The translator refuses (and marks `UNTRANSLATED`) if a chain ever holds two raw values, because
then bitwise `And` would no longer agree with `&&`.

## Coordinates

The original indexes the map as one flat run, `x + 30 * (y - 1)`, with x and y 1-based, x growing
east and **y growing north**. Facing is 1 to 4 for north, east, south, west - fixed by
MODULE3.BAS:1711, which sets the facing to `mc00D2` when the square carries bit 6, `FACE_NORTH`.
The port's `Floor` already stores each square's raw word with the original's bit numbering and
lays the grid out the same way, so `RenderView` hands `MazeSelect` the flags unchanged.
