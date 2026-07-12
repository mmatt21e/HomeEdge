# Architecture Overview

## Layers

HomeStock uses a classic layered (clean-ish) architecture. Dependencies point inward only.

```
Domain  ◄──  Application  ◄──  Infrastructure  ◄──  Web
```

### HomeStock.Domain
Pure C# entities, enums, and invariants. No framework dependencies except the Identity base
class for `ApplicationUser`. `BaseEntity` supplies `Id`, `CreatedAt`, `UpdatedAt`;
`ISoftDeletable` marks entities that archive instead of delete.

### HomeStock.Application
The use-case layer. Contains:
- **DTOs / view models** (`Models/`) — never expose entities across the boundary.
- **Service interfaces + implementations** (`Abstractions/`, `Services/`) for categories,
  locations, items, dashboard, item history, and settings.
- **`IApplicationDbContext`** — a persistence abstraction (DbSets + `SaveChangesAsync`) so
  services depend on an interface, not the concrete EF context. This keeps the layer testable
  and provider-agnostic while avoiding an over-engineered repository-per-entity.
- **`Result`/`Result<T>`** — success + errors + non-fatal warnings (e.g. duplicate barcode).
- **`ModelValidator`** — centralized DataAnnotations validation used at every service entry.

Business rules live here: duplicate detection, move-cycle prevention, in-use delete guards,
history recording, search/filter composition, dashboard aggregation.

### HomeStock.Infrastructure
- **`ApplicationDbContext`** — extends `IdentityDbContext<ApplicationUser>` and implements
  `IApplicationDbContext`. Maintains `CreatedAt`/`UpdatedAt` centrally in `SaveChangesAsync`.
- **Entity configurations** — indexes, lengths, decimal precision, and delete behaviours.
- **Provider selection** — `AddInfrastructure` picks SQLite or PostgreSQL from configuration.
- **`DatabaseInitializer`** — migrates and seeds idempotently on startup.
- **`LocalFileStorageService`** — stores attachments on disk under server-generated, sharded,
  path-traversal-safe names (used from Phase 2).
- **`CodeGenerator`** — Crockford base32 codes for QR labels.

### HomeStock.Web
Blazor Web App (Interactive Server) + REST API controllers + Identity UI. Composes DI
(`AddInfrastructure` + `AddApplication`), configures auth policies, secure cookies, forwarded
headers, rate limiting, and health checks. `CurrentUserService` and `StorageHealthCheck` live
here because they depend on `HttpContext`.

## Rendering model

Pages opt into `@rendermode InteractiveServer` individually. Identity/account pages stay
statically rendered (required for cookie-setting form posts). The layout's interactive pieces
(theme toggle, toasts) inherit the page's mode; the global search is a plain GET form so it
works under both. This mirrors the supported Blazor Web App pattern and avoids "cannot set
render mode within an interactive context" pitfalls.

## Databases and migrations

The default install is **SQLite** — a single file under the data volume, zero configuration.
The provider is chosen by `Database:Provider` (`Sqlite` or `Postgres`) and the connection by
`ConnectionStrings:DefaultConnection`, so **switching to PostgreSQL is configuration, not code**.

Migrations are committed under `src/HomeStock.Infrastructure/Data/Migrations` and applied
automatically at startup by `DatabaseInitializer`. They are generated against SQLite for the
default install. Because provider SQL differs, PostgreSQL adopters generate a parallel set:

```bash
HOMESTOCK_MIGRATIONS_PROVIDER=Postgres \
dotnet ef migrations add InitialCreate \
  --project src/HomeStock.Infrastructure --startup-project src/HomeStock.Infrastructure \
  --output-dir Data/Migrations/Postgres
```

(`DesignTimeDbContextFactory` honours `HOMESTOCK_MIGRATIONS_PROVIDER` so `dotnet ef` works
without the web host.)

## Security posture

Identity with role tiers and policies (`CanEdit`, `AdminOnly`), login lockout + a per-IP login
rate limiter, antiforgery on state-changing forms, HttpOnly/SameSite/Secure-when-HTTPS cookies,
and forwarded-headers handling that trusts only configured proxies. Secrets come from
environment/config, never source. See [SECURITY.md](SECURITY.md).

## Health & reliability

- `/health/live` — process liveness. `/health/ready` — database + attachment storage.
- Automatic DB initialization and controlled migrations on boot.
- Transactions via `SaveChangesAsync`; deliberate FK delete behaviours; soft delete for
  important records; duplicate warnings; logging that never emits passwords/secrets.
