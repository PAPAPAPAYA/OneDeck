# StatusEffectGiverEffect Recorder-Driven Projectile — PRD

## 1. Overview

### 1.1 Goal

Migrate `StatusEffectGiverEffect` from the **real-time `PlayMultiStatusEffectProjectile` path** to the **EffectRecorder-driven animation system**, while **preserving the multi-target projectile VFX** (staggered parallel flight from giver to multiple targets).

This closes one of the last gaps where effect logic is deferred into animation callbacks, violating the project's Two-Phase architecture.

### 1.2 Affected Methods

| Method | Current Path | New Path |
|--------|-------------|----------|
| `GiveStatusEffect(int amount)` | Real-time `PlayMultiStatusEffectProjectile` with per-target callback | Logic-phase synchronous `ApplyStatusEffectCore` + optional recorder-captured `StatusEffectProjectile` |
| `GiveAllFriendlyStatusEffect(int amount)` | Same as above | Same as above |
| `GiveStatusEffectToLastXCards()` | Same as above | Same as above |
| `GiveStatusEffectToXFriendly()` | Same as above | Same as above |
| `GiveSelfStatusEffect(int amount)` | Already synchronous via `ApplyStatusEffectCore` | **No change required** |

### 1.3 Design Rationale

`StatusEffectGiverEffect` currently calls `combatManager.visuals?.PlayMultiStatusEffectProjectile(...)` inside the logic phase. This method:
1. Spawns projectile tweens via `DOVirtual.DelayedCall` immediately.
2. Defers `ApplyStatusEffectCore` execution into `onEachComplete` callbacks.
3. Self-manages `BlockInput` / `AnimationStateTracker`, colliding with `RecorderAnimationPlayer`'s own input blocking.

When the callback finally fires, `EffectChainManager.currentEffectRecorder` has already been popped/closed, so `ApplyStatusEffectCore`'s internal `StatusEffectChange` capture either lands on the wrong recorder or misses entirely.

The fix: **execute all logic synchronously** (status effects, events, resolvers), let `ApplyStatusEffectCore` auto-capture `StatusEffectChange` into the correct recorder, then **optionally** append a single `StatusEffectProjectile` request that carries all targets for parallel staggered playback.

---

## 2. System Architecture Changes

### 2.1 AnimationRequest — No New Fields Required

`AnimationRequest` already has a `List<GameObject> targetCards` field (used by `MoveToBottomBatch` / `MoveToTopBatch`). We **reuse** it for `StatusEffectProjectile` when there are multiple targets.

**Semantics by `type`:**

| `type` | `targetCard` | `targetCards` | Meaning |
|--------|-------------|---------------|---------|
| `StatusEffectProjectile` | single target | `null` or empty | Single-target projectile (back-compat) |
| `StatusEffectProjectile` | `null` | populated | Multi-target projectile; all targets fly in parallel with stagger |
| `StatusEffectProjectile` | populated | populated | **Undefined** — do not use both simultaneously |

> **Decision**: We do NOT add a new `targetCardScripts` field. `targetCards` is already `List<GameObject>` and perfectly matches `PlayMultiStatusEffectProjectile`'s signature after `.Select(t => t.gameObject)`.

### 2.2 RecorderAnimationPlayer — Batch StatusEffectProjectile

In `Assets/Scripts/Managers/RecorderAnimationPlayer.cs`, replace the existing `StatusEffectProjectile` switch case:

```csharp
case AnimationRequestType.StatusEffectProjectile:
{
    if (request.attackerCard == null) break;

    // Build target list: prefer batch list, fall back to single targetCard
    var targetCardScripts = new List<CardScript>();
    if (request.targetCards != null && request.targetCards.Count > 0)
    {
        foreach (var t in request.targetCards)
        {
            if (t == null) continue;
            var cs = t.GetComponent<CardScript>();
            if (cs != null) targetCardScripts.Add(cs);
        }
    }
    else if (request.targetCard != null)
    {
        var cs = request.targetCard.GetComponent<CardScript>();
        if (cs != null) targetCardScripts.Add(cs);
    }

    if (targetCardScripts.Count == 0) break;

    bool done = false;
    visuals.PlayMultiStatusEffectProjectile(
        request.attackerCard,
        targetCardScripts,
        onEachComplete: null, // logic already resolved in logic phase
        onAllComplete: () => { done = true; }
    );
    yield return new WaitUntil(() => done);
    break;
}
```

**Why this works**:
- `PlayMultiStatusEffectProjectile` already supports `List<CardScript>` with internal `staggerDelay`.
- `onEachComplete` is `null` because `ApplyStatusEffectCore` was already called in the logic phase.
- The entire batch is treated as **one** `AnimationRequest`, so `RecorderAnimationPlayer` yields only once until all projectiles complete.

### 2.3 ICombatVisuals — No Changes Required

`PlayMultiStatusEffectProjectile` signature already accepts `List<CardScript>`:

```csharp
void PlayMultiStatusEffectProjectile(
    GameObject giverCard,
    List<CardScript> targetCards,
    Action<CardScript> onEachComplete,
    Action onAllComplete = null,
    float? customStaggerDelay = null);
```

No new interface methods needed.

---

## 3. Integration Points — StatusEffectGiverEffect

### 3.1 GiveStatusEffect

**Current flow**:
1. Filter & shuffle targets.
2. Pick `amount` target cards.
3. Call `PlayMultiStatusEffectProjectile` with `ApplyStatusEffectToSingleTarget` as callback.

**New flow**:
1. Filter & shuffle targets (unchanged).
2. Pick `amount` target cards (unchanged).
3. **Synchronous logic phase**: iterate targets and call `ApplyStatusEffectCore` directly.
4. **Optional animation capture**: if `recorder != null` and projectile prefab is configured, append one `StatusEffectProjectile` request with all targets.

```csharp
public virtual void GiveStatusEffect(int amount)
{
    if (statusEffectToGive == EnumStorage.StatusEffect.None) return;

    // --- 1. Target selection (unchanged) ---
    var cardsToGiveTag = new List<GameObject>();
    UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
    if (includeSelf) cardsToGiveTag.Add(myCard);
    if (combatManager.revealZone != null && !cardsToGiveTag.Contains(combatManager.revealZone))
    {
        if (combatManager.revealZone != myCard || includeSelf)
            cardsToGiveTag.Add(combatManager.revealZone);
    }
    cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
    for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
    {
        var targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
        if (ShouldSkipCard(targetCardScript) || !MatchesTargetFilter(targetCardScript, target))
            cardsToGiveTag.RemoveAt(i);
    }
    if (!canStatusEffectBeStacked)
    {
        for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
        {
            if (cardsToGiveTag[i].GetComponent<CardScript>().myStatusEffects.Contains(statusEffectToGive))
                cardsToGiveTag.RemoveAt(i);
        }
    }
    if (cardsToGiveTag.Count <= 0) return;
    if (spreadEvenly) amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);

    // --- 2. Synchronous logic execution ---
    var targetCards = new List<CardScript>();
    for (var i = 0; i < amount; i++)
    {
        CardScript targetCardScript = spreadEvenly
            ? cardsToGiveTag[i].GetComponent<CardScript>()
            : cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
        targetCards.Add(targetCardScript);
    }

    foreach (var target in targetCards)
    {
        ApplyStatusEffectCore(target, statusEffectToGive, 1,
            myStatusEffectResolverScript, statusEffectParticlePrefab, particleYOffset, 1, 1);
    }

    // --- 3. Refresh display (synchronous) ---
    CombatInfoDisplayer.me?.RefreshDeckInfo();

    // --- 4. Capture batch projectile animation ---
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && targetCards.Count > 0)
    {
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.StatusEffectProjectile,
            attackerCard = myCard,
            targetCards = targetCards.Select(t => t.gameObject).ToList()
        });
    }
}
```

**Deleted helpers**: `ApplyStatusEffectToSingleTarget`, `ApplyStatusEffectsToTargets`.

### 3.2 GiveAllFriendlyStatusEffect

```csharp
public virtual void GiveAllFriendlyStatusEffect(int amount)
{
    if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
    if (amount <= 0) return;

    // --- 1. Collect friendly targets (unchanged) ---
    var cardsToGive = new List<GameObject>();
    // ... existing filtering logic ...

    // --- 2. Synchronous logic execution ---
    var targetCardScripts = new List<CardScript>();
    foreach (var card in cardsToGive)
    {
        var cardScript = card.GetComponent<CardScript>();
        if (!CanReceiveStatusEffect(cardScript, statusEffectToGive)) continue;
        ApplyStatusEffectToFriendlySingle(cardScript, amount);
        targetCardScripts.Add(cardScript);
    }

    CombatInfoDisplayer.me?.RefreshDeckInfo();

    // --- 3. Capture batch projectile ---
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && targetCardScripts.Count > 0)
    {
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.StatusEffectProjectile,
            attackerCard = myCard,
            targetCards = targetCardScripts.Select(t => t.gameObject).ToList()
        });
    }
}
```

**Deleted helpers**: `ApplyStatusEffectToFriendlySingle` (inline or keep as private helper that only calls `ApplyStatusEffectCore`), `ApplyStatusEffectsToFriendly`.

### 3.3 GiveStatusEffectToLastXCards

```csharp
public virtual void GiveStatusEffectToLastXCards()
{
    if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
    if (lastXCardsCount <= 0 || statusEffectLayerCount <= 0) return;

    // --- 1. Collect last X targets (unchanged) ---
    var targetCards = new List<CardScript>();
    // ... existing index-walking logic ...

    if (targetCards.Count <= 0) return;

    // --- 2. Synchronous logic execution ---
    foreach (var target in targetCards)
    {
        ApplyStatusEffectToLastXCardSingle(target);
    }

    CombatInfoDisplayer.me?.RefreshDeckInfo();

    // --- 3. Capture batch projectile ---
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null)
    {
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.StatusEffectProjectile,
            attackerCard = myCard,
            targetCards = targetCards.Select(t => t.gameObject).ToList()
        });
    }
}
```

**Deleted helpers**: `ApplyStatusEffectToLastXCardSingle` (keep if reused), `ApplyStatusEffectsToLastXCards`.

### 3.4 GiveStatusEffectToXFriendly

```csharp
public virtual void GiveStatusEffectToXFriendly()
{
    if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
    if (xFriendlyCount <= 0 || yFriendlyLayerCount <= 0) return;

    // --- 1. Collect X random friendly targets (unchanged) ---
    var targetCards = new List<CardScript>();
    // ... existing filtering & shuffling logic ...

    if (targetCards.Count <= 0) return;

    // --- 2. Synchronous logic execution ---
    foreach (var target in targetCards)
    {
        ApplyStatusEffectToXFriendlySingle(target);
    }

    CombatInfoDisplayer.me?.RefreshDeckInfo();

    // --- 3. Capture batch projectile ---
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null)
    {
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.StatusEffectProjectile,
            attackerCard = myCard,
            targetCards = targetCards.Select(t => t.gameObject).ToList()
        });
    }
}
```

**Deleted helpers**: `ApplyStatusEffectToXFriendlySingle` (keep if reused), `ApplyStatusEffectsToXFriendly`.

### 3.5 GiveStatusEffectToXFriendly_BasedOnIntSO & GiveStatusEffectToXFriendly_BasedOnStaged

These are thin wrappers that mutate `xFriendlyCount` / `yFriendlyLayerCount` and call `GiveStatusEffectToXFriendly()`. No direct changes required; they inherit the new behavior automatically.

### 3.6 GiveSelfStatusEffect & GiveSelfStatusEffectBasedOnStatusEffectCount

Already synchronous via `ApplyStatusEffectCore`. No changes.

### 3.7 GiveStatusEffectBasedOnStatusEffectCount & GiveSelfStatusEffectBasedOnStatusEffectCount

These call `GiveStatusEffect(count)` / `GiveSelfStatusEffect(count)`. No direct changes required; they inherit the new behavior automatically.

---

## 4. Helper Refactoring

### 4.1 Helpers to Remove

The following private helpers exist **only** to serve as `PlayMultiStatusEffectProjectile` callbacks. After migration they are dead code:

| Helper | Used By | Action |
|--------|---------|--------|
| `ApplyStatusEffectToSingleTarget` | `GiveStatusEffect` | **Delete** |
| `ApplyStatusEffectsToTargets` | `GiveStatusEffect` (unused) | **Delete** |
| `ApplyStatusEffectToFriendlySingle` | `GiveAllFriendlyStatusEffect` | **Inline** into `GiveAllFriendlyStatusEffect` or keep as pure logic helper |
| `ApplyStatusEffectsToFriendly` | `GiveAllFriendlyStatusEffect` (unused) | **Delete** |
| `ApplyStatusEffectToLastXCardSingle` | `GiveStatusEffectToLastXCards` | **Inline** or keep as pure logic helper |
| `ApplyStatusEffectsToLastXCards` | `GiveStatusEffectToLastXCards` (unused) | **Delete** |
| `ApplyStatusEffectToXFriendlySingle` | `GiveStatusEffectToXFriendly` | **Inline** or keep as pure logic helper |
| `ApplyStatusEffectsToXFriendly` | `GiveStatusEffectToXFriendly` (unused) | **Delete** |

> **Note**: Some `Apply*Single` methods are still useful as private helpers that wrap `ApplyStatusEffectCore` with default params. Keep them if they reduce duplication, but remove the `List<T>` batch variants.

---

## 5. Edge Cases & Compatibility

| ID | Case | Handling |
|----|------|----------|
| EC-1 | `targetCards.Count == 0` after filtering | Early return before any recorder capture. No animation request added. |
| EC-2 | `recorder == null` (headless test or legacy fallback) | Logic still executes synchronously. No projectile captured. `ApplyStatusEffectCore` auto-captures `StatusEffectChange` only when recorder exists. Headless mode is unaffected. |
| EC-3 | `statusEffectParticlePrefab == null` in `CombatUXManager` | `PlayMultiStatusEffectProjectile` early-exits and calls `onAllComplete` immediately. This is existing behavior; no regression. |
| EC-4 | Target card is destroyed between logic phase and animation phase (e.g., by a reactive exile) | `PlayMultiStatusEffectProjectile` already has defensive null checks: `if (targetCardScript == null || targetCardScript.gameObject == null)` — it increments `completedCount` and continues. No crash. |
| EC-5 | `spreadEvenly == true` with `amount > cardsToGiveTag.Count` | `Mathf.Clamp(amount, 0, cardsToGiveTag.Count)` limits the loop. Batch projectile carries the clamped target list. |
| EC-6 | `GiveSelfStatusEffect` on a card in reveal zone | No projectile needed (single self-target). `ApplyStatusEffectCore` captures `StatusEffectChange` only. Existing behavior. |
| EC-7 | `CanReceiveStatusEffect` filters out all targets after logic begins | `targetCards` list stays empty; no projectile captured. `RefreshDeckInfo` still runs. |
| EC-8 | `AnimationRequest.targetCards` reused by `MoveToBottomBatch` / `MoveToTopBatch` | No conflict — `RecorderAnimationPlayer` reads `targetCards` only inside the `StatusEffectProjectile` switch case. Other cases ignore it for this type. |
| EC-9 | Same card receives status effect twice in one effect (e.g., `amount = 2`, non-spread, random picks same card twice) | `targetCards` may contain duplicates. `PlayMultiStatusEffectProjectile` will fire two projectiles at the same target. This is existing behavior (old code also called `onEachComplete` per amount iteration). |

---

## 6. Implementation Order

### Phase 1: Core Infrastructure (Files: 2)

| # | Task | File | What changes |
|---|------|------|-------------|
| 1.1 | Extend `StatusEffectProjectile` case to support `targetCards` batch | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | Replace single-target-only logic with batch-target support; keep `targetCard` fallback |
| 1.2 | Add `using System.Linq;` if missing | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | Required for `.Select(...).ToList()` in integration code (or use manual loop to avoid Linq) |

### Phase 2: Effect Integration (Files: 1)

| # | Task | File | What changes |
|---|------|------|-------------|
| 2.1 | Rewrite `GiveStatusEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | Remove `PlayMultiStatusEffectProjectile`; synchronous `ApplyStatusEffectCore` loop; batch projectile capture |
| 2.2 | Rewrite `GiveAllFriendlyStatusEffect` | Same | Same pattern |
| 2.3 | Rewrite `GiveStatusEffectToLastXCards` | Same | Same pattern |
| 2.4 | Rewrite `GiveStatusEffectToXFriendly` | Same | Same pattern |
| 2.5 | Delete dead callback helpers | Same | Remove 4–8 unused private methods (see §4.1) |
| 2.6 | Verify wrapper methods | Same | `GiveStatusEffectBasedOnStatusEffectCount`, `GiveSelfStatusEffectBasedOnStatusEffectCount`, `GiveStatusEffectToXFriendly_BasedOnIntSO`, `GiveStatusEffectToXFriendly_BasedOnStaged` — no direct changes needed |

### Phase 3: Testing (Files: 1–2)

| # | Task | Method |
|---|------|--------|
| 3.1 | Play Mode test: single-target status effect | Strategy B: reveal a card that gives self a status effect; verify logic resolves immediately, `StatusEffectChange` plays in animation phase |
| 3.2 | Play Mode test: multi-target random status effect | Strategy B: reveal a card with `GiveStatusEffect(amount=3)`; verify 3 projectiles fly in parallel with stagger, tints apply after flight completes |
| 3.3 | Play Mode test: `GiveAllFriendlyStatusEffect` | Strategy B: reveal a card that buffs all friendly cards; verify batch projectile flies to all friendlies simultaneously |
| 3.4 | Play Mode test: `GiveStatusEffectToLastXCards` | Strategy B: reveal a card that debuffs last 2 cards; verify 2 projectiles fly to correct deck cards |
| 3.5 | Play Mode test: headless fallback | Run with `NullCombatVisualsBehaviour`; verify no exception, status effects apply correctly, no projectile request crashes |
| 3.6 | Regression test: `CurseEffect` | Strategy B: reveal a curse card; verify `StatusEffectProjectile` still works (single-target fallback path) |

---

## 7. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `targetCards` batching breaks `MoveToBottomBatch` / `MoveToTopBatch` | Low | High | `RecorderAnimationPlayer` switch-case isolates by `type`; no shared logic between `StatusEffectProjectile` and batch moves. Code review + test 3.1–3.4. |
| Projectile animation feels slower because recorder serializes it after `StatusEffectChange` | Medium | Low | `ApplyStatusEffectCore` auto-captures `StatusEffectChange` **before** our manual `StatusEffectProjectile` capture. In animation phase, `StatusEffectChange` plays first (tint + particle), then `StatusEffectProjectile` plays (flight). If this feels odd, reorder by capturing `StatusEffectProjectile` **before** the logic loop. However, the old real-time path also had tint happen near projectile arrival (in the callback). Visual parity is acceptable. |
| `PlayMultiStatusEffectProjectile` still called from other legacy code after this PRD | Low | Medium | Search for all `PlayMultiStatusEffectProjectile` callers. Only `CurseEffect` and `StatusEffectGiverEffect` family use it. `CurseEffect` already migrated to recorder (single-target). No other callers expected. |
| Accidentally deleting a helper still used elsewhere | Low | High | Before deleting any `Apply*Single` or `Apply*Batch` helper, grep for all references in `Assets/Scripts/`. Keep helpers that are referenced by non-dead code. |
| Reactive effects triggered by synchronous `ApplyStatusEffectCore` create nested recorders that also try to capture `StatusEffectProjectile` | Low | Low | This is correct Two-Phase behavior. Nested recorders are parented to the triggering recorder. Each captures its own requests. No conflict. |
| `System.Linq` not available in CodeDom contexts | Low | Medium | Use manual `foreach` + `Add` instead of `.Select(...).ToList()` in `StatusEffectGiverEffect` to avoid Linq dependency. |

---

## 8. SOP Updates Required

| Document | Update |
|----------|--------|
| `AGENTS.md` | Update `StatusEffectGiverEffect` row in Animation System section: remove "NOT recorder-driven" note; document that `StatusEffectProjectile` now supports `targetCards` batching |
| `AGENTS.md` | Update `AnimationRequest Types` list: clarify `StatusEffectProjectile` can use `targetCard` (single) or `targetCards` (batch) |

---

## 9. Open Questions

1. **Should `GiveSelfStatusEffect` also capture a `StatusEffectProjectile`?**
   - Decision: No. Self-target status effects do not need a projectile flying from the card to itself. The existing `StatusEffectChange` (tint + particle) is sufficient.

2. **Should `StatusEffectProjectile` support `customStaggerDelay` via `AnimationRequest`?**
   - Decision: Not in this PRD. Use `CombatUXManager.projectileStaggerDelay` (existing). If a per-effect stagger is needed later, add `duration` field override to `AnimationRequest`.

3. **Should `PopUp` + `SlotIn` be added around batch projectiles?**
   - Decision: Not in this PRD. `StatusEffectGiverEffect` typically targets multiple cards; popping up every target would create visual chaos. The batch projectile itself provides enough visual emphasis. Single-target paths (e.g., `CurseEffect`) already have PopUp/SlotIn handled separately.
