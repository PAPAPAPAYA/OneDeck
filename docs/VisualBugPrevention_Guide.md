# Visual Bug Prevention Guide — OneDeck

## Purpose

This document defines the workflow and coding standards for fixing visual / presentation bugs in OneDeck to minimize regressions (fixing one bug and re-introducing an old one).

---

## 1. Comment Convention: VISUAL-FIX

All visual bug fixes must use a standardized comment block so the intent is **searchable** and **impossible to ignore** during future edits.

### Format

```csharp
// VISUAL-FIX(YYYY-MM-DD): One-line description of the symptom
//   Cause:     Why the bug happened
//   Affects:   Which effects / systems are involved
//   Regress:   How to manually test / reproduce
//   Related:   Card combos or prefabs that trigger this path (optional)
```

### Example

```csharp
// VISUAL-FIX(2026-05-20): Bury-then-Stage causes card to flicker to wrong deck position
//   Cause:    ApplyAnimationResult updates physicalCardsInDeck, but a reactive Stage
//             inside the same chain re-orders indices before the tween finishes
//   Affects:  BuryEffect, StageEffect, MoveToBottomBatch, MoveToTopPopUpBatch
//   Regress:  Reveal a card with BuryNextXCards that buries CardA, and CardA has onMeBuried -> StageSelf
//   Related:  Card_StoneShell, Card_RisingFlame
```

### Usage Rule
- Search `VISUAL-FIX` before editing any code in `Effects/`, `UXPrototype/`, or `Managers/Animation*` to see what historical fixes you might break.
- If you remove or alter a `VISUAL-FIX` block, you **must** verify the `Regress` scenario still passes.

---

## 2. Regression Checklist (Version-Controlled)

Do **not** keep the test matrix in a separate spreadsheet or note app. It lives in `docs/RegressionChecklist.md` and is committed together with the bug-fix code.

### File: `docs/RegressionChecklist.md`

Structure:

```markdown
| # | Scenario | System / Effect | Fixed Date | Verification Method |
|---|----------|-----------------|------------|---------------------|
| 1 | Bury → Stage position flicker | BuryEffect, StageEffect | 2026-05-20 | Play Mode: StoneShell + RisingFlame combo |
| 2 | Multi-target StatusEffectProjectile mis-alignment | StatusEffectGiverEffect | 2026-05-22 | Play Mode: Give Power to 3+ friendly cards at once |
| 3 | Chain closes but input stays blocked | EffectChainManager | 2026-05-23 | Play Mode: Rapid-click to force a deep chain |
```

### Rule
- Every bug-fix PR / commit must **append or update at least one row** in this table.
- If a row becomes obsolete (code refactored away), mark it `~~strikethrough~~` and add `(Obsolete YYYY-MM-DD)` rather than deleting it.

---

## 3. Defensive Encapsulation

If a visual bug has been fixed **twice or more**, the logic must be extracted into a named method instead of being copy-pasted or scattered as inline tweaks.

### Guideline

```csharp
// BAD: scattered inline fixes
physicalCardsInDeck.Insert(0, card);
if (RecorderAnimationPlayer.me != null) { ... }
UpdateAllPhysicalCardTargets();

// GOOD: single semantic method that centralizes known edge-cases
public void InsertCardAtDeckBottomWithSync(CardScript card, string context)
{
    // context = e.g. "BuryEffect_PostChain" for traceability
    ...
}
```

### Where to encapsulate
- Deck-order mutations (`physicalCardsInDeck` modifications)
- `ApplyAnimationResult` callers
- Input block reference-count pairs (`BlockInput` / `UnblockInput`)
- DOTween sequence builders that depend on chain state

---

## 4. Automated Regression Tests (High-Value Targets)

Use the existing `EditModeTests.csproj` / `Tests.csproj` infrastructure to automate the scenarios that are painful to manually verify.

### Priority Scenarios for Play Mode / Edit Mode Tests

1. **Chain depth + input blocking**
   - Verify `BlockInput` / `UnblockInput` ref-count is zero after `CloseOpenedChain()`.
2. **Bury → Stage reactive chain**
   - Verify final deck order and `physicalCardsInDeck` count after a Bury that triggers Stage.
3. **Recorder tree traversal order**
   - Verify `EffectRecorder.animationRequests` are played in effect-instance-boundary interleave order.
4. **Status effect batch animations**
   - Verify `StatusEffectProjectile` with multiple targets does not throw index errors when deck state changes mid-animation.

### Tip
`CombatManager.visualsOverride` supports `NullCombatVisualsBehaviour`; use it in Edit Mode tests to run effect chain logic without playing actual tweens, then inspect the `EffectRecorder` tree directly.

---

## 5. Pre-Commit Self-Check

Before committing any change inside `Assets/Scripts/Effects/`, `Assets/Scripts/UXPrototype/`, or `Assets/Scripts/Managers/*Animation*.cs`, answer the following:

```text
[ ] Did I grep for VISUAL-FIX in the files I modified?
[ ] Did I read every VISUAL-FIX comment near my changes and confirm I did not break its Regress scenario?
[ ] If I fixed a new visual bug, did I add a VISUAL-FIX comment?
[ ] Did I update docs/RegressionChecklist.md?
[ ] If this is the second fix for the same symptom, did I encapsulate it into a named method?
```

---

## 6. Quick-Reference: Where Visual Bugs Hide

| Area | Typical Regression Pattern |
|------|---------------------------|
| `BuryEffect` + reactive `StageEffect` | Deck index out of sync after `ApplyAnimationResult` |
| `StatusEffectGiverEffect` (batch) | `targetIndices` snapshot stale because of mid-chain deck mutation |
| `EffectChainManager.CloseOpenedChain()` | Input block not released if an exception fires during animation playback |
| `RecorderAnimationPlayer` | `HoldDeckFocus` / `ReleaseDeckFocus` mismatch causes cards to freeze off-position |
| `CombatUXManager.SyncPhysicalCardsWithCombinedDeck()` | Called too early or too late relative to the tween lifecycle |

---

## Action Items for Immediate Setup

1. ~~**Today**: Rename / re-format all existing visual bug comments in `Effects/` and `UXPrototype/` to the `VISUAL-FIX(YYYY-MM-DD):` block format.~~ ✅ Done 2026-05-24 (12 blocks across 5 files)
2. ~~**This week**: Create `docs/RegressionChecklist.md` and seed it with the last 3–5 visual bugs you fixed.~~ ✅ Done 2026-05-24 (6 scenarios seeded)
3. **Next bug fix**: Follow the full workflow (code + VISUAL-FIX comment + checklist row + pre-commit self-check).
4. **Ongoing**: When a bug is fixed a second time in the same area, encapsulate the logic and add an Edit Mode or Play Mode test.
