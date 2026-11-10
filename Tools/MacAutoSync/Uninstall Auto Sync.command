#!/bin/bash
set -euo pipefail
if [ "$(uname -s)" != Darwin ]; then echo 'Run this on your Mac.'; exit 1; fi
plist="$HOME/Library/LaunchAgents/com.ddes9902.code-autosync.plist"
/bin/launchctl bootout "gui/$(id -u)" "$plist" 2>/dev/null || true
rm -f "$plist"
echo 'Automatic sync disabled. Project scripts, logs and backups were kept.'
