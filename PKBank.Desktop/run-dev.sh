#!/usr/bin/env bash
# Runs PKBank.Desktop from this git checkout with `dotnet run`, so the desktop
# shortcut always builds and starts the current working tree.
# Any arguments (e.g. a save file path) are forwarded to the app.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
log="${XDG_RUNTIME_DIR:-/tmp}/pkhex-dev.log"

# The launcher has no visible stdout: keep the build/run output in a log and
# surface failures as a notification instead of exiting silently.
if ! dotnet run --project PKBank.Desktop.csproj -- "$@" >"$log" 2>&1; then
    command -v notify-send >/dev/null 2>&1 &&
        notify-send --app-name="PKHeX (Dev)" --urgency=critical "PKHeX (Dev) failed to start" "See $log"
    exit 1
fi
