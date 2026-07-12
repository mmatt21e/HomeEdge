# Phase 3 Checklist

Everything below is implemented, builds with **zero warnings**, and is covered by a running app
and/or unit tests. No schema change was needed — the loan, audit, and history tables were created
in the Phase 1 migration.

## Lending & item status
- [x] Loan an item out: borrower name, contact, loan date, expected return date, notes
- [x] Record return: actual return date + notes; status restored to Available
- [x] One open loan per item enforced; date sanity checks (return/expected ≥ loan date)
- [x] Full lending history per item; loan/return recorded in item change-history
- [x] Item status set to **Loaned** while out; "Loaned out" dashboard tile + `/items?loaned=true`
- [x] Open-loans query surfaces **overdue** loans first
- [x] All eight statuses supported (Available, In use, Loaned, Missing, Damaged, Disposed, Sold,
      Donated, Archived) via the item editor and audit outcomes

## Inventory audits
- [x] Start an audit for a location, optionally **including sub-locations**
- [x] Snapshots the items **expected** in that location at start
- [x] Per-item outcome: **Confirmed / Missing / Moved / Damaged** (+ moved-to location, notes)
- [x] **Scan or type a barcode** to confirm an item quickly during the audit
- [x] Complete the audit → applies outcomes to items (Missing/Damaged set status; Moved updates
      location; all with change-history) and produces a **discrepancy report**
- [x] Cancel an in-progress audit; full **audit history** with discrepancy counts

## Reports
- [x] **Printable inventory report** and **insurance report** (print / Save-as-PDF)
- [x] Filter by **category** and/or **location** (with sub-locations) and include-archived
- [x] Summary totals: record count, quantity, purchase total, estimated value
- [x] Print-optimised CSS (hides chrome); CSV export remains available for spreadsheets

## Backup & restore
- [x] **JSON backup** of the full inventory (from Phase 2 export)
- [x] **Restore** with a validation/preview step (counts + warnings) before applying
- [x] Restore is **additive/non-destructive**: recreates categories, the location hierarchy,
      tags, and items; **skips items that already exist** (name+serial+barcode) — safe to re-run
- [x] Admin-only; restored items recorded in change-history

## Change history
- [x] Per-item history (Phase 1) now also captures loans, returns, audit outcomes, attachments
- [x] **Recent activity** feed (`/activity`) for reviewing the latest changes across all items
- [x] Soft deletion retained for items/categories/locations; history preserved

## Verification performed
- [x] `dotnet build HomeStock.slnx -c Release` — succeeds, 0 warnings
- [x] `dotnet test` — **43/43 pass** (11 new: loans, audits, backup/restore)
- [x] Running app: `/audits`, `/activity`, `/reports`, `/reports/print` (inventory & insurance),
      and item detail (Lending panel) all render; login/authorization enforced
