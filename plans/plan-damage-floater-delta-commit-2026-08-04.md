# Plan: Damage Floater Delta Commit (per-hit HP loss attribution)

**Date:** 2026-08-04
**Status:** Implemented (EditMode suite green; Play Mode repro pending)
**Files:** `Assets/Scripts/Managers/CombatInfoDisplayer.cs`, `Assets/Scripts/Effects/HPAlterEffect.cs`, `Assets/Scripts/UXPrototype/DamageFloaterPresenter.cs`, `Assets/Scripts/Editor/Tests/CombatInfoDisplayerDeltaCommitTests.cs`

## Symptom

Deck: SMALL_SCALE_DEATH (尸爆, 2x2 damage, buries ETERNAL_GHOST mid-effect) + ETERNAL_GHOST (1 damage onMeBuried). Enemy HP 25.

| Moment | Floater shown | Expected |
|---|---|---|
| 尸爆 reveal hit | -2 | -2 ✓ |
| 尸爆 attack animation | **-1** | -2 |
| Eternal Ghost attack animation | **-2** | -1 |

Sum and final HP were correct; only the per-hit numbers were swapped. Console
log matched: `25→23(2), 23→22(1), 22→20(2)`.

## Root Cause

The HP display used a per-side **FIFO queue of absolute post-hit HP values**:

1. Logic phase: `HPAlterEffect` resolves damage immediately and enqueues the
   post-hit HP (`SnapshotHpDisplay(target, preHitHp, postHitHp)`).
2. Animation phase: each attack's `onHit` pops the oldest value
   (`CommitHpDisplay(target)`).
3. `DamageFloaterPresenter` polled the displayed HP per frame and spawned a
   floater with the frame diff.

Enqueue order = **logic resolution order**; pop order = **animation playback
order**. A reactive chain (bury → `onMeBuried` damage) resolves the ghost's
damage BETWEEN the parent's two logic hits, but the recorder tree plays the
ghost's attack AFTER the parent's. Each commit therefore popped a snapshot
belonging to a different hit, and every floater number was misattributed.

## Fix: delta commit

Each hit's actual HP loss is already known at logic time and is immune to
playback ordering, so the commit now carries it instead of an absolute value:

- `CombatInfoDisplayer`
  - `Queue<int>` replaced by pending counts (`_pendingOwnerHpCount` /
    `_pendingEnemyHpCount`) + frozen `_displayed*Hp`.
  - `SnapshotHpDisplay(target, preHitHp)` — first snapshot freezes the display
    on preHitHp; increments the pending count.
  - `CommitHpDisplay(target, hpLoss)` — decrements pending, subtracts THIS
    hit's own loss from the display, then raises
    `onHpDisplayCommitted(isOwner, hpLoss, newDisplayed)`.
  - `HasPendingHpDisplay(isOwner)`; `ClearHpDisplayLocks()` also raises
    `onHpDisplayLocksCleared` so consumers resync silently.
- `HPAlterEffect` (both capture sites): the `onHit` closure captures
  `actualHpLoss` and calls `CommitHpDisplay(capturedTarget, capturedHpLoss)`.
- `DamageFloaterPresenter`
  - Subscribes to `onHpDisplayCommitted` on combat entry: one floater per hit,
    number = that hit's own loss; `hpLoss <= 0` (full shield absorb) spawns
    nothing.
  - Frame polling is now a fallback only, skipped per side while
    `HasPendingHpDisplay` is true (covers status-effect damage and the legacy
    no-recorder path, which have no hit moment).
  - Resyncs its cache silently on `onHpDisplayLocksCleared` (fixes the stale-
    cache phantom floater after cancelled animations).

Replay of the bug scenario: commits (-2) → (-2) → (-1) produce displayed
23 → 21 → 20, floaters 2/2/1 — monotonic, order-independent, final value
matches live HP.

Side benefit: heals and status-effect damage resolved between two hits no
longer fold into the next hit's floater number.

## Diagnostics added

`[DamageFloater]`-prefixed `TestManager.Log` lines (LogCategory.DamageFloater),
all with `Time.frameCount`: `Snapshot` (preHitHp, pending depth), `Commit`
(hpLoss, displayed, pendingLeft), `Capture attack` (card, side, actualHpLoss,
totalDmg), `ClearHpDisplayLocks`, plus `frame=` on the existing
`[DynamicDamageDisplay]` lines.

## Verification

- `CombatInfoDisplayerDeltaCommitTests` (EditMode): out-of-order commit
  attribution (the exact bug replay), zero-loss commit, per-side independence,
  clear-locks unfreeze/notify. Full EditMode suite: 241/241 passed.
- Play Mode repro (manual, verified 2026-08-04): floaters read 2 (reveal),
  2 (尸爆 anim), 1 (ghost anim). Regression row 67 marked ✅.
- Regression row 67 in `docs/RegressionChecklist.md`;
  `VISUAL-FIX(2026-08-04)` blocks in `CombatInfoDisplayer.cs` and
  `DamageFloaterPresenter.cs`.

## Follow-up (same day): HP display timing — nested reactive hit wins the freeze

Play Mode verification of the delta commit exposed a second, distinct bug: the
enemy HP text dropped 25 → 23 AT REVEAL (before any attack animation), stepped
23 → 21 → 19 on the two 尸爆 animations, then "corrected" to 20 on the ghost's
animation. Floaters were correct; only the HP display timing was wrong.

The new diagnostics pinned it in one repro — snapshot order at frame 712:

```
Snapshot side=enemy preHitHp=23 pending=1   <- ETERNAL_GHOST (nested Linger hit)
Snapshot side=enemy preHitHp=25 pending=2   <- 尸爆 hit 1 (outer, resumed)
Snapshot side=enemy preHitHp=22 pending=3   <- 尸爆 hit 2
```

`HPAlterEffect` snapshotted AFTER `CheckDmgTargets` raised the damage events.
ETERNAL_GHOST's Linger (`onTheirPlayerTookDmg`) resolves INSIDE that raise —
between the outer hit's `preHitHp` read and its snapshot — so the nested hit's
mid-burst `preHitHp=23` won the batch freeze instead of the outer hit's 25.
Every commit then displayed a wrong absolute value until the pending count
drained and `GetDisplayedEnemyHp()` fell back to live HP (the "+1 correction").

**Fix:** in `DecreaseMyHp` / `DecreaseTheirHp`, the snapshot + attack capture
now run BEFORE `CheckDmgTargets` (display-only state; game logic unchanged —
`ProcessDamage` still applies first, so reactive chains see post-damage HP).
The outermost hit always snapshots first and its preHitHp freezes the batch.

- EditMode: `CombatInfoDisplayerDeltaCommitTests.NestedReactiveHit_OuterHitWinsTheFreeze`
  (one-shot Linger listener dealing nested damage inside the outer hit).
- Full suite: 242 tests, 239 passed + 3 pre-existing failures
  (`AfterShuffleTimingTests` x2, `RecorderAnimationPlayerTests` x1). The 3
  failures reproduce identically with this change temporarily reverted
  (bisect), and the failing tests reference neither `HPAlterEffect` nor the HP
  display — unrelated to this work (likely the 2026-08-03 peel-deck WIP /
  editor-state coroutine timing).
- Regression row 68 in `docs/RegressionChecklist.md` (✅ Play Mode verified
  2026-08-04); `VISUAL-FIX(2026-08-04)` blocks in `HPAlterEffect.cs`.
