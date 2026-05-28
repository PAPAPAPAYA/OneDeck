# MoveToTopPopUpBatch Animation — PRD

## 1. Overview

### 1.1 Goal

Introduce a new batch animation type **`MoveToTopPopUpBatch`** that gives staged cards the same arc-trajectory visual treatment as buried cards, while preserving the Pop Up + Slot In flow.

**Bury** currently does:
```
PopUpBatch (straight lift) → MoveToBottomBatch (arc via showPos to bottom)
```

**Stage** currently does (after recent refactor):
```
MoveToPopUpPosition (straight fly to pop-up peak) → SlotIn (straight descent to top)
```

This PRD makes **Stage** do:
```
MoveToTopPopUpBatch:
  Phase 1: All staged cards arc via showPos to their pop-up peak positions (parallel)
  Phase 2: All staged cards slot in from peak to deck top positions (parallel)
```

### 1.2 Design Rationale

- **Visual symmetry**: Bury and Stage are inverse operations (bottom vs top). They should share the same arc-trajectory language.
- **ShowPos reuse**: The existing `showPos` transform at `(0, 3, -80)` is the established arc midpoint for all deck-move animations. Staging should use it too.
- **Pop-up peak from target index**: Unlike `PopUpBatch` (which computes peak from the card's *current* position), `MoveToTopPopUpBatch` computes peak from the card's *final* deck position. This ensures the pop-up peak is visually aligned with where the card will ultimately land, making the subsequent Slot In look like a clean vertical drop rather than a diagonal correction.

---

## 2. Animation Definition

### 2.1 MoveToTopPopUpBatch — Two-Phase Playback

```
[ApplyAnimationResult]  ← update physicalCardsInDeck order first
  |
  v
Phase 1: Arc to Pop-Up Peak (parallel for all cards)
  |
  |  Per card:
  |    Start:  current world position
  |      |
  |      v  (duration/2, Ease.OutQuad)
  |    Mid:   showPos.position  (arc midpoint)
  |      |
  |      v  (duration/2, Ease.InOutQuad)
  |    Peak:  CalculatePositionAtIndex(finalTopIndex) + popUpYOffset + popUpZBoost
  |            Scale = physicalCardDeckSize * popUpScaleMultiplier
  |            isPlayingSpecialAnimation = true
  |
  +-- wait for last card to reach peak
  |
  v
Phase 2: Slot In from Peak to Deck Top (parallel for all cards)
  |
  |  Per card:
  |    Start:  pop-up peak position
  |      |
  |      v  (slotInDuration, slotInEase)
  |    End:    CalculatePositionAtIndex(finalTopIndex)
  |            Scale = physicalCardDeckSize
  |            isPlayingSpecialAnimation = false
  |            SetTargetPosition / SetTargetScale synced
  |
  +-- wait for last card to slot in
```

### 2.2 Arc Trajectory Detail

The arc uses the existing `showPos` transform as the midpoint:

```
Segment 1: currentPos → showPos.position  (halfDuration, Ease.OutQuad)
Segment 2: showPos.position → peakPos      (halfDuration, Ease.InOutQuad)
```

**Why showPos?**
- `showPos` at `(0, 3, -80)` sits between the camera `(0, 0, -100)` and the deck `(z ≈ 0)`.
- For a card at deck `(0.5, 0.5, -2.5)` staging to top, the arc goes:
  - Forward toward camera: `(0.5, 0.5, -2.5) → (0, 3, -80)`
  - Back toward deck: `(0, 3, -80) → (-0.5, 1, -2.5)` (peak at top position)
- This creates the same "fly-out, fly-back" arc language used by `MoveToBottomBatch` and `MoveToTopBatch`.

**Why not a custom midpoint?**
- Consistency with existing arc system. All deck-move arcs in the project currently use `showPos` as the default midpoint via `config.arcMidpoint ?? showPos`.
- No new config field needed.

### 2.3 Pop-Up Peak Position — Way B (Target Deck Index)

The peak is computed from the card's **final** deck position, not its current position:

```csharp
Vector3 deckPos = CalculatePositionAtIndex(finalTopIndex);
Vector3 peakPos = deckPos + Vector3.up * popUpYOffset;
peakPos.z += popUpZBoost;
```

**Why target-index-based?**
- Stage moves cards to the top of the deck. Their final position is known at animation-capture time.
- If we computed peak from the card's *current* position (like `PopUpBatch` does), a card at index 0 (bottom) would pop up at `y ≈ -0.5 + 1.5 = 1.0`, while a card at index 10 would pop up at `y ≈ 2.0 + 1.5 = 3.5`. After Slot In, both cards end up near the top (y ≈ -0.5 to 1.0). The visual gap between peak and landing would be large and jarring for cards that started far from the top.
- Computing peak from the *final* position ensures all staged cards pop up to roughly the same height (aligned with the top of the deck), making the subsequent Slot In a clean vertical descent regardless of where the card started.

---

## 3. System Architecture Changes

### 3.1 New AnimationRequestType

Add to `Assets/Scripts/Managers/AnimationRequest.cs`:

```csharp
public enum AnimationRequestType
{
    // ... existing types ...
    MoveToTopPopUpBatch   // Arc via showPos to pop-up peak, then slot in to deck top
}
```

**Reused fields on `AnimationRequest`:**
- `targetCards` — list of staged cards
- `targetIndices` — final deck indices (parallel to targetCards)
- `duration` — total arc fly duration (default 0.5f, read from `deckMoveArcDuration`)
- `useArc` — must be true for this type (ignored if false, but always true in practice)
- `snapshotDeckSize` — deck size at capture time

### 3.2 ICombatVisuals Extension

Add to `Assets/Scripts/Managers/ICombatVisuals.cs`:

```csharp
/// <summary>
/// Batch animation: arc via showPos to pop-up peak, then slot in to deck top.
/// Phase 1: all cards arc in parallel to their pop-up peaks.
/// Phase 2: all cards slot in in parallel to their final deck top positions.
/// </summary>
void MoveCardToTopPopUpBatch(List<GameObject> logicalCards, List<int> targetIndices,
    float duration, Action onComplete = null);
```

### 3.3 CombatUXManager Implementation

Add to `Assets/Scripts/UXPrototype/CombatUXManager.cs`:

**New config field** (under `[Header("DECK MOVE")]`):
```csharp
public float deckMoveArcDuration = 0.5f;
```

**`MoveCardToTopPopUpBatch`** implementation sketch:

```csharp
public void MoveCardToTopPopUpBatch(List<GameObject> logicalCards, List<int> targetIndices,
    float duration, Action onComplete = null)
{
    if (logicalCards == null || logicalCards.Count == 0)
    {
        onComplete?.Invoke();
        return;
    }

    int totalCount = logicalCards.Count;
    int phase1Done = 0;
    int phase2Done = 0;

    // Phase 1: Arc to pop-up peak (parallel)
    for (int i = 0; i < totalCount; i++)
    {
        var logicalCard = logicalCards[i];
        int finalIndex = targetIndices[i];

        var cardScript = logicalCard.GetComponent<CardScript>();
        if (cardScript == null) { phase1Done++; phase2Done++; continue; }

        BuildCardScriptToPhysicalDictionary();
        var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
        if (physicalCard == null) { phase1Done++; phase2Done++; continue; }

        var physScript = physicalCard.GetComponent<CardPhysObjScript>();
        if (physScript == null) { phase1Done++; phase2Done++; continue; }

        physScript.KillTweens();
        physScript.isPlayingSpecialAnimation = true;
        AnimationStateTracker.me?.RegisterAnimation();
        BlockInput(this);

        // Compute peak from FINAL deck position
        Vector3 deckPos = CalculatePositionAtIndex(finalIndex);
        Vector3 peakPos = deckPos + Vector3.up * popUpYOffset;
        peakPos.z += popUpZBoost;
        Vector3 peakScale = physicalCardDeckSize * popUpScaleMultiplier;

        // Arc via showPos
        Sequence arcSeq = DOTween.Sequence();
        float halfDuration = duration * 0.5f;
        arcSeq.Append(physicalCard.transform.DOMove(showPos.position, halfDuration).SetEase(Ease.OutQuad));
        arcSeq.Append(physicalCard.transform.DOMove(peakPos, halfDuration).SetEase(Ease.InOutQuad));
        arcSeq.Join(physicalCard.transform.DOScale(peakScale, duration).SetEase(Ease.OutQuad));

        arcSeq.OnComplete(() =>
        {
            phase1Done++;
            if (phase1Done >= totalCount)
            {
                // All cards reached peak — start Phase 2
                StartSlotInPhase();
            }
        });
        arcSeq.Play();
    }

    void StartSlotInPhase()
    {
        for (int i = 0; i < totalCount; i++)
        {
            var logicalCard = logicalCards[i];
            int finalIndex = targetIndices[i];

            var cardScript = logicalCard.GetComponent<CardScript>();
            if (cardScript == null) { phase2Done++; continue; }

            var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
            if (physicalCard == null) { phase2Done++; continue; }

            var physScript = physicalCard.GetComponent<CardPhysObjScript>();
            if (physScript == null) { phase2Done++; continue; }

            Vector3 targetPos = CalculatePositionAtIndex(finalIndex);

            Sequence slotSeq = DOTween.Sequence();
            slotSeq.Append(ApplySlotInEase(physicalCard.transform.DOMove(targetPos, slotInDuration)));
            slotSeq.Join(ApplySlotInEase(physicalCard.transform.DOScale(physicalCardDeckSize, slotInDuration)));
            slotSeq.OnComplete(() =>
            {
                physScript.isPlayingSpecialAnimation = false;
                physScript.SetTargetPosition(targetPos);
                physScript.SetTargetScale(physicalCardDeckSize);

                phase2Done++;
                if (phase2Done >= totalCount)
                {
                    AnimationStateTracker.me?.CompleteAnimation();
                    UnblockInput(this);
                    onComplete?.Invoke();
                }
            });
            slotSeq.Play();
        }
    }
}
```

### 3.4 ApplyAnimationResult Extension

In `CombatUXManager.ApplyAnimationResult`, add:

```csharp
case AnimationRequestType.MoveToTopPopUpBatch:
    // Same as MoveToTopBatch: remove each target card and append to end
    if (request.targetCards == null) break;
    foreach (var card in request.targetCards)
    {
        var phys = GetPhysicalCard(card);
        if (phys != null)
        {
            physicalCardsInDeck.Remove(phys);
            physicalCardsInDeck.Add(phys);
        }
    }
    break;
```

### 3.5 RecorderAnimationPlayer Extension

In `Assets/Scripts/Managers/RecorderAnimationPlayer.cs`, add:

```csharp
case AnimationRequestType.MoveToTopPopUpBatch:
{
    // Deck-focus restoration (same guard as MoveToTopBatch)
    var combatUX = visuals as CombatUXManager;
    if (combatUX != null && combatUX.IsDeckFocused)
    {
        yield return combatUX.StartCoroutine(combatUX.RestoreDeckFocusCoroutine());
    }

    visuals.ApplyAnimationResult(request);
    visuals.UpdateAllPhysicalCardTargets();

    int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
    if (totalCount == 0) break;

    bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
    int currentCount = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.Count : 0;

    // Build final indices (same correction logic as MoveToTopBatch)
    var finalIndices = new List<int>();
    for (int i = 0; i < totalCount; i++)
    {
        int correctedIndex = currentCount - totalCount + i;
        correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
        finalIndices.Add(correctedIndex);
    }

    bool done = false;
    visuals.MoveCardToTopPopUpBatch(request.targetCards, finalIndices, request.duration, () => { done = true; });
    yield return new WaitUntil(() => done);
    break;
}
```

### 3.6 NullCombatVisuals Extension

In `NullCombatVisuals.cs` and `NullCombatVisualsBehaviour.cs`, add empty implementation:

```csharp
public void MoveCardToTopPopUpBatch(List<GameObject> logicalCards, List<int> targetIndices,
    float duration, Action onComplete = null)
{
    onComplete?.Invoke();
}
```

---

## 4. Integration — StageEffect

Replace the current `MoveToPopUpPosition` + `SlotIn` capture in `StageEffect.StageChosenCards` with a single `MoveToTopPopUpBatch`:

```csharp
if (recorder != null)
{
    // Arc via showPos to pop-up peak, then slot in to top
    recorder.animationRequests.Add(new AnimationRequest {
        type = AnimationRequestType.MoveToTopPopUpBatch,
        targetCards = stagedCards,
        targetIndices = stagedTargetIndices,
        snapshotDeckSize = _combinedDeck.Count,
        duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
        useArc = true
    });
}
```

**Why a single request instead of two?**
- `MoveToTopPopUpBatch` encapsulates both phases internally. This keeps `StageEffect.cs` clean (one request instead of a loop of `MoveToPopUpPosition` + `SlotIn` pairs).
- It also enables **true parallelism**: all cards arc simultaneously, then all cards slot in simultaneously. The old approach was sequential (card A arcs+slots, then card B arcs+slots).

---

## 5. Configuration Parameters

| Parameter | Location | Default | Description |
|-----------|----------|---------|-------------|
| `deckMoveArcDuration` | `CombatUXManager` | `0.5f` | Arc fly duration for bury/stage deck-move animations. Controls both `MoveToBottomBatch` and `MoveToTopPopUpBatch`. |
| `slotInDuration` | `CombatUXManager` | `0.35f` | Slot In descent duration (Phase 2). Independent of arc duration. |
| `popUpYOffset` | `CombatUXManager` | `1.5f` | Vertical lift above final deck position. |
| `popUpZBoost` | `CombatUXManager` | `-1.0f` | Z push toward camera at peak. |
| `popUpScaleMultiplier` | `CombatUXManager` | `1.15f` | Scale at pop-up peak. |

---

## 6. Edge Cases

| ID | Case | Handling |
|----|------|----------|
| EC-1 | Card is already at top (IsCardAtTop returned true) | `StageChosenCards` filters these out before capture. No request is generated. |
| EC-2 | Reactive effect moves card between logic phase and animation phase | `targetIndices` snapshot at capture time; `ApplyAnimationResult` uses the snapshot. `MoveCardToTopPopUpBatch` uses `finalIndices` recomputed at playback time based on current deck size. |
| EC-3 | showPos is null | Arc gracefully degrades to straight line (same pattern as `MoveCardWithAnimation`). |
| EC-4 | Deck is focused/peeled | `RecorderAnimationPlayer` restores deck focus before playback (same guard as `MoveToTopBatch`). |
| EC-5 | Headless test (`NullCombatVisuals`) | Empty implementation; `onComplete` fires immediately. |
| EC-6 | Only 1 card staged | Phase 1 and Phase 2 still execute correctly (parallel with count=1 is just a single tween). |
| EC-7 | Card destroyed during Phase 1 | `MoveCardToTopPopUpBatch` defensively checks `physicalCard == null` and counts it as done for both phases. |

---

## 7. Implementation Order

### Phase 1: Core Infrastructure (Files: 5)

| # | Task | File |
|---|------|------|
| 1.1 | Add `MoveToTopPopUpBatch` to `AnimationRequestType` enum | `Assets/Scripts/Managers/AnimationRequest.cs` |
| 1.2 | Add `MoveCardToTopPopUpBatch()` to `ICombatVisuals` | `Assets/Scripts/Managers/ICombatVisuals.cs` |
| 1.3 | Implement `MoveCardToTopPopUpBatch()` in `CombatUXManager` + add `deckMoveArcDuration` | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |
| 1.4 | Add `MoveToTopPopUpBatch` case in `RecorderAnimationPlayer` | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |
| 1.5 | Add empty implementations in `NullCombatVisuals` + `Behaviour` | `Assets/Scripts/Managers/NullCombatVisuals.cs`, `NullCombatVisualsBehaviour.cs` |

### Phase 2: Consumer Updates (Files: 2)

| # | Task | File |
|---|------|------|
| 2.1 | BuryEffect: replace hardcoded `0.5f` with `CombatUXManager.me.deckMoveArcDuration` | `Assets/Scripts/Effects/BuryEffect.cs` |
| 2.2 | StageEffect: replace `MoveToPopUpPosition`+`SlotIn` loop with single `MoveToTopPopUpBatch` | `Assets/Scripts/Effects/StageEffect.cs` |

### Phase 3: Validation (Files: 1)

> **Note:** All validation items below are intended to be performed **manually by the developer** in the Unity Editor. Do not execute these via automated tools.

| # | Task | Method | Executor |
|---|------|--------|----------|
| 3.1 | Play Mode test: Stage 2+ cards, verify arc trajectory via showPos | Enter Play Mode. Reveal a card that triggers `StageMyCards(2)` (or any Stage effect on 2+ cards). Observe that staged cards fly in an arc via `showPos` to their pop-up peak, then slot in vertically to the top of the deck. | User |
| 3.2 | Play Mode test: Bury card, verify duration matches `deckMoveArcDuration` | Enter Play Mode. Trigger a Bury effect. Observe that the arc fly duration now matches the value configured in `CombatUXManager.deckMoveArcDuration` (instead of the previous hardcoded `0.5f`). | User |
| 3.3 | Headless test: run combat with `NullCombatVisualsBehaviour`, verify no null-ref | Attach `NullCombatVisualsBehaviour` to `CombatManager.visualsOverride` in the Inspector. Enter Play Mode and run a full combat. Verify the Console shows no `NullReferenceException` and all effects resolve correctly (logic only, no visuals). | User |

---

## 8. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| showPos arc looks unnatural for pop-up landing | Medium | Medium | `popUpYOffset` and `popUpZBoost` can be tuned in Inspector; peak is computed from final deck position so the landing is always vertical |
| `MoveCardToTopPopUpBatch` is complex and may have tween conflicts | Low | High | Each phase kills existing tweens (`KillTweens()`) and sets `isPlayingSpecialAnimation`. Same safety pattern as `MoveCardWithAnimation`. |
| Two-phase parallel tweens desync (one card finishes Phase 2 before another starts) | Low | Medium | Phase 2 is gated by a counter: all cards must finish Phase 1 before any starts Phase 2. |
| `targetIndices` snapshot becomes stale | Low | High | `ApplyAnimationResult` runs before animation. `finalIndices` are recomputed at playback time based on current deck size. |

---

## 9. SOP Updates Required

| Document | Update |
|----------|--------|
| `AGENTS.md` | Add `MoveToTopPopUpBatch` to AnimationRequestType list; document that Stage uses arc+pop-up+slot-in instead of straight `MoveToTopBatch` |

---

## 10. Open Questions

1. **Should `MoveToBottomBatch` also use `deckMoveArcDuration`?**
   - Decision: Yes. This PRD updates `BuryEffect.cs` to read `deckMoveArcDuration` instead of hardcoded `0.5f`, unifying bury and stage arc speeds.

2. **Should `MoveToTopPopUpBatch` support `onComplete` per card?**
   - Decision: No. Batch types use a single `onComplete` for the entire batch (same pattern as `MoveToTopBatch` / `MoveToBottomBatch`). Per-card callbacks are not needed because the logic phase has already resolved.

---

## 11. Implementation Notes

以下细节在 PRD 的伪代码中未完全展开，实施时请特别注意：

### 11.1 `BlockInput` / `UnblockInput` API 对齐

项目中的输入阻塞由 `CombatManager` 的引用计数机制管理（`BlockInput(requester)` / `UnblockInput(requester)`）。

- 3.3 伪代码中写的是 `BlockInput(this)` / `UnblockInput(this)`。
- **实施时**，请确认 `CombatUXManager` 是否已封装了直接调用 `CombatManager.Me.BlockInput(gameObject)` 的快捷方法；**如果没有**，需改为完整的调用路径（例如 `CombatManager.Me.BlockInput(gameObject)`），否则编译会失败。

### 11.2 `ApplySlotInEase` 方法存在性检查

- 3.3 伪代码中的 `ApplySlotInEase(...)` 被直接调用。
- **实施前**，请确认这是 `CombatUXManager` 的已有私有/内部方法；**如果不是**，需要补充实现（它通常是对 `Ease.InOutQuad` 或类似 ease 的封装）。

### 11.3 `ApplyAnimationResult` 中 `GetPhysicalCard` 方法名

- 3.4 伪代码中使用了 `GetPhysicalCard(card)`。
- **实施时**，请以 `CombatUXManager` 的实际代码为准。现有代码中的方法名可能是 `GetPhysicalCardFromLogicalCard(...)` 或其他变体，不要假设方法名一定与伪代码一致。

### 11.4 `deckMoveArcDuration` 的统一范围

- 当前 PRD 明确将 `BuryEffect` 的硬编码 `0.5f` 替换为 `deckMoveArcDuration`。
- **建议**：如果希望全项目的弧线动画速度统一，请一并检查 `MoveToTopBatch`（非 PopUp 版本）是否仍在使用自身的硬编码 duration；如果是，也同步替换为 `deckMoveArcDuration`。

### 11.5 DOTween `OnComplete` 与销毁防御的精确行为

- EC-7 提到 card 在 Phase 1 期间被销毁的场景。
- DOTween 的 `OnComplete` 回调在 tween 被 `Kill()` 时**默认不会触发**。
- 因此 `KillTweens()` 不会导致 `phase1Done` 自动增加；伪代码中通过 `null` 检查直接 `continue` 并递增计数器的逻辑是**必需且正确**的。
- **实施时**，请确保 reactive chain（如 `onMeStaged` → Exile）导致 card 提前被 exile/destroy 时，`phase1Done` 和 `phase2Done` 的计数仍然准确，避免 Phase 2 永远不被触发。
