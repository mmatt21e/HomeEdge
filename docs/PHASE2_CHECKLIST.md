# Phase 2 Checklist

Everything below is implemented, builds with **zero warnings**, and is covered by a running app
and/or unit tests. (This phase also folds in the requested **downloadable installer/upgrader**
release capability.)

## Photographs & documents
- [x] Multiple attachments per item: photos, serial-number photos, receipts, warranty docs,
      manuals, insurance docs (typed)
- [x] Binaries stored **outside the database** under safe, server-generated, sharded filenames
      (never the client filename); path-traversal rejected
- [x] File **size** and **type** validation (configurable allow-list + max size)
- [x] Authorized download/serve endpoint (files are outside wwwroot)
- [x] Item detail gallery with image thumbnails, download, and delete
- [x] Adds/removals recorded in item **history**; dashboard "missing photos" reflects uploads

## Barcode & QR scanning
- [x] Phone-camera scanning of UPC/EAN barcodes and QR codes (bundled `html5-qrcode`, offline)
- [x] On scan: **open** the matching item, **create** a new item pre-filled with the code, or
      **assign** the code to an existing item
- [x] HomeStock **location** QR labels resolve to that location's contents
- [x] Manual-entry fallback for devices without a usable camera
- [x] Scanning layer isolated so an external product-info lookup can be added later

## Search & filtering
- [x] Case-insensitive search across all key fields (Phase 1) + filter UI for category, location,
      status, warranty state, and archived
- [x] Dashboard shortcut tiles deep-link into filtered item lists

## Import & export
- [x] **CSV export** of items (respects archived toggle) — insurance-ready record
- [x] **JSON backup** of the full inventory (items, categories, locations, tags) — admin only
- [x] **CSV import wizard**: upload → column **mapping** (auto-detected) → **validation** with
      per-row errors/warnings → **duplicate detection** (barcode/serial, in-file and vs DB) →
      **confirmation** before saving; category/location resolved by name

## Releases, installers & upgraders (requested)
- [x] `scripts/publish-all.sh` builds **self-contained, single-file** executables for
      linux-x64/arm64, win-x64, osx-x64/arm64 (runtime bundled — no SDK needed) + SHA-256 sums
- [x] `scripts/install.sh` / `install.ps1` — one-command install (detect platform, download,
      verify checksum, optional systemd/Windows service, Docker mode)
- [x] `scripts/upgrade.sh` — in-place upgrade that backs up data first; migrations auto-apply
- [x] `.github/workflows/release.yml` — on `vX.Y.Z` tag: build artifacts, push Docker image to
      GHCR, create a GitHub Release with assets + checksums + notes
- [x] `.github/workflows/ci.yml` — build (warnings-as-errors) + test on push/PR
- [x] App reports its version (Settings → Instance); `Directory.Build.props` centralises it

## Verification performed
- [x] `dotnet build HomeStock.slnx -c Release` — succeeds, 0 warnings
- [x] `dotnet test` — 32/32 pass (8 new: attachments + import/export)
- [x] Running app: attachment upload/list/download verified (image renders, `hasPhoto` flips);
      Scan and Reports pages render; CSV export returns headers+data; JSON backup returns 200
- [x] Self-contained single-file `linux-x64` build **runs standalone** (bundled runtime): boots,
      seeds admin, migrates, health + login endpoints return 200
