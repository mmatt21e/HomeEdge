# HomeStock — Implementation Plan

This document is the roadmap for building HomeStock in phases. Phase 1 is complete; the
remaining phases build on the same architecture and data model without rewrites.

## Goals

- A self-hosted home inventory app, accessible across the home LAN from any device.
- Simple to run (SQLite + Docker), able to grow (PostgreSQL, remote access) without a rewrite.
- Clean layered architecture, tested business logic, no placeholder/non-functional features.

## Architecture at a glance

```
HomeStock.Domain          Entities, enums, invariants — no external dependencies
      ▲
HomeStock.Application      DTOs, service interfaces + logic, validation, IApplicationDbContext
      ▲
HomeStock.Infrastructure  EF Core DbContext, configs, migrations, seeding, file storage
      ▲
HomeStock.Web             Blazor UI + REST API, Identity, health checks, DI composition
```

Business logic lives in the Application layer and is unit-tested independently of the UI.
See [ARCHITECTURE.md](ARCHITECTURE.md) for detail.

## Data model

Entities (all with `Id`, most with `CreatedAt`/`UpdatedAt`; soft-delete where noted):

| Entity | Notes |
|--------|-------|
| `ApplicationUser` | Identity user + display name / active flag |
| `Category` | Soft-delete, system flag, colour/icon |
| `Location` | Self-referencing tree, unique QR `Code`, soft-delete |
| `InventoryItem` | Central record, 25+ fields, soft-delete, status enum |
| `Tag` + `ItemTag` | Many-to-many via a real join table (no CSV columns) |
| `ItemAttachment` | File metadata; binary stored on disk (Phase 2) |
| `ItemLoan` | Lending history (Phase 3) |
| `InventoryAudit` + `InventoryAuditItem` | Location audits (Phase 3) |
| `ItemHistory` | Append-only change log (populated from Phase 1) |
| `ApplicationSetting` | Key/value app settings |

Foreign keys use deliberate delete behaviours: `SetNull` for an item's category/location so items
survive their removal, `Cascade` for owned children (tags, attachments, loans, audit items,
history), and `Restrict` for a location's parent so a populated branch can't vanish.

## Phases

### Phase 1 — Foundations ✅ (complete)
Solution structure, authentication & roles, database + auto-migration + seeding, item CRUD,
categories, hierarchical locations with QR labels, dashboard, search/filter, responsive
mobile-first UI with theming, REST API, health checks, Docker deployment, unit tests.
See [PHASE1_CHECKLIST.md](PHASE1_CHECKLIST.md).

### Phase 2 — Capture & find ✅ (complete)
- Photo/document uploads (safe generated filenames, size/type validation, on-disk storage).
- Barcode/QR scanning via the phone camera; open/create/attach on scan; pluggable product lookup.
- CSV import (preview → map → validate → dedupe → confirm) and CSV/JSON export.
- Downloadable installers/upgraders and an automated release pipeline (self-contained builds for
  Windows/Linux/macOS + Docker image). See [PHASE2_CHECKLIST.md](PHASE2_CHECKLIST.md).

### Phase 3 — Operations ✅ (complete)
- Lending workflow (borrower, dates, return) with history.
- Location audits (expected list → confirm/missing/moved/damaged → discrepancy report → history).
- Printable inventory & insurance reports, filtered by location/category.
- Backup & restore with validation; expanded change-history / activity views.
  See [PHASE3_CHECKLIST.md](PHASE3_CHECKLIST.md).

### Phase 4 — Hardening & reach
- PWA polish (offline shell already scaffolded), install prompts, camera permissions.
- Reverse-proxy/base-URL support (headers already wired), MFA, security review.
- Deployment hardening, broader automated test coverage, PostgreSQL migration guide.

## After each phase

1. Build the solution. 2. Run tests. 3. Fix errors/warnings. 4. Verify migrations.
5. Check desktop + mobile layouts. 6. Update the README. 7. Commit the phase separately.
