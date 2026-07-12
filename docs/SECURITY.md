# Security Configuration Guide

## Accounts & roles

- **Local installs still require login.** There is no anonymous access to inventory data.
- Roles: **Administrator** (full control, incl. hard delete and settings), **Standard user**
  (create/edit items, categories, locations), **Read-only** (view + search only).
- Self-registered users receive no elevated role (effectively read-only) until an administrator
  assigns one. A dedicated user-management screen is planned; until then, roles can be assigned
  directly via Identity (e.g. an admin-run SQL/EF update on `AspNetUserRoles`).
- Authorization is enforced by policies: `CanEdit` (Admin + Standard) guards create/edit/delete
  actions in both the UI and the REST API; `AdminOnly` guards hard delete and settings.

## Secrets

- **Never hardcode secrets.** The admin password and any database password come from environment
  variables / `.env` / secure configuration.
- If `Seed:AdminPassword` is blank, a strong random password is generated on first run and
  written to the log **once** — retrieve it, log in, and change it. Set the variable explicitly
  to control it.
- `.env` is git-ignored; keep it off version control and restrict its file permissions.
- Logs never include passwords or secrets.

## Transport & cookies

- Authentication cookies are `HttpOnly` and `SameSite=Lax`. Their `Secure` flag follows the
  request scheme (`SameAsRequest`) so plain-HTTP LAN use works while HTTPS gets secure cookies.
- Behind TLS (reverse proxy / tunnel / VPN with HTTPS), set `EnableHttpsRedirection=true`.

## Abuse resistance

- **Login rate limiting** — a per-IP fixed-window limiter on the login endpoint.
- **Account lockout** — Identity locks an account after 5 failed attempts for 5 minutes.
- **CSRF** — antiforgery tokens on all state-changing form posts.
- **Output encoding** — Blazor encodes rendered output by default; no raw HTML from user input.
- **Input validation** — centralized DataAnnotations validation at every service boundary, plus
  ASP.NET model validation on the API.

## File uploads (Phase 2 storage layer, already implemented)

- Stored under **server-generated** names (never the client filename), sharded, and confined to
  the storage root (path-traversal is rejected).
- Size and extension validation with a safe default allow-list; configurable via `Storage:*`.

## Reverse proxies

- Forwarded headers are honoured **only** from IPs listed in `ReverseProxy:KnownProxies`. With
  the list empty (default), forwarded headers from arbitrary clients are ignored, preventing
  IP/scheme spoofing.

## Data exposure

- The database is **not** published on the network in any provided deployment. Only the HTTP
  port is exposed; with PostgreSQL, the DB stays on the internal compose network.

## Recommended operational hygiene

- Strong, unique passwords; per-person accounts; least-privilege roles.
- Keep the host OS and the container image patched (`docker compose pull`/rebuild).
- Regular, tested backups ([BACKUP_RESTORE.md](BACKUP_RESTORE.md)).
- For remote access, prefer a VPN/private network; see [REMOTE_ACCESS.md](REMOTE_ACCESS.md).
- Enable two-factor authentication (Settings → Two-factor) for accounts reachable remotely.

## Reporting

For a real deployment, add a `SECURITY.md` contact/policy describing how to report issues.
