#!/usr/bin/env bash
# Builds the plugin and copies the DLL to dist/ with the versioned file name.
set -euo pipefail
cd "$(dirname "$0")"

VERSION="1.4.3.0"

dotnet build -c Release

cp "bin/Release/netstandard2.0/Emby.Plugin.TelegramNotification.dll" \
   "dist/Emby.Plugin.TelegramNotification-${VERSION}.dll"

echo "OK -> dist/Emby.Plugin.TelegramNotification-${VERSION}.dll"
