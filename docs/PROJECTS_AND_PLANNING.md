# Projects, Stock & AI Planning Guide

This guide covers the Phase 5 workflow: measuring amounts, taking stock out and putting it back,
gathering items into a **project**, and planning a project from a plain-language description.

## The idea

You organise your garage and record where things live. Weeks later you start a project. Instead of
hunting for what you need, you describe the job — HomeStock works out the tools and materials,
checks them against your inventory, tells you **what you have and where**, reserves them, and when
you're done it deducts what you used and puts the rest back.

## 1. Measured amounts (units)

Every item has a **Quantity** and an optional **Unit**. Leave the unit blank for a plain count
(e.g. "3"); set it for measured goods (e.g. "100 **ft**" of wire). Quantities can be fractional
(25.5 ft). The item form has a unit box with common suggestions; lists and reports show `25 ft` or
`×3`.

## 2. Taking stock out and putting it back

Open an item — the **Stock** panel shows:

- **On hand** — how much you physically own.
- **Available** — what's free to take (on-hand minus what's currently checked out).
- **Checked out** — what's out right now.

Buttons (for editors):

- **Take out** — reserve/borrow an amount (available drops; on-hand unchanged).
- **Put back** — return a checked-out amount (a tool comes back).
- **Use** — consume an amount permanently (on-hand drops — materials used up).
- **Restock** — add stock (bought/found more).

Netting works the way you'd expect: 100 ft, **take out** 25 → available 75; **use** those 25 →
on-hand 75, nothing still "out". A tool you **take out** then **put back** leaves on-hand unchanged.
Every movement is recorded in the item's stock history and change log.

## 3. Projects

**Projects** gather everything for a job in one place.

1. **Projects → New project** — give it a name and description.
2. **Add items** — search, pick, enter a quantity, and tick **Consumable** for materials (wire,
   screws) or leave it off for tools. Adding an item **reserves** it — the item's *available* drops
   (it fails if there isn't enough). Each reserved item shows its **location** so it's a grab-list.
3. **Complete** — enter how much of each item you actually **used**; consumables default to fully
   used, tools to 0. HomeStock **consumes** the used amounts (deducts on-hand) and **returns** the
   rest. **Cancel** instead returns everything and consumes nothing.

Example: reserve **25 ft** off a 100 ft spool and a drill → close out using all the wire, drill
returned → wire on-hand **75 ft**, drill unchanged.

## 4. Plan a project with AI (optional)

If an AI provider is configured (see below), **Projects → Plan with AI**:

1. Describe the job in plain language (e.g. *"run a new 20A circuit ~25 ft to a garage outlet"*).
2. HomeStock proposes the tools and materials, then **matches them to your inventory locally** —
   only your description is sent to the model, so the locations and stock it reports are always
   real. You get: items you **have** (with amounts and **locations**) and a **shopping list** of
   what's **missing**.
3. Adjust the reserve amounts, then **Create project & reserve** — the matched items become the
   project's allocations (reserved), and you carry on with the normal project flow above.

### Configuring the planner

The planner uses any OpenAI-compatible **text** model. The easiest setup reuses your photo-import
(Vision) provider — just add a text model:

```
# If you already set Vision__BaseUrl / Vision__ApiKey for photo import:
Planner__Model=meta-llama/llama-3.1-8b-instruct     # OpenRouter example
# or set its own provider explicitly:
Planner__BaseUrl=https://openrouter.ai/api/v1
Planner__ApiKey=<your key>
Planner__Model=<a text model>
```

Free options (OpenRouter, Groq, Google Gemini) and a fully-local Ollama model all work — see
[PHOTO_IMPORT.md](PHOTO_IMPORT.md) for provider base URLs and keys (the same providers serve both
features). When unconfigured, the planner is simply hidden and the Plan page explains how to enable
it.

## Tips

- Set units on measured goods so the planner can reason about "need 25 of 100 ft".
- Mark project items **consumable** when adding them, so close-out defaults are right.
- The planner suggests a bill of materials — treat it as a starting point you review, not gospel.
- Reserving from a project and the item Stock panel share the same ledger, so *available* is always
  consistent across both.
