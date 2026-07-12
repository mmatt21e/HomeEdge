# Phase 4 Checklist

Everything below is implemented, builds with **zero warnings**, and is covered by the running app
and/or automated tests (48/48 passing, including in-process integration tests).

## Progressive Web App
- [x] Installable on Android/iOS/Windows/desktop (manifest + maskable icons already in place)
- [x] **Install prompt** handling (`beforeinstallprompt`) with an in-app **Install app** button
      (Settings → Appearance & install); detects already-installed/standalone mode
- [x] Improved offline **app shell** service worker (versioned cache, icon font cached, offline
      fallback page); graceful "you're offline" page when the server is unreachable
- [x] Touch-friendly, responsive UI (Phase 1) and camera access for scanning (Phase 2)
- [x] No false offline claims — data entry still requires the server, and that is stated

## Reverse-proxy & remote access
- [x] Forwarded headers honour `X-Forwarded-For/Proto/Prefix` from **configured trusted proxies only**
- [x] **Sub-path hosting**: `X-Forwarded-Prefix` or `ReverseProxy:BasePath` sets `PathBase`, and the
      `<base href>` follows it so links stay correct behind a proxy
- [x] Configurable HTTPS redirect (off by default for plain-HTTP LAN) and **HSTS** (1 year,
      includeSubDomains) over HTTPS
- [x] Remote-access options documented (Tailscale, WireGuard, Cloudflare Tunnel, reverse proxy)

## Multi-factor authentication
- [x] TOTP authenticator-app 2FA (ASP.NET Core Identity) with recovery codes — reachable and
      surfaced with a recommendation in **Settings → Security**

## Security hardening
- [x] **Security headers** on every response: `Content-Security-Policy` (Blazor-compatible),
      `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`,
      `Permissions-Policy` (camera=self for scanning, mic/geo off), `Cross-Origin-Opener-Policy`
- [x] **Data-protection keys** persisted to the data volume (auth survives restarts; enables a
      read-only container root filesystem)
- [x] Login **rate limiting** + account **lockout**; **antiforgery** on forms; `SameSite=Lax`
      secure cookies mitigate cross-site API forgery
- [x] Centralised validation; parameterised EF queries (no raw SQL); output encoding by default

## Automated tests
- [x] **Integration tests** (WebApplicationFactory) covering health endpoints, presence of
      security headers, anonymous→login redirect, and API 401 — through the real pipeline
- [x] 48 tests total (unit + integration); CI runs build (warnings-as-errors) + tests

## Deployment hardening
- [x] Non-root container (base-image `$APP_UID`), OCI image labels, container HEALTHCHECK
- [x] Compose hardening: `read_only` root FS + `tmpfs:/tmp`, `no-new-privileges`, `cap_drop: ALL`
- [x] Database never published to the network; only the HTTP port is exposed
- [x] Persistent volumes for DB, attachments, and keys; backups directory

## Security review summary
A focused review of the codebase was performed. Findings / posture:

| Area | Status |
|------|--------|
| AuthN/AuthZ | All routable app pages `[Authorize]`; writes behind `CanEdit`; admin actions behind `AdminOnly`; every API controller authorized. Error/NotFound intentionally anonymous. |
| CSRF | Antiforgery tokens on forms; `SameSite=Lax` auth cookie blocks cross-site API POSTs; no CORS enabled. |
| Injection | EF Core parameterised throughout; **no raw SQL**; output HTML-encoded by Blazor. |
| File uploads | Server-generated names, extension allow-list, size cap, path-traversal guard; downloads via authorized endpoint. |
| Secrets | None hardcoded; admin password + DB creds from env/config; logs never emit secrets. |
| Transport | Secure cookies over HTTPS; HSTS; CSP; configurable HTTPS redirect. |
| Data at rest | Data-protection keys stored unencrypted on the data volume (documented) — appropriate for self-host; protect the volume with filesystem permissions. |
| DoS | Login rate limiting + lockout; upload size limits; health checks. |

Residual notes (documented, not defects): CSP allows `'unsafe-inline'` for styles/scripts to stay
compatible with the Blazor import map — tighten with nonces if you serve HomeStock publicly;
data-protection keys are unencrypted at rest by default.

## Verification performed
- [x] `dotnet build HomeStock.slnx -c Release` — succeeds, 0 warnings
- [x] `dotnet test` — **48/48 pass** (5 new integration tests)
- [x] Running app: security headers present on responses; `<base href>` renders from PathBase;
      `blazor.web.js` loads and authenticated pages render under CSP; data-protection keys written
      to `data/keys`; health endpoints healthy
