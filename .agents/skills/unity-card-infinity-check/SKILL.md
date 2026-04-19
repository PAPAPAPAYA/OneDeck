---
name: unity-card-infinity-check
description: Check OneDeck card designs for infinite loop combos (single-card, two-card, three-card). Use when analyzing card design documents, prefab configurations, or effect changes to detect same-turn infinite loops, resource inflation, and state explosion risks. Trigger on requests like "check cards for infinite loops", "analyze infinite combos", "review card design for balance issues", or when modifying card effects involving Stage, Bury, DeathRattle, Linger, or self-replication.
---

# OneDeck Infinite Loop Checker

## Purpose

Analyze OneDeck card designs to detect infinite loops and balance-breaking combos.

## Workflow

### Step 1: Load Card Data

Read the current card design document or prefab data.

- **Document path**: `docs/CardDesign_v*.md` or user-specified path.
- **Prefab path**: `Assets/Prefabs/Cards/` (use unity-read-prefab-serialized skill if inspecting prefabs directly).

### Step 2: Extract StageMyCards Effects

Scan all cards for effects that stage friendly cards to the top of the deck.

**Critical risk**: `onMeRevealed` triggers `StageMyCards(N)` unconditionally.

**Known high-risk cards** (verify current design):
- `ARMED_SUMMONER`
- `ALMIGHTY`
- `UNSTABLE_PORTAL`
- `OVERCHARGED_SUMMONER`
- `CURSE_SUMMONER`
- `ADVANCE_PORTAL`
- `BOOSTER` (verify trigger timing)
- `DR_MANHATTON` (conditional: Power >= 4)
- `RIFT_SUMMONER` (conditional: Minion Cost)

**Exception**: `RIFT` has `ExileSelf` and is safe.

**Rule**: Any two unconditional `StageMyCards` cards from the same player form a **Type A same-turn infinite** — they can stage each other indefinitely, trapping the Start Card in the middle of the deck forever.

### Step 3: Extract Self-Replication Effects

Check for cards that add copies of themselves to the deck.

**Known risk cards**:
- `SLIME` — `CheckCost_Counter(2) + AddSelfToMe` on bury.

**Check**: Does the cost consumption actually decrement the Counter? If not, every bury after the 2nd generates infinite copies.

### Step 4: Extract Linger Event Feedback Loops

Check Linger cards that respond to events by re-triggering the same event.

**Known risk cards**:
- `WEAPON_SPIRIT` — listens to "friendly card gains Power" and gives that card more Power. If the "give" action fires the same event, this creates an infinite chain (mitigated by EffectChainManager depth 9, but still a design flaw that inflates Power 8x beyond intent).

### Step 5: Extract Exponential State Growth

Check cards that multiply their own state each reveal.

**Known risk cards**:
- `UNFINISHED_ROBOT` — doubles own Power on reveal.
- `POWER_CRAVER` — gains 3x current Power on reveal.

**Cross-check**: Does the deck contain cards that stage this card back to top (e.g., `THE_FOOL` stages highest-Power enemy)? If yes, this enables exponential growth across turns.

### Step 6: Generate Report

Use this format:

```markdown
## Infinite Loop Check Report

### Type A: Same-Turn Infinite (Critical)
| Combo | Cards Needed | Loop Mechanism |
|-------|-------------|----------------|

### Type B: Resource Infinite (High)
| Card/Combo | Mechanism | Fix Needed |
|------------|-----------|------------|

### Type C: State Explosion (Medium)
| Card/Combo | Mechanism | Risk |
|------------|-----------|------|

### Design Concerns
- List suspicious but non-infinite interactions

### Recommendations
- Suggested fixes for each finding
```

## Reference

For detailed rules and deck simulation logic, see [references/infinity-rules.md](references/infinity-rules.md).
