#!/usr/bin/env bash
# HomeStock upgrader (Linux / macOS).
#
# Backs up your data, downloads the target release, replaces the application binaries in place,
# and restarts the service. The database schema is migrated automatically on the next startup,
# so no manual migration step is needed. Your data folder is never touched by the upgrade.
#
# Examples:
#   ./upgrade.sh                       # upgrade the install in ~/.homestock to the latest
#   ./upgrade.sh --dir /opt/homestock --version v1.3.0
set -euo pipefail

INSTALL_DIR="${HOMESTOCK_DIR:-$HOME/.homestock}"
VERSION="latest"
REPO="${HOMESTOCK_REPO:-mmatt21e/HomeEdge}"

while [ $# -gt 0 ]; do
  case "$1" in
    --dir) INSTALL_DIR="$2"; shift 2;;
    --version) VERSION="$2"; shift 2;;
    --repo) REPO="$2"; shift 2;;
    -h|--help) grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0;;
    *) echo "Unknown option: $1" >&2; exit 1;;
  esac
done

[ -d "$INSTALL_DIR/app" ] || { echo "No HomeStock install found at $INSTALL_DIR. Run install.sh first." >&2; exit 1; }
[ -f "$INSTALL_DIR/homestock.env" ] && . "$INSTALL_DIR/homestock.env"
REPO="${HOMESTOCK_REPO:-$REPO}"

command -v curl >/dev/null || { echo "curl is required." >&2; exit 1; }

OS="$(uname -s)"; ARCH="$(uname -m)"
case "$OS" in Linux) RID_OS="linux";; Darwin) RID_OS="osx";; *) echo "Unsupported OS"; exit 1;; esac
case "$ARCH" in x86_64|amd64) RID_ARCH="x64";; aarch64|arm64) RID_ARCH="arm64";; *) echo "Unsupported arch"; exit 1;; esac
RID="$RID_OS-$RID_ARCH"

if [ "$VERSION" = "latest" ]; then
  TAG="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep -oE '"tag_name": *"[^"]+"' | head -1 | sed -E 's/.*"([^"]+)".*/\1/')"
else
  TAG="$VERSION"
fi
VER="${TAG#v}"
ASSET="homestock-$VER-$RID.tar.gz"

echo "Upgrading HomeStock in $INSTALL_DIR to $TAG ($RID)"

# 1) Back up the data folder first.
if [ -d "$INSTALL_DIR/data" ]; then
  BK="$INSTALL_DIR/backups"; mkdir -p "$BK"
  STAMP="$(date +%Y%m%d-%H%M%S)"
  echo "Backing up data to $BK/data-$STAMP.tar.gz"
  tar -czf "$BK/data-$STAMP.tar.gz" -C "$INSTALL_DIR" data
fi

# 2) Stop the service if present.
SERVICE_ACTIVE=0
if command -v systemctl >/dev/null && systemctl is-active --quiet homestock 2>/dev/null; then
  SERVICE_ACTIVE=1; echo "Stopping service ..."; sudo systemctl stop homestock
fi

# 3) Download and swap the app directory atomically.
TMP="$(mktemp -d)"
URL="https://github.com/$REPO/releases/download/$TAG/$ASSET"
echo "Downloading $URL"
curl -fSL "$URL" -o "$TMP/$ASSET"
if curl -fsSL "https://github.com/$REPO/releases/download/$TAG/SHA256SUMS-$VER.txt" -o "$TMP/SHA256SUMS.txt" 2>/dev/null; then
  ( cd "$TMP" && grep " $ASSET\$" SHA256SUMS.txt | sha256sum -c - ) && echo "Checksum OK"
fi
mkdir -p "$TMP/app"
tar -xzf "$TMP/$ASSET" -C "$TMP/app"
chmod +x "$TMP/app/HomeStock.Web" 2>/dev/null || true

rm -rf "$INSTALL_DIR/app.old"
mv "$INSTALL_DIR/app" "$INSTALL_DIR/app.old"
mv "$TMP/app" "$INSTALL_DIR/app"
rm -rf "$TMP" "$INSTALL_DIR/app.old"

# 4) Record the new version and restart.
sed -i.bak "s/^HOMESTOCK_VERSION=.*/HOMESTOCK_VERSION=$TAG/" "$INSTALL_DIR/homestock.env" 2>/dev/null && rm -f "$INSTALL_DIR/homestock.env.bak" || true

if [ "$SERVICE_ACTIVE" = "1" ]; then
  echo "Starting service ..."; sudo systemctl start homestock
  echo "Upgraded to $TAG. Migrations apply automatically on startup."
else
  echo "Upgraded to $TAG. Start HomeStock again to apply database migrations automatically."
fi
