# User Guide

A quick tour of HomeStock for everyday use. The interface is mobile-first: on a phone you get a
bottom navigation bar; on a tablet/desktop a sidebar. A search box sits at the top of every page.

## Signing in

Open the app and sign in with the account your administrator created (the first-run admin is
`admin@homestock.local`). Change your password under **Settings → Change password**. You can
switch **light / dark / system** themes from the top-bar toggle or Settings.

## Navigation

- **Dashboard** — your inventory at a glance.
- **Items** — the full list, with search and filters.
- **Scan** — camera scanning (arriving in a later phase).
- **Locations** — your place hierarchy and QR labels.
- **Categories** — item classifications.
- **Audits / Reports** — arriving in later phases.
- **Settings** — instance options, your account, appearance.

## Adding an item (fast)

1. Tap **Add item** (on the Dashboard or Items page).
2. Enter a **Name** — that's the only required field, so quick capture is fast.
3. Fill in whatever else you know: category, quantity, manufacturer/brand/model, serial number,
   barcode, purchase details, value, condition, warranty date, location and container, tags,
   notes. Anything can be left blank and added later.
4. If the serial number or barcode matches another item, you'll get a **duplicate warning** —
   informational, not blocking.
5. **Create item.** You'll land on the item's page.

## Finding items

- Type in the top **search** box (matches name, description, manufacturer, model, serial,
  barcode, category, location, tags, notes — case-insensitive).
- On the **Items** page, narrow further with the **filters**: category, location, status,
  warranty state, and an Archived toggle. Use **Clear** to reset.
- Dashboard tiles are shortcuts — e.g. tap **Warranties expiring soon**, **Loaned out**,
  **Missing photos**, or **No location** to jump to that filtered list.

## Editing, archiving, deleting

On an item's page (with edit permission):

- **Edit** — change any field; status changes and edits are recorded in the item's **History**.
- **Archive** — hides the item from normal lists but keeps it (find it via the Archived filter);
  **Restore** brings it back.
- **Delete** (admin only) — permanent removal, after a confirmation.

## Locations

Build a hierarchy that matches your home, e.g. **Home › Garage › Tool Cabinet › Drawer 2**.

- **New location** — name it and pick a parent (or leave it top-level).
- **Move** — edit a location and change its parent (moving a branch into its own child is
  prevented).
- **QR label** — tap the QR icon to view/download a location's code. Print it and stick it on the
  shelf/bin; scanning it opens that location's contents.
- Tap a location name to see the items stored there.

## Categories

Fourteen useful categories are provided. Add your own, give them a colour, and edit or archive
them. A category that's still used by items can't be deleted until you reassign those items
(archive it instead).

## Dashboard

Shows total item records, total quantity, estimated total value, warranties expiring soon,
items loaned out, items missing photos, items with no location, recently added/updated items,
and counts by category and location.

## Lending items out

Open an item and use the **Lending** panel:

- **Loan out** — enter the borrower's name (and optionally contact, dates, notes). The item's
  status becomes *Loaned* and it shows on the dashboard's "Loaned out" tile.
- **Record return** — when it comes back, set the return date and any notes; the status returns to
  *Available*. The full lending history stays on the item, and overdue loans are flagged in red.

## Running an inventory audit

Go to **Audits → Start an audit**, pick a location (optionally including sub-locations), and start.
HomeStock lists the items expected there. For each item, mark **Confirm / Missing / Damaged /
Moved** (choose a destination for moved items) — or scan/type a barcode to confirm it instantly.
When done, **Complete audit**: missing/damaged items get that status, moved items are relocated,
and a **discrepancy report** is produced. Past audits and their discrepancies are kept under
Audit history.

## Reports (print / PDF)

Under **Reports → Printable reports**, choose a category and/or location and generate an
**Inventory** or **Insurance** report. Use your browser's Print dialog to print or "Save as PDF".
The **Export** panel also offers CSV (for spreadsheets) and a full JSON backup.

## Backup & restore

- **Backup** — Reports → Export → *JSON backup* downloads your whole inventory.
- **Restore** (administrators) — Reports → *Restore from JSON backup* uploads a backup, shows a
  summary, and merges it in: it recreates missing categories, locations, tags and items, and
  skips items already present. It never deletes existing data.

Remember to also back up the **attachments** folder — see the Backup & Restore guide.

## Reviewing activity

**Settings → Recent activity** (`/activity`) shows the latest changes across your inventory —
creations, edits, status changes, loans, returns, audits, and attachments.

## Tips

- Use **tags** for cross-cutting labels ("fragile", "insured", "loanable").
- Record **serial numbers** and **purchase price/value** for insurance purposes.
- Set **warranty expiration** dates so the dashboard can warn you before they lapse.
- Install HomeStock to your home screen (browser "Add to Home Screen") for an app-like shortcut.
