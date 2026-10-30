# Mobile and cloud saves - spec (not yet built)

Status: proposal, 2026-10-04. Companion to docs/MODDING.md. Built since: M3 for macOS only, as the iCloud Saves
mod (2026-10-30, "M3 on the Mac today" below); the rest is not implemented.

## Goals

1. Play the same port on an Android or iOS **tablet** first, a **phone** second.
2. Keep one game, one set of rules: `Mordor.Core` is shared unchanged, so fidelity carries over.
3. Saves that follow you between the Windows PC and a mobile device ("cloud saves").
4. Nothing of the original's copyrighted material ships in an app: the player brings their own copy.

## Where the code stands (what helps, what blocks)

| Area | State | Mobile impact |
|---|---|---|
| Rules (`Mordor.Core`) | .NET 8, no UI, one Windows call (`lstrcmpiW` in LibrarySearch, which already has a managed fallback) | Runs as-is on Android/iOS |
| UI framework | Avalonia 11.2.3 | Avalonia has Android and iOS targets; the same XAML/C# compiles |
| Window model | Already one top-level window with an MDI frame inside (`MdiFrame`) | Good: mobile apps have one view. But many dialogs are separate `Window`s (OriginalBox, NamePicker, PasswordBox, Options, Help, Exchange, Hall, Mods, a mod's settings window) and must become in-view overlays |
| Code structure | `CityWindow` / `DungeonWindow` are large code-behind classes mixing layout, input and game flow | The main cost: a phone layout needs the flow separated from the desktop layout |
| Audio | `winmm.dll` (WAV through the port's own 8-channel mixer over waveOut, MIDI via MCI) | Must be replaced: a cross-platform player for WAV, and a software MIDI synth (e.g. MeltySynth + a General MIDI SoundFont) |
| Other Windows APIs | `user32` (foreground/frame), `comdlg32` ChooseFont, `gdi32` | Replace with Avalonia equivalents (font picker from installed fonts; no foreground tricks) |
| Mods | AssemblyLoadContext + Harmony | Android: plugin loading works under the Mono JIT, Harmony partly. iOS: no runtime code loading or JIT, so mods can only be compiled in (the QoL mod built into the app, its toggles kept) |
| Game data | Read from the player's `game_files` at runtime | Needs an import step on mobile (below) |
| Bundled originals | None in the build since 2026-10: pictures and help are extracted from the player's copy at first run (docs/GAME_FILES.md). Still in the repo: `decompiled*/`, and git history holds the old Assets | A public repo needs a fresh history (no Assets/help in old commits) and a decision on `decompiled*/` |

## Phase M0 - feasibility spike (about 1-2 days of agent work)

- Split the app into heads: `Mordor.App` (shared UI library), `Mordor.App.Desktop` (today's exe), `Mordor.App.Android`; iOS later (needs a Mac or a cloud Mac for builds).
- `ISingleViewApplicationLifetime` on Android; the MDI frame as the root view.
- Stub audio (silent) and the Windows-only calls behind an interface.
- Import: a file picker for a zip or folder of the player's Mordor install (Android Storage Access Framework), copied into app storage; verify MDATA*.MDR / MORDOR.EXE / WAV / MID by name and size.
- Run in the Android emulator on a tablet profile: town, dungeon, a fight, save, reload.
- Exit: a list of what broke (paths, fonts, dialogs, input), with sizes for the later phases.

## Phase M1 - tablet, landscape (about 1-2 weeks)

The original's 800x600 desktop layout fits a 10-11" tablet in landscape almost unchanged.

- **Dialogs as overlays**: one modal-overlay host inside the root view; every `Window`-based dialog becomes a content panel shown in it (OriginalBox, NamePicker, PasswordBox, Exchange, Hall, Options, Mods, a mod's settings window, Help).
- **Touch input**:
  - tap = click; double-tap = double-click (Use, swap); long-press = right-click (automap re-plot) and tooltips;
  - drag threshold from the touch tap size (about 10 px), not the mouse's 4;
  - an on-screen movement pad (turn left/right, forward, turn round; Shift-step via a hold modifier) over the dungeon view;
  - a compact command bar for keyboard-only commands (Pool, Swap, Search, Pickup/Drop body, Cast, Leave...) - every key in `fn1521` reachable by touch;
  - an optional hardware keyboard works as on desktop.
- **Sizes**: minimum touch target about 40 DIP: the form-drawn windows grow through the existing `FormGrowth` mechanism with a "touch" scale, rather than per-window edits.
- **Audio**: WAV player + MeltySynth MIDI with a bundled free GM SoundFont (or the player's own .sf2).
- **Fonts**: MS Sans Serif / Courier New are not on Android/iOS: ship free metric-compatible stand-ins (e.g. a sans and a monospace with permissive licences) as the defaults; the Options font list reads installed fonts.
- **Mods**: Android loads mods from the app's mods folder; iOS builds the QoL mod in.
- **Saves**: app-private storage; export/import of the save folder as a zip (also the bridge to cloud saves until M3).

## Phase M2 - phone layout (about 2-4 weeks)

A phone cannot hold the MDI desktop. A separate **mobile shell** over the same game flow:

- One full-screen panel at a time, switched by a bottom tab bar: **Dungeon** (view + movement pad + monster strip + message line), **Party**, **Character**, **Items**, **Spells**, **Map**, **Messages**.
- Town services (Store, Guild, Bank, Morgue, Seer, Confinement, Library) as full-screen pages with the same controls stacked vertically.
- Prerequisite refactor: move the game flow out of `CityWindow`/`DungeonWindow` code-behind into controllers both shells drive (input → command, state → view). This is most of the cost, and it also makes the desktop code easier to test.
- Portrait and landscape.

## Phase M3 - cloud saves

### What a "save" is

The saves folder is a whole **world**, shared by every character in it, as in the original's multi-user design:

| File | Size today | Kind |
|---|---|---|
| `dungeon.json` | ~650 KB | world: every level's rooms, bodies, chests |
| `automap.json` | ~70 KB | world: the shared map |
| `library.json`, `store.json`, `records.json`, `confinement.json`, `guildmasters.json`, `MDATA9.MDR`, `roster.json` | small | world |
| `<name>.json` | ~10-15 KB each | one per character |
| `party-<leader>.json` | tiny | party lists |
| `settings.ini`, `mods.log` | tiny | this device's settings (do not sync `mods.log`) |

About 0.9 MB in all, roughly 100-150 KB gzipped. Writes happen on every area change and on quit.

### The hard part: two devices, one world

Character files and the world files change together, and the original never merged two copies of a world. So sync should not try to merge:

- **One active device at a time.** Starting the game "checks out" the world (a lease with device name and time). Another device that starts sees "This world is open on HOME-PC since 21:04 - Wait / Take over / Play offline".
- **Take over** keeps the other device's last upload as the truth, and saves the local unsynced copy as a dated conflict copy (never deletes).
- **Every upload is versioned** (keep the last N snapshots), so any bad sync can be rolled back from a "Restore an earlier save" list.
- **Upload points**: on quit, on returning to town, and every few minutes of play if changed; **download** at start (and before taking over).
- **Offline** play is allowed; it uploads next time, with the same conflict rule.

### Where cloud saves could live

| Option | Cross-platform (PC, Android, iOS) | Backend to run | Cost | Notes |
|---|---|---|---|---|
| **A. A synced folder** (OneDrive / Dropbox / Google Drive desktop folder, iCloud Drive) chosen as the save folder | PC only, practically; mobile file providers don't keep a live folder in sync | None | Free | Works on desktop today with one setting (the port already supports `MORDOR_SAVES`). Risk: the sync client can write a conflict copy mid-session. Needs the lease file to be safe. |
| **B. The player's own cloud drive through its API** (OneDrive/Graph, Google Drive, Dropbox) | Yes | None (OAuth app registration only) | Free | Saves stay in the player's account; we hold no data. One provider to start (OneDrive fits a Windows user; Google Drive fits Android). OAuth sign-in on each device. |
| **C. Platform game services**: Google Play Games Saved Games, Apple GameKit/iCloud | No: Android-only or Apple-only | None | Free | Built for this (snapshots, conflict callbacks), but each locks the save to one ecosystem and neither reaches the PC. Requires store distribution. |
| **D. Our own small backend**: Supabase (Postgres + Storage + Auth), Firebase (Firestore/Storage + Auth), or Cloudflare (Workers + R2 + D1) | Yes | Yes (small) | Free tier covers personal use; pennies beyond | Full control: leases, versions, a "shared world" with friends later. But we would hold user data and run a service: accounts, security, backups. |

**Recommendation:**
1. **Now (desktop):** option A as a "Save folder" setting with a lease file and conflict detection - a few days, no services.
2. **With mobile:** option **B with one provider** (OneDrive or Google Drive, your choice) implementing the lease/versions above in a hidden app folder of the player's own drive. No backend, no data held by us, works on PC, Android and iOS.
3. **Only if you want shared worlds between different players** (the original's multi-user idea): option D, since leases and permissions between people need a server.

### M3 on the Mac today: the iCloud Saves mod

Option A of the table, done as a mod for the macOS build (docs/MODDING.md, "The iCloud Saves mod"; off by default,
macOS only). It keeps the saves folder where it is and copies whole-world snapshots to and from
`~/Library/Mobile Documents/com~apple~CloudDocs/OpenDejenol/` - the iCloud Drive folder, plain files, synced by
macOS - because CloudKit and an app's iCloud container need entitlements and a provisioning profile that an ad hoc
signed app cannot have. Of the design above it follows: whole worlds, nothing merged; every upload versioned (each Mac
keeps its own last ten); upload on returning to town and on quit, download (offered) at start; offline play with the
same conflict rule; conflicts never decided silently, both copies kept (the other in iCloud as an earlier version, or
in a `saves-backup-*` folder). Not built: the lease ("This world is open on HOME-PC since 21:04"); a conflict is
caught at the next save point or start instead, and only then. Nor "every few minutes of play": the game's own save
points are the upload points.

### Apple Games / Game Center (investigated, not built)

What GameKit (achievements, leaderboards, the access point) would take for the macOS build:

- **An entitlement**, `com.apple.developer.game-center`, in the app's signature - and an entitlement Apple honours
  needs a **provisioning profile**, which needs a **paid Apple Developer Program** membership (99 USD a year). The ad
  hoc signature `tools/pack-macos.sh` makes cannot carry it: GameKit refuses to authenticate the player
  (`GKLocalPlayer.authenticateHandler` returns an error) without it.
- **An App Store Connect app record** for the bundle id, with Game Center switched on and each achievement and
  leaderboard defined there (ids, titles, points, images). Whether a Developer ID build (notarised, given out
  directly, outside the Mac App Store) may use Game Center is to be checked against Apple's current "Supported
  capabilities" table before any work starts - it has been a Mac App Store-only capability - and that decides how
  the game would be distributed.
- **Signing for distribution**: Developer ID or Mac App Store signing, hardened runtime, notarisation.
- **Calling GameKit from .NET**: Avalonia has no binding. Either (a) the Objective-C runtime from C# directly -
  `objc_getClass`, `sel_registerName` and `objc_msgSend` P/Invoked from `/usr/lib/libobjc.dylib`, with blocks for the
  completion handlers (workable for a handful of calls, fiddly for blocks); (b) a **small native shim** - a few Swift
  or Objective-C functions with C entry points (`gc_authenticate`, `gc_report_achievement(id, percent)`,
  `gc_submit_score(board, value)`, `gc_show_access_point(bool)`) built as a dylib inside the app bundle and
  P/Invoked, which is the least code and the easiest to keep working; or (c) the .NET macOS workload (`net8.0-macos`,
  Microsoft's Apple bindings, GameKit included), which would mean moving the macOS head of the app to that target
  framework - a large change for this alone. (b) is the one to choose.
- **Authentication and UI**: the player signs in with their Apple Account in the system's Game Center sheet, which
  needs an NSWindow to present over; Avalonia's main window on macOS is one (its native handle), so the shim can be
  handed it.
- **Fidelity**: the original has no achievements or leaderboards (docs/FIDELITY.md: nothing invented), so any would
  be a mod's addition, off by default like every mod - e.g. a mod listening to `ExperienceGained`, `DamageDealt` and
  `ArrivedInTown` that reports milestones through the shim. Game Center's saved games (`GKSavedGame`) also exist, but
  they need the same entitlement and an iCloud container, so the iCloud Drive mod is what works without a paid
  account.

## Licensing and distribution (before anything public)

- Private use (sideloaded APK, your own iPad via a free/paid Apple developer account): fine as the repo stands.
- Public store release or public repo: the images and help are no longer in the build (extracted from the player's copy at first run, docs/GAME_FILES.md); still to do: a fresh git history without the old Assets, a decision on `decompiled*/`, and the rights to Mordor's name.
- iOS: no mods at runtime (built-in QoL only); Apple review rules on emulator-like apps don't apply (it's a native app), but the "bring your own game files" step must be clear.

## Open decisions for you

1. Tablet-only first (M0-M1), or go straight to phone (M2)?
2. Android first (recommended: faster builds, sideloading) or iOS?
3. Cloud provider for option B: OneDrive or Google Drive?
4. Is a desktop "Save folder" setting (option A) wanted now as a stopgap?
