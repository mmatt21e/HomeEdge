# Phase 1 Checklist

Everything below is implemented, builds with **zero warnings**, and is covered by a running app
and/or unit tests.

## Solution & architecture
- [x] Layered solution: `Domain`, `Application`, `Infrastructure`, `Web`, plus `Tests`
- [x] Dependency injection throughout; business logic kept out of UI components
- [x] DTOs / view models at the application boundary
- [x] Centralized validation (`ModelValidator`) and `Result`/`Result<T>` at service boundaries
- [x] Structured logging via `ILogger`
- [x] Async database and file operations end-to-end

## Database
- [x] EF Core 10 with SQLite; provider abstraction ready for PostgreSQL (`Database:Provider`)
- [x] All entities + configurations (indexes, max lengths, decimal precision, delete rules)
- [x] Many-to-many tags via a real join table (no comma-separated columns)
- [x] Initial migration; **automatic migration + seeding on startup** (`DatabaseInitializer`)
- [x] Seed: roles, first-run admin (random password if unset, logged once), 14 categories,
      default settings, optional sample data

## Authentication & security
- [x] ASP.NET Core Identity; local install still requires login
- [x] Three roles: Administrator, Standard user, Read-only, with authorization policies
- [x] Secure password hashing (Identity default), login lockout + login rate limiting
- [x] Secure cookies (HttpOnly, SameSite, Secure-when-HTTPS), CSRF/antiforgery, output encoding
- [x] No hardcoded secrets; admin credentials via environment/config
- [x] Audit logging of important changes (`ItemHistory`) and app logs without secrets

## Item inventory
- [x] Create / view / edit / archive / restore / delete, with all specified fields
- [x] Fast entry — only Name is required
- [x] Per-item change history (created/updated/status-changed/archived/restored)
- [x] Duplicate serial-number and barcode **warnings** (non-blocking)

## Categories & locations
- [x] Configurable categories (seeded defaults + user CRUD, archive, in-use delete protection)
- [x] Hierarchical locations: create, rename, move (with cycle prevention), archive, delete
- [x] Unique location `Code` + generated **QR label** that opens the location's contents

## Search, dashboard, UI
- [x] Case-insensitive search across all key fields; category/location/status/warranty/archived filters
- [x] Dashboard: totals, quantity, estimated value, expiring warranties, loaned, missing photo,
      no-location, recent activity, counts by category and location
- [x] Responsive mobile-first UI: sidebar (desktop) + bottom nav (mobile), large touch targets
- [x] Dark / light / system themes, toasts, confirm dialogs, loading + empty states,
      field-level validation messages
- [x] Search available from every page (top bar)

## API & operability
- [x] REST API (`/api/items`, `/api/categories`, `/api/locations`, `/api/dashboard`) sharing the
      same services as the UI, secured by the same policies
- [x] Health checks: `/health/live`, `/health/ready` (database + attachment storage), `/health`
- [x] Friendly error page; reverse-proxy forwarded-headers support wired for the future
- [x] Installable PWA shell (manifest, icons, offline fallback) — no false offline data claims

## Deployment
- [x] `Dockerfile` (multi-stage, non-root, HEALTHCHECK) and `docker-compose.yml`
- [x] Persistent data volume for the SQLite DB **and** attachments; database not published to LAN
- [x] `.env.example`, environment-variable configuration, backups directory
- [x] Optional PostgreSQL service (compose profile), kept off the network

## Verification performed
- [x] `dotnet build HomeStock.slnx` — succeeds, 0 warnings
- [x] `dotnet test` — 24/24 pass
- [x] App boots, applies migrations, seeds, and serves; login → dashboard/items/locations/
      categories all render; REST API create/search/duplicate-warning/validation verified;
      health endpoints report healthy
- [x] `dotnet publish -c Release` — succeeds (the image build step) with all static assets
