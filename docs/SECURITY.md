# Security Configuration Guide

## Accounts & roles

- **Local installs still require login.** There is no anonymous access to inventory data.
- Roles: **Administrator** (full control, incl. hard delete and settings), **Standard user**
  (create/edit items, categories, locations), **Read-only** (view + search only).
- Self-registered users receive no elevated role (effectively read-only) until an administrator
  assigns one. Administrators manage users at **Settings → Manage users & roles** (`/settings/users`):
  assign/remove roles, enable/disable accounts, create users, reset passwords, and delete —
  with guards against removing/disabling/deleting the last administrator or your own account.
  Disabling an account locks it out of sign-in immediately.
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
- `SameSite=Lax` also means the auth cookie is **not** sent on cross-site POST/PUT/DELETE, which
  mitigates CSRF against the REST API (no CORS is enabled either).
- Behind TLS (reverse proxy / tunnel / VPN with HTTPS), set `EnableHttpsRedirection=true`. HSTS
  (1 year, includeSubDomains) is sent over HTTPS.

## Security response headers

Every response includes defensive headers (see `Security` config section):

- `Content-Security-Policy` — restricts scripts/styles/images/connections to same-origin (plus
  `data:` images for QR codes and `blob:` workers for the scanner). It permits `'unsafe-inline'`
  for the Blazor import map and scoped styles; tighten with nonces if you expose HomeStock
  publicly. Toggle with `Security:EnableContentSecurityPolicy`, or override
  `Security:ContentSecurityPolicy`.
- `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` (anti-clickjacking),
  `Referrer-Policy: strict-origin-when-cross-origin`, `Cross-Origin-Opener-Policy: same-origin`,
  and a `Permissions-Policy` that allows the camera (for scanning) but disables microphone and
  geolocation.

## Data protection at rest

- ASP.NET Core **data-protection keys** (used to encrypt auth cookies/tokens) are persisted to
  `DataProtection:KeysPath` (the `/data/keys` volume in Docker) so logins survive restarts.
- By default the keys are stored **unencrypted at rest** — appropriate for a single self-hosted
  instance. Protect the data volume with filesystem permissions and back it up. To encrypt the
  keys, configure a key-encryption provider (e.g. certificate or platform key store).

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

## Email

Password-reset and confirmation emails are sent over **SMTP** when the `Email` section is
configured (`Email:Host` + `Email:From`, with optional `Username`/`Password`/`UseSsl`/`Port`).
If it's left blank, no email is sent — fine for a LAN install where login needs no email
confirmation, but "forgot password" then can't deliver a link (an admin can reset passwords from
the user-management screen instead). SMTP failures are logged, never surfaced to the user.

## Recommended operational hygiene

- Strong, unique passwords; per-person accounts; least-privilege roles.
- Keep the host OS and the container image patched (`docker compose pull`/rebuild).
- Regular, tested backups ([BACKUP_RESTORE.md](BACKUP_RESTORE.md)).
- For remote access, prefer a VPN/private network; see [REMOTE_ACCESS.md](REMOTE_ACCESS.md).
- Enable two-factor authentication (Settings → Two-factor) for accounts reachable remotely.

## Reporting

For a real deployment, add a `SECURITY.md` contact/policy describing how to report issues.
