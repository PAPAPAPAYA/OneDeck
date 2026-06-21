# PRD: Per-Projectile Status Effect Display Commit (PowerReactionEffect Delta Tracking)

## 1. Problem Statement

`PowerReactionEffect.GivePowerToCardThatGotPower()` is triggered **after** the target card has already gained Power from a previous effect (e.g. `SACRIFICIAL_SWORD` → `POWER_CRAVER` → `WEAPON_SPIRIT` reaction). The current display-defer mechanism relies on `CardScript.SnapshotDisplayState()` + `CommitDisplayState()`:

- `SnapshotDisplayState()` copies the **entire** current `myStatusEffects` list into `displayMyStatusEffects`.
- `CommitDisplayState()` copies the **entire** current `myStatusEffects` list back.

When multiple effects give status effects to the same card in a chain, the first `StatusEffectProjectile` that lands calls `CommitDisplayState()`, which refreshes the text to show the **full current state** — including the Power added by the later reaction. As a result, the reaction's Power text appears before the reaction's own projectile has landed.

Currently only `PowerReactionEffect` creates this nested "same-target" pattern, but the fix should be implemented generically so any future effect with the same pattern is covered.

## 2. Goal

Make the status-effect text update follow each `StatusEffectProjectile` individually, instead of committing the full card state whenever any projectile lands.

Specifically for the `PowerReactionEffect` case:

- Parent effect's projectile lands → text shows the Power from the parent effect only.
- Reaction effect's projectile lands → text updates to include the reaction's Power.

## 3. Current Architecture

### 3.1 Snapshot / Commit Flow

```csharp
// CardScript.cs
public void SnapshotDisplayState()
{
    if (_hasDisplaySnapshot) return;
    displayMyStatusEffects.Clear();
    displayMyStatusEffects.AddRange(myStatusEffects);
    _displayCardDesc = ComputeDynamicCardDesc();
    _hasDisplaySnapshot = true;
}

public void CommitDisplayState()
{
    displayMyStatusEffects.Clear();
    displayMyStatusEffects.AddRange(myStatusEffects);
    _displayCardDesc = null;
    _hasDisplaySnapshot = false;
}
```

### 3.2 StatusEffectChange Request

`ApplyStatusEffectCore()` captures:

```csharp
recorder.animationRequests.Add(new AnimationRequest
{
    type = AnimationRequestType.StatusEffectChange,
    targetCard = targetCardScript.gameObject,
    statusEffect = effect,
    statusEffectAmount = amount,
    statusEffectParticlePrefab = particlePrefab,
    statusEffectParticleYOffset = particleYOffset
});
```

`StatusEffectAmount` is used for particle count / tint intensity, but the request does **not** carry a display delta.

### 3.3 RecorderAnimationPlayer

`MarkDeferredDisplayCommits()` sets `deferDisplayCommit = true` on `StatusEffectChange` requests whose target will receive a `StatusEffectProjectile` in the **same recorder**. The actual `CommitDisplayState()` is called inside `StatusEffectProjectile` completion callbacks.

This works for single-effect status giving, but fails for nested same-target giving because the first projectile to land commits the full current state.

## 4. Proposed Design (Option A+)

Track **display deltas** per `StatusEffectChange` request and apply them incrementally as projectiles land, instead of copying the full card state.

### 4.1 New Fields on AnimationRequest

```csharp
// AnimationRequest.cs
/// <summary>
/// For StatusEffectChange requests: the signed delta applied to the target's display state.
/// Positive = gain layers, negative = lose layers.
/// </summary>
public int statusEffectDelta = 0;
```

`statusEffect` and `statusEffectAmount` remain unchanged.

### 4.2 ApplyStatusEffectCore Captures Delta

In `EffectScript.ApplyStatusEffectCore`, populate `statusEffectDelta`:

```csharp
recorder.animationRequests.Add(new AnimationRequest
{
    type = AnimationRequestType.StatusEffectChange,
    targetCard = targetCardScript.gameObject,
    statusEffect = effect,
    statusEffectAmount = amount,
    statusEffectDelta = amount,   // <-- new
    statusEffectParticlePrefab = particlePrefab,
    statusEffectParticleYOffset = particleYOffset
});
```

For consumption effects that manually build `StatusEffectChange`, populate `statusEffectDelta` with the negative amount (e.g. `ConsumeStatusEffect` sets `-amountRemoved`).

### 4.3 Baseline Computation

Before playing root recorders, `RecorderAnimationPlayer` pre-scans the entire recorder tree and computes a per-card baseline:

```csharp
// Pseudocode
baseline[card] = card.myStatusEffects - Sum(pending.statusEffectDelta for pending in all recorders targeting card)
```

Then for each affected card:

```csharp
card.displayMyStatusEffects = baseline[card];
card._hasDisplaySnapshot = true;  // or use a new flag
```

This ensures `GetStatusEffectsForDisplay()` returns the state **before any pending animations**.

### 4.4 Incremental Application

Introduce a helper on `CardScript`:

```csharp
public void ApplyDisplayDelta(EnumStorage.StatusEffect effect, int delta)
{
    if (displayMyStatusEffects == null)
        displayMyStatusEffects = new List<EnumStorage.StatusEffect>();

    for (int i = 0; i < delta; i++)
        displayMyStatusEffects.Add(effect);

    // Negative delta: remove layers
    for (int i = 0; i < -delta; i++)
        displayMyStatusEffects.Remove(effect);

    _displayCardDesc = null;
}
```

In `RecorderAnimationPlayer`:

- `StatusEffectChange` with `deferDisplayCommit = false`:
  - Apply its delta immediately: `targetCardScript.ApplyDisplayDelta(request.statusEffect, request.statusEffectDelta)`.
- `StatusEffectChange` with `deferDisplayCommit = true`:
  - Skip delta application; tint/particles still play.
- `StatusEffectProjectile` completion:
  - Find all **deferred** `StatusEffectChange` requests for the same target in the same recorder (or same "projectile group").
  - Apply their deltas: `targetCardScript.ApplyDisplayDelta(...)`.

### 4.5 Linking StatusEffectChange to StatusEffectProjectile

Within one `EffectRecorder`, a `StatusEffectProjectile` request corresponds to the deferred `StatusEffectChange` requests for the **same target** that appear before it in `animationRequests`. Therefore:

- During playback, when a `StatusEffectProjectile` completes, apply all still-deferred `StatusEffectChange` deltas for the same target in the same recorder.
- Mark those `StatusEffectChange` requests as "applied" so they are not applied again.

For batch effects like `GiveStatusEffect` (3 `StatusEffectChange` + 1 `StatusEffectProjectile`), all 3 deltas are applied when the single projectile request completes.

### 4.6 Cross-Recorder Same-Target Case

For the `PowerReactionEffect` scenario:

- Parent recorder has deferred `StatusEffectChange` + `StatusEffectProjectile` for `POWER_CRAVER`.
- Child recorder has deferred `StatusEffectChange` + `StatusEffectProjectile` for `POWER_CRAVER`.

The pre-scan baseline is `current state - (parent delta + child delta)`. Playback applies deltas independently:

1. Baseline = 0 Power displayed.
2. Parent projectile lands → apply parent delta → display 1 Power.
3. Child projectile lands → apply child delta → display 2 Powers.

## 5. Why This Is Safe

- **No logical state change**: `myStatusEffects` is still mutated synchronously in the logic phase. Only the **display copy** (`displayMyStatusEffects`) is updated incrementally.
- **Backward compatible for single-effect cases**: One `StatusEffectChange` + one `StatusEffectProjectile` behaves the same as before: baseline is 0, projectile applies the delta, final display matches current state.
- **Consumption effects**: Negative deltas work the same way. `ConsumeOwnStatusEffect` already sets `deferDisplayCommit = true`; it just needs to populate `statusEffectDelta`.
- **Non-deferred StatusEffectChange**: Effects that intentionally commit immediately (no matching projectile) continue to apply their delta immediately.

## 6. Impact Scope

### 6.1 Files Modified

| File | Change |
|------|--------|
| `Assets/Scripts/Managers/AnimationRequest.cs` | Add `statusEffectDelta` field. |
| `Assets/Scripts/Effects/EffectScript.cs` | Set `statusEffectDelta = amount` in `ApplyStatusEffectCore`. |
| `Assets/Scripts/Effects/StatusEffect/ConsumeStatusEffect.cs` | Set `statusEffectDelta` on manually built `StatusEffectChange` requests. |
| `Assets/Scripts/Card/CardScript.cs` | Add `ApplyDisplayDelta` helper; adjust `SnapshotDisplayState` / `CommitDisplayState` or add new baseline flag. |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | Pre-scan pending deltas, compute baselines, replace `CommitDisplayState()` calls with `ApplyDisplayDelta()`, manage "applied" flags. |

### 6.2 Files Requiring Documentation Updates

| File | Update |
|------|--------|
| `AGENTS.md` | Update the "Status Effect" animation section to describe per-projectile delta commit. |
| `docs/RegressionChecklist.md` | Add a row for the PowerReactionEffect nested Power scenario. |

### 6.3 Test Impact

| Test File | Impact |
|-----------|--------|
| Existing Edit Mode tests for status effects | May need updates if they assert on `displayMyStatusEffects` directly. |
| `PowerReactionEffect` tests | Add new test: parent gives Power, reaction gives Power to same target, verify display increments per projectile. |

## 7. Risks and Regression Verification

| Risk | Explanation | Verification |
|------|-------------|--------------|
| Baseline computed incorrectly | If pending deltas are double-counted or missed, display will show wrong layer count. | Reveal any single status-effect-giving card (e.g. self-Power). Verify final layer count matches logic state. |
| Deltas applied twice | If a deferred `StatusEffectChange` is not marked "applied", the same delta may be applied by both `StatusEffectChange` and `StatusEffectProjectile`. | Run `GiveStatusEffect` with amount=3. Verify display shows exactly 3 layers, not 6. |
| Consumption display wrong | Negative deltas could remove the wrong layers if applied out of order. | Run `ConsumeOwnStatusEffect` with 2+ Power. Verify display decrements correctly. |
| Non-projectile StatusEffectChange | Effects without projectiles must still apply delta immediately. | Run a card whose effect uses `ApplyStatusEffectCore` without `CaptureBatchStatusEffectAnimation`. Verify text updates immediately. |
| `PowerReactionEffect` specific | Parent and reaction both target same card. | Deck: `SACRIFICIAL_SWORD` + `POWER_CRAVER` + `WEAPON_SPIRIT`. Reveal `SACRIFICIAL_SWORD`. Verify `POWER_CRAVER` text shows 1 Power after first projectile, then 2 Powers after `WEAPON_SPIRIT` projectile. |

## 8. Recommended Implementation Order

1. Add `statusEffectDelta` to `AnimationRequest`.
2. Populate `statusEffectDelta` in `ApplyStatusEffectCore` and `ConsumeStatusEffect`.
3. Add `CardScript.ApplyDisplayDelta` helper and baseline-setting API.
4. Update `RecorderAnimationPlayer`:
   - Pre-scan recorder tree for pending deltas.
   - Compute baselines for affected cards before playing root recorders.
   - Apply immediate deltas for non-deferred `StatusEffectChange`.
   - Apply deferred deltas inside `StatusEffectProjectile` completion.
5. Add detailed temporary logs to verify baseline/delta/application sequence.
6. Run Edit Mode tests and fix any assertions.
7. Run Play Mode regression on status-effect-giving cards and the specific `PowerReactionEffect` combo.
8. Update `AGENTS.md` and `docs/RegressionChecklist.md`.

## 9. VISUAL-FIX Comment Template

Add a comment block in `RecorderAnimationPlayer.cs` near the new delta-commit logic:

```csharp
// VISUAL-FIX(2026-06-20): Per-projectile status effect display commit
//   Cause:    CommitDisplayState() copied the full current myStatusEffects list, so when
//             multiple effects gave status effects to the same card (e.g. PowerReactionEffect),
//             the first projectile landing refreshed the text to include all pending layers.
//   Fix:      Track statusEffectDelta per StatusEffectChange request, compute a pre-animation
//             baseline, and apply deltas incrementally as each StatusEffectProjectile completes.
//   Affects:  AnimationRequest, EffectScript, CardScript, RecorderAnimationPlayer, ConsumeStatusEffect
//   Regress:  Reveal SACRIFICIAL_SWORD with POWER_CRAVER and WEAPON_SPIRIT in deck. Check that
//             POWER_CRAVER's Power text shows 1 after the first projectile, then 2 after the
//             WEAPON_SPIRIT reaction projectile lands.
//   Related:  PRD power-reaction-status-effect-delta-commit-2026-06-20
```

## 10. Acceptance Criteria

- [ ] `AnimationRequest` has a `statusEffectDelta` field.
- [ ] `ApplyStatusEffectCore` populates `statusEffectDelta` on `StatusEffectChange` requests.
- [ ] `ConsumeStatusEffect` populates `statusEffectDelta` on manually constructed `StatusEffectChange` requests.
- [ ] `CardScript` exposes an `ApplyDisplayDelta` helper.
- [ ] `RecorderAnimationPlayer` computes per-card baselines before playing root recorders.
- [ ] `StatusEffectProjectile` completion applies the corresponding deferred deltas instead of calling `CommitDisplayState()`.
- [ ] Single-effect status giving still shows the final layer count immediately after the projectile lands.
- [ ] `PowerReactionEffect` nested scenario displays Power count incrementally: parent projectile → 1, reaction projectile → 2.
- [ ] All existing Edit Mode tests pass.
- [ ] `AGENTS.md` and `docs/RegressionChecklist.md` are updated.
