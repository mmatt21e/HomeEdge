# Backup & Restore Guide

HomeStock keeps **all** of its state in two places under the data volume (`/data` in Docker):

- `homestock.db` (plus `-wal`/`-shm` side files) — the SQLite database.
- `attachments/` — uploaded files (photos, receipts, manuals) stored outside the database.

Backing up = capturing both together. A backup is only trustworthy if the database and the
attachments are captured consistently.

> Full in-app backup/restore with validation is scheduled for Phase 3. Until then, use the
> file-level procedures below — they are complete and reliable.

## Docker: back up

Stop the app briefly (or at least ensure no write is mid-flight) for a clean SQLite snapshot,
then archive the volume:

```bash
mkdir -p deploy/backups
docker compose stop homestock

# Copy the whole data volume into a timestamped tarball on the host.
docker run --rm \
  -v homestock-data:/data:ro \
  -v "$(pwd)/deploy/backups:/backups" \
  busybox tar czf /backups/homestock-$(date +%Y%m%d-%H%M%S).tar.gz -C /data .

docker compose start homestock
```

The tarball contains both `homestock.db*` and `attachments/`. Store copies off the machine
(another disk, NAS share, cloud).

### Verify a backup

```bash
tar tzf deploy/backups/homestock-YYYYmmdd-HHMMSS.tar.gz | grep -E 'homestock.db|attachments/' | head
```

You should see the database file and the attachments directory. For a deeper check, restore into
a throwaway volume (below) and start the app pointed at it.

## Docker: restore

```bash
docker compose down                      # stop the app
docker volume rm homestock-data          # remove the current (damaged/old) data volume
docker volume create homestock-data

docker run --rm \
  -v homestock-data:/data \
  -v "$(pwd)/deploy/backups:/backups" \
  busybox sh -c "tar xzf /backups/homestock-YYYYmmdd-HHMMSS.tar.gz -C /data"

docker compose up -d                     # migrations re-apply cleanly if the backup is older
```

## SDK / bare-metal

The same two artifacts live under `src/HomeStock.Web/data/` (dev) or your publish `data/`
directory. Back up:

```bash
sqlite3 data/homestock.db ".backup 'data/homestock-backup.db'"   # consistent DB snapshot
tar czf homestock-backup.tar.gz data/homestock-backup.db data/attachments
```

Restore by stopping the app and replacing `data/homestock.db` and `data/attachments` from the
archive, then starting again.

## Migrating SQLite → PostgreSQL

1. Stand up PostgreSQL (the compose `postgres` profile does this).
2. Generate PostgreSQL migrations (see [ARCHITECTURE.md](ARCHITECTURE.md#databases-and-migrations)).
3. Point `Database:Provider=Postgres` and `ConnectionStrings:DefaultConnection` at it; start the
   app to create the schema.
4. Move data with a tool such as `pgloader`, or re-import via the CSV import feature (Phase 2).
   Copy the `attachments/` directory across unchanged.

## Good practice

- Automate the Docker backup with `cron` (e.g. nightly) and rotate old tarballs.
- Test a restore periodically — an untested backup is not a backup.
- Keep at least one copy off the host machine.
