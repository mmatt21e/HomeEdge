# Remote Access Preparation Guide

HomeStock runs LAN-only by default and **does not expose any port to the internet**. When you
want to reach it from outside the home, choose one of the options below. The application already
supports the pieces these need — forwarded headers, configurable HTTPS/secure cookies, trusted
proxies, and rate limiting — so enabling remote access is configuration, not a rewrite.

> **Recommendation:** prefer a private network (Tailscale or WireGuard). They give you remote
> access without publishing anything to the public internet, which is the safest default for a
> home server.

## What the app already supports

- **Reverse-proxy headers** — `X-Forwarded-For` / `X-Forwarded-Proto` are honoured, but only
  from proxies you list in `ReverseProxy:KnownProxies` (spoofed headers are ignored otherwise).
- **HTTPS & secure cookies** — cookies are `Secure` automatically over HTTPS; set
  `EnableHttpsRedirection=true` when a TLS endpoint terminates in front of the app.
- **Rate limiting & lockout** — login attempts are rate-limited per IP and Identity locks
  accounts after repeated failures.
- **Multi-factor authentication** — Identity 2FA scaffolding is present (Settings → Two-factor);
  full enforcement is a Phase 4 item.

Keep remote-access configuration **separate** from the core app — it belongs in your proxy/VPN
layer and environment variables, not in the application code.

## Option 1 — Tailscale (easiest, private)

1. Install Tailscale on the host and on your remote devices; join the same tailnet.
2. Reach the app at `http://<host-tailscale-name>:8080` — no ports opened to the internet.
3. Optional: enable **Tailscale Serve/HTTPS** to get a TLS URL. Then set
   `EnableHttpsRedirection=true` and add the Tailscale proxy IP to `ReverseProxy:KnownProxies`.

## Option 2 — WireGuard VPN

1. Run a WireGuard server on your router or the host.
2. Connect remote devices to the VPN; access HomeStock by its LAN IP as if you were home.
3. Nothing about the app changes — it stays LAN-only; the VPN carries you onto the LAN.

## Option 3 — Cloudflare Tunnel

1. Install `cloudflared` and create a tunnel to `http://localhost:8080`.
2. Map a hostname (e.g. `inventory.example.com`) to the tunnel; Cloudflare provides HTTPS.
3. In HomeStock set `EnableHttpsRedirection=true` and add Cloudflare's connector/proxy IP to
   `ReverseProxy:KnownProxies` so forwarded headers are trusted.
4. Protect it with **Cloudflare Access** (SSO/one-time-PIN) for a second gate.

## Option 4 — Reverse proxy with HTTPS (Caddy / Nginx / Traefik)

Terminate TLS at a proxy and forward to the container. Example (Caddy):

```
inventory.example.com {
    reverse_proxy 127.0.0.1:8080
}
```

Then set:

```
EnableHttpsRedirection=true
ReverseProxy__KnownProxies__0=127.0.0.1     # the proxy's address as seen by the app
```

If you expose a reverse proxy to the internet directly, also: use strong admin passwords, enable
2FA, keep the OS/image patched, and consider IP allow-listing or an auth gate in front.

## Hardening checklist for any remote setup

- [ ] Strong, unique admin password; create per-person accounts with least-privilege roles.
- [ ] HTTPS everywhere it leaves the LAN (`EnableHttpsRedirection=true` behind TLS).
- [ ] Only trusted proxy IPs in `ReverseProxy:KnownProxies`.
- [ ] Keep the database unpublished (it already is by default).
- [ ] Regular backups (see [BACKUP_RESTORE.md](BACKUP_RESTORE.md)) and OS/container updates.
