# Phase 5 Checklist — Projects & AI planning

Phase 5 turns HomeStock from a catalogue into a working assistant: it tracks measured amounts,
lets you take stock out and put it back, gathers the items for a job into a **project**, and can
**plan a project from a plain-language description**. Delivered in four steps, each its own commit.

## Step 1 — Units of measure + decimal quantities
- [x] Item `Quantity` is now **decimal**, with an optional **Unit** (ft, m, box… blank = a count)
- [x] Migration converts existing integer quantities losslessly and adds the `Unit` column
- [x] Carried through DTOs, dashboard totals, reports, CSV import/export (new Unit column, decimal
      parsing) and JSON backup/restore; item form shows a Unit field, lists show "25 ft" / "×3"

## Step 2 — Inventory ledger (take out / put back / use / restock)
- [x] `InventoryTransaction` ledger + `TransactionType` (CheckOut/Return/Consume/Restock/Adjust); migration
- [x] `InventoryLedgerService` computes **on-hand / checked-out / available**, where
      currently-out = max(0, checkouts − returns − consumes) so consuming what you took nets it out
- [x] Guards: can't take out more than available, can't use more than on-hand; every move is
      appended to the ledger and the item history
- [x] Item detail **Stock panel**: on hand / available / checked out, with Take out / Put back /
      Use / Restock and a stock history

## Step 3 — Projects with allocations
- [x] `Project` + `ProjectAllocation` entities + `ProjectStatus`; migration
- [x] `ProjectService` builds on the ledger: adding an item **reserves** it (checks out; fails if
      not enough available); changing/removing a reservation checks out or returns the difference
- [x] **Close-out** consumes the "used" amount per item and returns the rest (100 ft spool:
      reserve 25, use 25 → on-hand 75; a drill reserved then returned unchanged); **cancel**
      returns everything, consumes nothing
- [x] Projects list + detail pages (search-and-add items with a consumable flag, see each item's
      **location**, complete/cancel); "Projects" nav entry

## Step 4 — AI project planner
- [x] `IProjectPlanner` abstraction + OpenAI-compatible text implementation; credentials fall back
      to the Vision provider so one key serves both AI features (set a text `Planner:Model`)
- [x] `ProjectPlanningService`: proposes a bill of materials, then **matches locally** by token
      overlap on name/keywords/category so reported locations and stock are grounded in real items;
      available quantities come from the ledger (respect existing reservations)
- [x] **Plan page**: describe a job → review matched items (reserve qty, location, consumable) +
      a shopping list of missing items → create project & reserve; "Plan with AI" entry shown only
      when configured, friendly not-configured state otherwise
- [x] Config: `Planner` section in appsettings / `.env` / docker-compose

## Verification performed
- [x] `dotnet build HomeStock.slnx -c Release` — succeeds, **0 warnings**
- [x] `dotnet test` — **75/75 pass** (30 new across steps: units flow, ledger netting/guards,
      project reserve/close-out/partial-use/cancel, planner matching + parsing + fallback)
- [x] Upgrade path tested (legacy integer quantity preserved through the migration)
- [x] Live: decimal + unit round-trip; Stock panel, Projects list/detail, and Plan page render;
      "Plan with AI" correctly gated on configuration
