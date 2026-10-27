# Releasing OpenDejenol

A release is made by GitHub Actions from a tag pushed to the public repository,
[github.com/uncanardnoir/open-dejenol](https://github.com/uncanardnoir/open-dejenol). Nothing of
the game's is in it: like the source, a release is only the engine, and the player points it at
their own copy of Mordor.

## What a release holds

| file | what it is |
|---|---|
| `OpenDejenol.Game-win-Setup.exe` | the installer: Windows x64, for the current user, no administrator needed. Installs into `%LOCALAPPDATA%\OpenDejenol.Game`, with an OpenDejenol shortcut on the desktop and in the Start menu; removed again from Settings > Apps |
| `OpenDejenol.Game-win-Portable.zip` | the same program to unzip anywhere and run (`OpenDejenol.exe`); it updates itself in place, like an installed copy |
| `OpenDejenol.Game-<version>-full.nupkg`, `RELEASES`, `releases.win.json` | the update feed: what installed copies download and read |
| `OpenDejenol.Game-<version>-delta.nupkg` | from the second release on: the change from the previous one, a much smaller download |

The program is self-contained (no .NET to install) and not a single file: the bundled mods are in
`mods\` beside it, as in a build (docs/MODDING.md). Each copy carries `LICENSE.txt` (GPL-3.0).

The packages are made by [Velopack](https://velopack.io): `vpk`, at the version pinned in
`.config/dotnet-tools.json`, run by `tools/pack-release.sh`. That script is the whole recipe, and
CI runs it on every push (without uploading anything), so a tree that cannot be packed is caught
before it is tagged. To see what a release would hold, run it yourself (Windows, .NET 8 SDK):

    sh tools/pack-release.sh ../release-test

It publishes into `../release-test/publish`, checks the publish (the program, each bundled mod's
dll and mod.json, the licence, no file of the game's, no path of your user folder) and packs into
`../release-test/releases`, listing each file's size and SHA-256. It never runs the program or the
installer.

## Cutting a release

1. **Set the version.** It is `<Version>` in `src/Mordor.App/Mordor.App.csproj`, the one place it
   is set: SemVer, `0.2.0` for a release, `0.2.0-beta.1` (or `-alpha.N`, `-rc.N`) for a test
   release. Never reuse a version that has been published.
2. **Check.** The Release build has no warning, and `sh tools/check.sh <your Mordor folder>` ends
   in `all good`.
3. **Commit** the change to `main` of the public repository and push it.
4. **Tag and push the tag** - `v` and the version, exactly:

       git tag -a v0.2.0 -m "OpenDejenol 0.2.0"
       git push origin v0.2.0

   The workflow refuses a tag that is not `v` + the version in the .csproj.
5. **The workflow** (`.github/workflows/release.yml`, the Actions tab) builds and tests the
   solution, fetches the previous release to make a delta package against, packs, and uploads
   everything to a GitHub release named "OpenDejenol 0.2.0" on that tag:
   - a tag with a suffix (`v0.2.0-beta.1`) is **published at once as a pre-release**;
   - a plain tag (`v0.2.0`) becomes a **draft**. Download its Setup.exe (or the copy attached to
     the workflow run), try it, write the release notes, and press Publish. Nobody is offered a
     draft.
6. **If it fails**, read the run's log. A failure before the upload leaves nothing behind: fix,
   move the tag (`git tag -d`, `git push origin :refs/tags/v0.2.0`, tag again) and push it again.
   A failure after the release was made: delete that release on GitHub first.

## Updates

An installed (or portable) copy asks GitHub once at start, in the background, whether there is a
newer release (`src/Mordor.App/Updates.cs`). If there is, the main menu says "A new version x.y.z
is available" with **Update now** and **Later**; Update now downloads it, closes the game, applies
the update and starts the new version. Later asks again at the next start. With no network, or
GitHub not answering, nothing at all is shown. A copy run from a build folder or `dotnet run` is
not installed and never checks.

- A stable copy is offered stable releases only; a copy that is itself a test release is offered
  test releases too. The launch settings (`%APPDATA%\OpenDejenol\settings.ini`) can say otherwise:

      [Updates]
      Prereleases=True    ; or False
      Check=False         ; no check at all

- Only published releases are offered (not drafts), and only newer ones.
- The package id, `OpenDejenol.Game`, is fixed for good: installed copies find their updates by
  it. It also names the install folder, and is chosen so that folder is not the player's own
  `%LOCALAPPDATA%\OpenDejenol` (the picture cache): an update replaces only the install folder,
  and an uninstall removes only it. The saves, the settings and crash log in
  `%APPDATA%\OpenDejenol` (or beside the game folder), the cache, the game folder and the mods
  beside it are never touched (docs/GAME_FILES.md).

## Signing (not yet)

The releases are not code-signed, so Windows SmartScreen warns on the first run of the installer
("Windows protected your PC": More info, Run anyway) until the download has a reputation. Signing
is a later step; the workflow has a place for it:

- **A certificate, or Azure Trusted Signing**: give vpk what it needs and it signs the program's
  files and the installer as it packs - `VPK_SIGN_PARAMS` (signtool parameters) or
  `VPK_SIGN_TEMPLATE` (any signing command, `{{file}}` for the file), set from repository secrets
  in the Package step's `env:`; for Azure Trusted Signing, vpk's `--azureTrustedSignFile`.
- **[SignPath](https://signpath.org)** (free signing for open-source projects): SignPath signs
  artifacts uploaded from a workflow run, so the run would publish unsigned, submit the publish
  folder for signing (signpath/github-action-submit-signing-request), pack the signed files, then
  submit the Setup.exe the same way.

Whichever it is, it replaces the "Sign (not set up)" step in `release.yml`, which does nothing
unless the repository variable `SIGNING` is `true` - and then fails the run, so switching signing
on without filling the step in cannot ship an unsigned release by mistake.

## What GitHub needs

- Actions enabled for the repository. The workflows ask for their own permissions (`contents:
  read` for CI, `contents: write` for the release, to make the release and upload to it) and use
  only the built-in `GITHUB_TOKEN`: no secret is needed.
- Nothing in the workflows needs the game: CI's `dotnet test` runs the test project, whose suites
  all skip without `MORDOR_GAMEDIR`. The full check, `tools/check.sh`, is run locally against a
  Mordor folder.
