#!/usr/bin/env bash
# HomeStock installer (Linux / macOS).
#
# Downloads a released, self-contained HomeStock build for your platform and installs it — no
# .NET SDK required. Can also run the Docker deployment, or install a systemd service.
#
# Examples:
#   curl -fsSL https://raw.githubusercontent.com/mmatt21e/HomeEdge/main/scripts/install.sh | bash
#   ./install.sh --dir /opt/homestock --port 8080 --service
#   ./install.sh --docker
#
# Options:
#   --version <vX.Y.Z>   Release to install (default: latest)
#   --dir <path>         Install directory (default: ~/.homestock)
#   --port <port>        HTTP port (default: 8080)
#   --repo <owner/repo>  Source repo (default: mmatt21e/HomeEdge)
#   --service            Install and start a systemd service (Linux, needs sudo)
#   --docker             Use the Docker Compose deployment instead of the binary
#   --admin-password <p> Set the first-run admin password
set -euo pipefail

REPO="${HOMESTOCK_REPO:-mmatt21e/HomeEdge}"
INSTALL_DIR="${HOMESTOCK_DIR:-$HOME/.homestock}"
PORT="8080"
VERSION="latest"
SERVICE=0
MODE="binary"
ADMIN_PASSWORD="${HOMESTOCK_ADMIN_PASSWORD:-}"

while [ $# -gt 0 ]; do
  case "$1" in
    --version) VERSION="$2"; shift 2;;
    --dir) INSTALL_DIR="$2"; shift 2;;
    --port) PORT="$2"; shift 2;;
    --repo) REPO="$2"; shift 2;;
    --service) SERVICE=1; shift;;
    --docker) MODE="docker"; shift;;
    --admin-password) ADMIN_PASSWORD="$2"; shift 2;;
    -h|--help) grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0;;
    *) echo "Unknown option: $1" >&2; exit 1;;
  esac
done

need() { command -v "$1" >/dev/null 2>&1 || { echo "Required tool '$1' not found." >&2; exit 1; }; }
need curl; need tar

# ---- Docker mode ----
if [ "$MODE" = "docker" ]; then
  need git
  echo "Installing HomeStock via Docker Compose from $REPO ..."
  git clone --depth 1 "https://github.com/$REPO.git" "$INSTALL_DIR" 2>/dev/null || (cd "$INSTALL_DIR" && git pull)
  cd "$INSTALL_DIR"
  [ -f .env ] || cp .env.example .env
  [ -n "$ADMIN_PASSWORD" ] && sed -i.bak "s/^HOMESTOCK_ADMIN_PASSWORD=.*/HOMESTOCK_ADMIN_PASSWORD=$ADMIN_PASSWORD/" .env && rm -f .env.bak
  [ "$PORT" != "8080" ] && sed -i.bak "s/^HOMESTOCK_HTTP_PORT=.*/HOMESTOCK_HTTP_PORT=$PORT/" .env && rm -f .env.bak || true
  docker compose up -d --build
  echo "HomeStock is starting. Open http://<this-host-ip>:$PORT"
  exit 0
fi

# ---- Detect platform ----
OS="$(uname -s)"; ARCH="$(uname -m)"
case "$OS" in
  Linux)  RID_OS="linux";;
  Darwin) RID_OS="osx";;
  *) echo "Unsupported OS: $OS (use --docker)"; exit 1;;
esac
case "$ARCH" in
  x86_64|amd64) RID_ARCH="x64";;
  aarch64|arm64) RID_ARCH="arm64";;
  *) echo "Unsupported architecture: $ARCH"; exit 1;;
esac
RID="$RID_OS-$RID_ARCH"

# ---- Resolve version ----
if [ "$VERSION" = "latest" ]; then
  echo "Resolving latest release of $REPO ..."
  TAG="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep -oE '"tag_name": *"[^"]+"' | head -1 | sed -E 's/.*"tag_name": *"([^"]+)".*/\1/')"
  [ -n "$TAG" ] || { echo "Could not determine the latest release. Pass --version explicitly." >&2; exit 1; }
else
  TAG="$VERSION"
fi
VER="${TAG#v}"
ASSET="homestock-$VER-$RID.tar.gz"
URL="https://github.com/$REPO/releases/download/$TAG/$ASSET"

echo "Installing HomeStock $TAG ($RID) to $INSTALL_DIR"
TMP="$(mktemp -d)"
echo "Downloading $URL"
curl -fSL "$URL" -o "$TMP/$ASSET"

# Verify checksum if the sums file is published alongside the release.
if curl -fsSL "https://github.com/$REPO/releases/download/$TAG/SHA256SUMS-$VER.txt" -o "$TMP/SHA256SUMS.txt" 2>/dev/null; then
  ( cd "$TMP" && grep " $ASSET\$" SHA256SUMS.txt | sha256sum -c - ) && echo "Checksum OK"
else
  echo "Note: checksum file not found; skipping verification."
fi

mkdir -p "$INSTALL_DIR/app" "$INSTALL_DIR/data"
tar -xzf "$TMP/$ASSET" -C "$INSTALL_DIR/app"
chmod +x "$INSTALL_DIR/app/HomeStock.Web" 2>/dev/null || true
rm -rf "$TMP"

# Record install metadata for the upgrader.
cat > "$INSTALL_DIR/homestock.env" <<EOF
HOMESTOCK_REPO=$REPO
HOMESTOCK_VERSION=$TAG
HOMESTOCK_PORT=$PORT
EOF

echo "Installed HomeStock $TAG."

if [ "$SERVICE" = "1" ] && [ "$RID_OS" = "linux" ]; then
  UNIT=/etc/systemd/system/homestock.service
  echo "Installing systemd service (requires sudo) ..."
  sudo tee "$UNIT" >/dev/null <<EOF
[Unit]
Description=HomeStock Home Inventory
After=network.target

[Service]
WorkingDirectory=$INSTALL_DIR/app
ExecStart=$INSTALL_DIR/app/HomeStock.Web
Environment=ASPNETCORE_URLS=http://0.0.0.0:$PORT
Environment=ConnectionStrings__DefaultConnection=Data Source=$INSTALL_DIR/data/homestock.db;Cache=Shared
Environment=Storage__AttachmentsPath=$INSTALL_DIR/data/attachments
$( [ -n "$ADMIN_PASSWORD" ] && echo "Environment=Seed__AdminPassword=$ADMIN_PASSWORD" )
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF
  sudo systemctl daemon-reload
  sudo systemctl enable --now homestock
  echo "Service started. Open http://<this-host-ip>:$PORT"
else
  echo
  echo "Run it with:"
  echo "  cd $INSTALL_DIR/app && ASPNETCORE_URLS=http://0.0.0.0:$PORT \\"
  echo "    ConnectionStrings__DefaultConnection='Data Source=$INSTALL_DIR/data/homestock.db;Cache=Shared' \\"
  echo "    Storage__AttachmentsPath='$INSTALL_DIR/data/attachments' ./HomeStock.Web"
  echo
  echo "Then open http://<this-host-ip>:$PORT"
fi
