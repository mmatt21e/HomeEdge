# Local Network Deployment Guide

HomeStock is designed to run on a computer or small server inside your home and be reachable
from any phone, tablet, or computer on the same network.

## Prerequisites

- A machine that stays on (mini PC, NAS, Raspberry Pi 4/5, spare laptop).
- **Docker** with the Compose plugin (`docker compose version` should work), _or_ the
  **.NET 10 SDK** for a direct run.

## Option A — Docker Compose (recommended)

```bash
git clone <your-fork-or-copy> homestock && cd homestock
cp .env.example .env
# Edit .env: set a strong HOMESTOCK_ADMIN_PASSWORD (and change the port if 8080 is taken).
docker compose up -d --build
```

Check it is healthy:

```bash
docker compose ps           # STATUS should show "healthy"
docker compose logs -f homestock
```

### Accessing the app

1. **By IP address (always works).** Find the host's LAN IP:
   - Linux/macOS: `ip addr` or `ifconfig` → look for `192.168.x.x` / `10.x.x.x`.
   - Windows: `ipconfig` → "IPv4 Address".
   Then browse to `http://<that-ip>:8080` from any device on the network.

2. **By `homestock.local` (if mDNS is available).** Many networks resolve `<hostname>.local`
   via mDNS/Bonjour/Avahi. If your host's name is `homestock`, `http://homestock.local:8080`
   may work from Apple devices and modern Windows/Linux. **Do not rely on this** — `.local`
   resolution is not guaranteed on every network or device. When in doubt, use the IP.

   To make a friendly name reliable, either:
   - Add a DNS entry / DHCP reservation on your router, or
   - Add a `hosts` file entry on each client (`192.168.1.50  homestock.local`), or
   - Install Avahi (`avahi-daemon`) on a Linux host to advertise `homestock.local`.

### Data persistence

The SQLite database and all uploaded attachments live in the Docker named volume
`homestock-data` (mounted at `/data`). They survive `docker compose down`/rebuilds. Only the
HTTP port is published — **the database is never exposed on the network**. Backups go to
`./deploy/backups` (mounted at `/backups`). See [BACKUP_RESTORE.md](BACKUP_RESTORE.md).

### Updating

```bash
git pull
docker compose up -d --build      # migrations apply automatically on startup
```

## Option B — Run with the .NET SDK

```bash
dotnet publish src/HomeStock.Web -c Release -o ./publish
cd publish
# Bind to all interfaces so other devices can reach it:
ASPNETCORE_URLS="http://0.0.0.0:8080" \
Seed__AdminPassword="<strong-password>" \
dotnet HomeStock.Web.dll
```

Run it as a service (systemd example):

```ini
# /etc/systemd/system/homestock.service
[Unit]
Description=HomeStock
After=network.target
[Service]
WorkingDirectory=/opt/homestock/publish
ExecStart=/usr/bin/dotnet /opt/homestock/publish/HomeStock.Web.dll
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=Seed__AdminPassword=CHANGE_ME
Restart=always
[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable --now homestock
```

## Firewall

Allow inbound TCP on your chosen port (8080 by default) on the host, e.g.:

```bash
sudo ufw allow 8080/tcp
```

## Troubleshooting

| Symptom | Fix |
|--------|-----|
| Can't reach from another device | Use the IP not `.local`; check host firewall; confirm same subnet |
| "healthy" never reached | `docker compose logs homestock`; check the data volume is writable |
| Forgot admin password | `docker compose logs homestock \| grep -A4 "INITIAL ADMIN"` (first run only), or set `Seed__AdminPassword`, wipe the volume, and restart to reseed |
| Port already in use | Change `HOMESTOCK_HTTP_PORT` in `.env` |
