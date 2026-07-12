# HomeStock

A self-hosted **home inventory** web application. Track, locate, value, and audit the items in
your household — from any phone, tablet, or computer on your local network. Runs on your own
machine, keeps your data on your own disk, and is built so you can add secure remote access
later without rewriting anything.

> **Status:** Phase 1 complete (foundations: auth, database, items, categories, locations,
> responsive UI, Docker). Phases 2–4 (scanning, uploads, dashboard extras, lending, audits,
> reports, PWA/remote hardening) are planned — see [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md).

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

24 unit tests cover the category, location, item, and dashboard business logic (duplicate
detection, history recording, move-cycle prevention, search/filtering, aggregation) against a
real in-memory SQLite database.

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

The **Scan**, **Audits**, and **Reports** sections are present in the navigation and clearly
signposted as arriving in later phases (they are not dead buttons).

---

## Documentation

| Guide | Contents |
|-------|----------|
| [Implementation plan](docs/IMPLEMENTATION_PLAN.md) | Phased roadmap and data model |
| [Phase 1 checklist](docs/PHASE1_CHECKLIST.md) | What was delivered in Phase 1 |
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
