# Releases, Installers & Upgrades

HomeStock ships **downloadable installers/upgraders** so you can run it without installing the
.NET SDK. Releases are produced automatically by CI when a version tag is pushed.

## What a release contains

Each GitHub Release (created from a `vX.Y.Z` tag) includes:

- **Self-contained, single-file builds** for every supported platform — the .NET runtime is
  bundled, so there are no prerequisites to install:
  - `homestock-<version>-linux-x64.tar.gz`
  - `homestock-<version>-linux-arm64.tar.gz` (Raspberry Pi 64-bit, ARM servers)
  - `homestock-<version>-win-x64.zip`
  - `homestock-<version>-osx-x64.tar.gz`, `homestock-<version>-osx-arm64.tar.gz`
- **`SHA256SUMS-<version>.txt`** — checksums the installer/upgrader verify automatically.
- A **Docker image** at `ghcr.io/<owner>/<repo>:<version>` (and `:latest`).
- The **install/upgrade scripts** are bundled inside each archive, and can also be curled
  straight from the tag.

Each archive also contains `RUNME.txt` and the install/upgrade helpers.

## Install (recommended: one command)

**Linux / macOS**

```bash
curl -fsSL https://raw.githubusercontent.com/mmatt21e/HomeEdge/main/scripts/install.sh | bash
# or with options:
./install.sh --dir /opt/homestock --port 8080 --service --admin-password 'strong-pw'
```

The installer detects your OS/architecture, downloads the matching release, verifies its
checksum, installs to `~/.homestock` (or `--dir`), creates a `data/` folder, and — with
`--service` on Linux — registers and starts a **systemd** service.

**Windows (PowerShell)**

```powershell
irm https://raw.githubusercontent.com/mmatt21e/HomeEdge/main/scripts/install.ps1 | iex
# or:
powershell -ExecutionPolicy Bypass -File install.ps1 -Dir 'C:\HomeStock' -Port 8080 -Service
```

**Docker**

```bash
docker run -d --name homestock -p 8080:8080 -v homestock-data:/data \
  -e Seed__AdminPassword='strong-pw' \
  ghcr.io/mmatt21e/homeedge:latest
```

Or use the repo's `docker compose up -d` (see
[LOCAL_NETWORK_DEPLOYMENT.md](LOCAL_NETWORK_DEPLOYMENT.md)).

## Manual install

1. Download the archive for your platform from the Releases page.
2. (Optional) verify it: `sha256sum -c SHA256SUMS-<version>.txt`.
3. Extract it and run `./HomeStock.Web` (Linux/macOS) or `HomeStock.Web.exe` (Windows).
   See the bundled `RUNME.txt` for environment variables (port, admin password, data path).

## Upgrade

Upgrading **never touches your data folder**, and database migrations apply automatically on the
next startup — there is no manual migration step.

**Linux / macOS**

```bash
./upgrade.sh                 # upgrade the install in ~/.homestock to the latest release
./upgrade.sh --dir /opt/homestock --version v1.3.0
```

`upgrade.sh` backs up your `data/` folder to `<install>/backups/` first, downloads the target
release (with checksum verification), swaps the application binaries in place, and restarts the
service if one is installed.

**Windows** — re-run `install.ps1` (it replaces the app in place; your `data\` folder is kept).

**Docker** — `docker compose pull && docker compose up -d`, or pull the new image tag.

## Cutting a release (maintainers)

Releases are driven by the `Release` GitHub Actions workflow (`.github/workflows/release.yml`):

```bash
# Bump the version (optional — CI derives it from the tag) and tag:
git tag v0.2.0
git push origin v0.2.0
```

The workflow then:

1. Builds all platform artifacts via `scripts/publish-all.sh` (settable version).
2. Builds and pushes the Docker image to GHCR.
3. Creates the GitHub Release with all artifacts, checksums, and auto-generated notes.

You can also run it manually (`workflow_dispatch`) for a dry run that uploads artifacts without
creating a release. To build artifacts locally:

```bash
scripts/publish-all.sh 0.2.0 artifacts
```

The application reports its running version under **Settings → Instance**.
