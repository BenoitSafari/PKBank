#!/usr/bin/env bash
# Builds the PKBank AppImage.
#
# Usage: packaging/appimage/build-appimage.sh [version]
#   version   x.y.z stamped into the binaries and the file name; defaults to <Version> in
#             Directory.Build.props.
#
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
arch="${ARCH:-x86_64}"

case "$arch" in
    x86_64) rid=linux-x64 ;;
    aarch64) rid=linux-arm64 ;;
    *) echo "Unsupported ARCH: $arch" >&2; exit 1 ;;
esac

version="${1:-}"
if [ -z "$version" ]; then
    version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -n1)"
fi
if ! printf '%s' "$version" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
    echo "Version must be x.y.z, got “$version”" >&2
    exit 1
fi

out="$root/artifacts"
appdir="$out/AppDir"
image="$out/PKBank-$version-$arch.AppImage"

rm -rf "$appdir" "$image"
mkdir -p "$appdir/usr/bin"

echo "==> Publishing PKBank.Desktop $version ($rid)"
dotnet publish "$root/PKBank.Desktop/PKBank.Desktop.csproj" \
    --configuration Release \
    --runtime "$rid" \
    --self-contained true \
    -p:Version="$version" \
    -p:DebugType=none \
    --output "$appdir/usr/bin"

echo "==> Assembling the AppDir"
install -m 755 "$here/AppRun" "$appdir/AppRun"

# appimagetool wants the desktop entry and the icon at the root; the usr/share copies are what a
# desktop integration tool (or the user) installs.
install -m 644 "$here/pkbank.desktop" "$appdir/pkbank.desktop"
install -D -m 644 "$here/pkbank.desktop" "$appdir/usr/share/applications/pkbank.desktop"

icon="$root/packages/PKHeX/icon.png"
if [ ! -f "$icon" ]; then
    echo "Missing $icon — run: git submodule update --init --recursive" >&2
    exit 1
fi
install -m 644 "$icon" "$appdir/pkbank.png"
install -m 644 "$icon" "$appdir/.DirIcon"
install -D -m 644 "$icon" "$appdir/usr/share/icons/hicolor/256x256/apps/pkbank.png"

# The published apphost is PKBank.Desktop; AppRun starts it, the .desktop entry only names the app.
chmod +x "$appdir/usr/bin/PKBank.Desktop"

# Kept out of the way of artifacts/*.AppImage, which is what the release picks up.
tools="$out/.tools"
tool="$tools/appimagetool-$arch.AppImage"
if [ ! -x "$tool" ]; then
    echo "==> Fetching appimagetool"
    mkdir -p "$tools"
    curl -fsSL -o "$tool" \
        "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-$arch.AppImage"
    chmod +x "$tool"
fi

echo "==> Packing $image"
# No FUSE on a CI runner: appimagetool unpacks itself instead of mounting.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH="$arch" "$tool" "$appdir" "$image"

echo "==> Done: $image"
