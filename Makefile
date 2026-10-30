# Shortcuts for the commands CI and docs/TOOLS.md already describe; nothing here is new behaviour.
#
#   make verify             what CI runs: restore, Release build with warnings as errors, dotnet test.
#                           With no game folder every suite skips itself, so this proves it builds.
#   make check GAME=<dir>   the suites and window checks against the player's own Mordor folder,
#                           under dotnet test (tests/Mordor.Tests.Runner). Works on macOS and Windows.
#   make check-full GAME=<dir>
#                           tools/check.sh, the full gate (adds the tools' sweep and the app's
#                           start-up smoke). Windows only: it runs .exe files, powershell and cygpath.
#   make mac-app            tools/pack-macos.sh: dist/macos/OpenDejenol.app, arm64, signed ad hoc.
#
# GAME defaults to game_files/, a local copy that .gitignore keeps out of git. The game's files are
# never committed (docs/GAME_FILES.md).

DOTNET ?= dotnet
GAME ?= game_files
SLN := Mordor.sln

export DOTNET_NOLOGO := true
export DOTNET_CLI_TELEMETRY_OPTOUT := true

.PHONY: verify restore build test check check-full mac-app clean

verify: build test

restore:
	$(DOTNET) restore $(SLN)

build: restore
	$(DOTNET) build $(SLN) -c Release --no-restore -warnaserror

test:
	$(DOTNET) test $(SLN) -c Release --no-build

check: build
	@test -d "$(GAME)" || { echo "no game folder at $(GAME): pass GAME=/path/to/Mordor"; exit 1; }
	MORDOR_GAMEDIR="$(abspath $(GAME))" $(DOTNET) test $(SLN) -c Release --no-build

check-full:
	sh tools/check.sh "$(GAME)"

mac-app:
	sh tools/pack-macos.sh

clean:
	$(DOTNET) clean $(SLN) -c Release
