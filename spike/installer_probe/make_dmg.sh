#!/bin/bash
# Throwaway. Turns the spike's `dotnet publish -r osx-arm64` output into an
# unsigned .app inside a .dmg, with hdiutil only. Prints the time of each step.
#
#   make_dmg.sh <publish dir> <renderer dylib> <out dir>
set -euo pipefail
PUBLISH="$1" DYLIB="$2" OUT="$3"
APP="$OUT/Spike.app"

step() { printf '%-28s' "$1"; STARTED=$(python3 -c 'import time; print(time.time())'); }
done_() { python3 -c "import time; print('%.2f s' % (time.time() - $STARTED))"; }

rm -rf "$OUT"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

step "copy publish output"
cp -R "$PUBLISH"/. "$APP/Contents/MacOS/"
done_

step "copy native renderer"
mkdir -p "$APP/Contents/MacOS/runtimes/osx-arm64/native"
cp "$DYLIB" "$APP/Contents/MacOS/runtimes/osx-arm64/native/"
done_

step "write Info.plist"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>Spike</string>
<key>CFBundleIdentifier</key><string>spike.surface</string>
<key>CFBundleExecutable</key><string>SurfaceSpike</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
done_

step "ad-hoc sign"
codesign --force --deep --sign - "$APP" 2>&1 | tail -1
done_

step "hdiutil create .dmg"
hdiutil create -quiet -volname Spike -srcfolder "$APP" -ov -format UDZO "$OUT/Spike.dmg"
done_

ls -l "$OUT/Spike.dmg"
du -sh "$APP"
