# HomeStock

A self-hosted **home inventory** web application. Track, locate, value, and audit the items in
your household — from any phone, tablet, or computer on your local network. Runs on your own
machine, keeps your data on your own disk, and is built so you can add secure remote access
later without rewriting anything.

> **Status:** Phases 1–3 complete. Phase 1 delivered the foundations (auth, database, items,
> categories, locations, responsive UI, Docker); **Phase 2** added photo/document uploads,
> phone-camera barcode/QR scanning, CSV/JSON export, a guided CSV import wizard, and
> **downloadable installers/upgraders** with an automated release pipeline; **Phase 3** adds
> lending, location audits with discrepancy reports, printable inventory/insurance reports,
> backup & restore, and a change-activity feed. Phase 4 (PWA/reverse-proxy/MFA hardening,
> security review) is planned — see [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md).

---

## Tech stack

| Layer | Technology |
|------|------------|
| UI | Blazor Web App (.NET 10, Interactive Server) + Bootstrap 5.3, mobile-first |
| API | ASP.NET Core controllers (`/api/**`) — same services as the UI |
| Data | Entity Framework Core 10, **SQLite** by default, **PostgreSQL**-ready |
| Auth | ASP.NET Core Identity (roles, lockout, secure cookies) |
| Deploy | Docker + Docker Compose, health checks, persistent volumes |

Architecture is layered: **Domain → Application → Infrastructure → Web**. See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## Download & install (released builds)

No .NET SDK needed — released builds bundle the runtime. One-command install:

**Linux / macOS**
```bash
curl -fsSL https://raw.githubusercontent.com/mmatt21e/HomeEdge/main/scripts/install.sh | bash
```

**Windows (PowerShell)**
```powershell
irm https://raw.githubusercontent.com/mmatt21e/HomeEdge/main/scripts/install.ps1 | iex
```

**Docker image**
```bash
docker run -d -p 8080:8080 -v homestock-data:/data \
  -e Seed__AdminPassword=change-me ghcr.io/mmatt21e/homeedge:latest
```

Upgrade later with `./upgrade.sh` (Linux/macOS) or by re-running the installer — your data is
preserved and migrations apply automatically. Releases (self-contained installers for
Windows/Linux/macOS + checksums + Docker image) are produced automatically from version tags.
Full details: [`docs/RELEASES.md`](docs/RELEASES.md).

---

## Quick start (Docker — recommended)

Requires Docker with Compose v2.

```bash
# 1. Configure the first-run admin password (and any other settings)
cp .env.example .env
#   edit .env and set HOMESTOCK_ADMIN_PASSWORD to a strong value

# 2. Build and start
docker compose up -d --build

# 3. Find this machine's LAN IP and open the app
#    e.g. http://192.168.1.50:8080
```

If you left `HOMESTOCK_ADMIN_PASSWORD` blank, a strong random password is generated on first run
and printed to the logs:

```bash
docker compose logs homestock | grep -A4 "INITIAL ADMIN"
```

Sign in with `admin@homestock.local` (or your configured email) and that password, then change it
under **Settings → Change password**.

Full instructions, `.local` hostname setup, and troubleshooting:
[`docs/LOCAL_NETWORK_DEPLOYMENT.md`](docs/LOCAL_NETWORK_DEPLOYMENT.md).

---

## Quick start (local .NET SDK — for development)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# From the repository root:
dotnet restore HomeStock.slnx
dotnet build   HomeStock.slnx

# Run the web app (Development seeds sample data + a known admin password)
dotnet run --project src/HomeStock.Web
```

Then open the URL printed in the console (e.g. `http://localhost:5xxx`).

In **Development** the seed creates:

- Admin login **`admin@homestock.local`** / **`Admin123!`** (set in `appsettings.Development.json`)
- 14 default categories, and a few sample locations and items

> The database and attachments are written to `src/HomeStock.Web/data/` (git-ignored). Delete that
> folder to reset to a clean install.

### Run the tests

```bash
dotnet test HomeStock.slnx
```

43 unit tests cover the category, location, item, dashboard, attachment, import/export, lending,
audit, and backup/restore business logic (duplicate detection, history recording, move-cycle
prevention, search/filtering, aggregation, file storage, CSV mapping/validation/commit, loan
lifecycle, audit outcomes, additive restore) against a real in-memory SQLite database.

---

## Database migrations

Migrations live in `src/HomeStock.Infrastructure/Data/Migrations` and are **applied
automatically on startup**. To create a new migration after changing entities:

```bash
dotnet tool install --global dotnet-ef            # once
dotnet ef migrations add <Name> \
  --project src/HomeStock.Infrastructure \
  --startup-project src/HomeStock.Infrastructure \
  --output-dir Data/Migrations
```

Migrations are generated against SQLite (the default install). PostgreSQL users generate their
own set — see [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#databases-and-migrations).

---

## Features (Phase 1)

- **Items** — full CRUD with 25+ fields (identifiers, purchase/value, warranty, condition,
  location, container, tags, notes), archive/restore, hard delete (admin), and a per-item
  **change history**.
- **Search & filters** — case-insensitive text search across name, description, manufacturer,
  model, serial, barcode, category, location, tags, and notes; filter by category, location
  (with sub-locations), status, warranty state, and archived state.
- **Categories** — 14 seeded defaults plus your own; colour-coded; archive and delete with
  in-use protection.
- **Hierarchical locations** — unlimited nesting (Home › Garage › Cabinet › Drawer), move with
  cycle protection, and a **QR label** per location that opens its contents when scanned.
- **Dashboard** — totals, estimated value, warranties expiring soon, loaned/missing-photo/
  no-location counts, recent activity, and breakdowns by category and location.
- **Duplicate warnings** — non-blocking alerts when a serial number or barcode is already used.
- **Roles & security** — Administrator / Standard user / Read-only, authorization policies,
  login lockout + rate limiting, secure cookies, CSRF protection, health checks.
- **Responsive UI** — mobile-first, large touch targets, bottom nav on phones, sidebar on
  desktop, **dark/light/system themes**, toasts, confirmations, empty states, loading
  indicators, and an installable PWA shell.

## Features (Phase 3)

- **Lending** — loan items out (borrower, contact, dates, notes), record returns, and keep a full
  lending history per item; status flips to *Loaned* while out; overdue loans are flagged.
- **Inventory audits** — start an audit for a location (optionally including sub-locations),
  work through the expected items marking each **Confirmed / Missing / Moved / Damaged** (scan a
  barcode to confirm quickly), then complete to apply outcomes to the items and produce a
  **discrepancy report**. Full audit history is kept.
- **Printable reports** — inventory and insurance reports, filtered by category/location, print or
  save-as-PDF, with record/quantity/value totals.
- **Backup & restore** — full JSON backup plus a validated, **non-destructive** restore that
  rebuilds categories, the location hierarchy, tags and items (skipping ones already present).
- **Activity feed** — review the latest changes across all items at `/activity`.

## Features (Phase 2)

- **Photos & documents** — attach multiple files per item (photos, serial-number shots, receipts,
  warranties, manuals, insurance docs). Binaries are stored outside the database under safe,
  server-generated filenames with size/type validation; downloads go through an authorized
  endpoint. Thumbnails and a gallery appear on each item; adds/removals are recorded in history.
- **Barcode & QR scanning** — scan UPC/EAN barcodes and QR codes with the phone camera (bundled,
  offline-capable library). A scan opens the matching item, offers to **create** a new item
  pre-filled with the code, or **assigns** the code to an existing item; HomeStock location QR
  labels open that location's contents. A manual-entry fallback covers cameraless devices.
- **CSV & JSON export** — download items as CSV (insurance-ready) or a full JSON backup.
- **CSV import wizard** — upload → **column mapping** (auto-detected) → **validation** with
  per-row errors/warnings → **duplicate detection** → **confirmation** before anything is saved.
- **Released installers/upgraders** — self-contained one-command install and in-place upgrade
  for Windows/Linux/macOS, plus a published Docker image, via an automated release pipeline.

---

## Documentation

| Guide | Contents |
|-------|----------|
| [Implementation plan](docs/IMPLEMENTATION_PLAN.md) | Phased roadmap and data model |
| [Phase 1 checklist](docs/PHASE1_CHECKLIST.md) | What was delivered in Phase 1 |
| [Phase 2 checklist](docs/PHASE2_CHECKLIST.md) | What was delivered in Phase 2 |
| [Phase 3 checklist](docs/PHASE3_CHECKLIST.md) | What was delivered in Phase 3 |
| [Releases & installers](docs/RELEASES.md) | Downloadable installers, upgrades, cutting releases |
| [Architecture](docs/ARCHITECTURE.md) | Layers, DI, data model, migrations |
| [Local network deployment](docs/LOCAL_NETWORK_DEPLOYMENT.md) | Docker, IP/hostname access |
| [Backup & restore](docs/BACKUP_RESTORE.md) | Backing up and restoring data |
| [Remote access preparation](docs/REMOTE_ACCESS.md) | Tailscale, WireGuard, Cloudflare, reverse proxy |
| [Security configuration](docs/SECURITY.md) | Secrets, roles, hardening |
| [User guide](docs/USER_GUIDE.md) | Day-to-day usage |

---

## Configuration reference

All settings can be supplied via `appsettings.json`, environment variables (double-underscore
nesting, e.g. `Seed__AdminPassword`), or Docker `.env`. Secrets should come from environment
variables — never commit them.

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:DefaultConnection` | `Data Source=data/homestock.db` | Database connection |
| `Database:Provider` | `Sqlite` | `Sqlite` or `Postgres` |
| `Storage:AttachmentsPath` | `data/attachments` | Where uploaded files are stored |
| `Storage:MaxFileSizeBytes` | `20971520` (20 MB) | Upload size limit |
| `Seed:AdminEmail` | `admin@homestock.local` | First-run admin username |
| `Seed:AdminPassword` | _(random if blank)_ | First-run admin password |
| `Seed:SeedSampleData` | `false` | Insert demo items/locations |
| `EnableHttpsRedirection` | `false` | Force HTTPS (enable behind TLS only) |
| `ReverseProxy:KnownProxies` | `[]` | Trusted proxy IPs for forwarded headers |

---

## License

Provided as-is for self-hosting. Add your preferred license here.
