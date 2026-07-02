# Popup Deck Centering — Implementation Plan

## 1. Overview

### 1.1 Goal
When a card outside the `revealZone` tries to trigger a popup effect, or when `AddTempCard` creates a new card entering the deck, move the entire deck so the target card is centered at `deckFocusTargetPos`.

Key differences from the existing attack peel:
- The deck translates as a rigid block (no fanning of cards above the target).
- The reveal-zone card follows the deck translation instead of sliding off-screen.
- If the deck is already peeled/focused, transition directly from the old state to the new centered state in one smooth animation.
- Controlled by a new config flag `enablePopupDeckCentering`.

### 1.2 Scope
- Off-reveal source card with a non-attack popup (`PopUp` / `PopUpBatch`).
- `AddTempCard` new cards entering via `MoveToPopUpPosition`.
- Other cases (e.g., status effect projectiles without popup) are out of scope for this phase.

---

## 2. Requirements (from clarification)

1. Triggered by:
	- Any popup effect whose source card is not in `revealZone`.
	- `AddTempCard` new cards entering from outside the deck.
2. Center target at the existing `deckFocusTargetPos`.
3. Do **not** fan/peel cards above the target; keep the deck as a solid stack.
4. The reveal-zone card moves together with the deck (translate only, do not slide off-screen).
5. If the deck is already peeled, animate from the old peeled state to the new centered state in a single transition (restore fan + translate simultaneously).
6. Add config flag `enablePopupDeckCentering` to toggle the behavior.

---

## 3. Design

### 3.1 New State in `CombatUXManager`

Add a focus mode enum and replace the boolean `_isDeckFocused`:

```csharp
public enum DeckFocusMode
{
	None,
	AttackPeel,
	PopupCenter
}

[SerializeField] private bool enablePopupDeckCentering = true;

private DeckFocusMode _deckFocusMode = DeckFocusMode.None;
private CardScript _currentFocusCard;
private Vector3 _deckFocusOffset;
private List<GameObject> _peeledCards = new List<GameObject>();
```

`IsDeckFocused` becomes `_deckFocusMode != DeckFocusMode.None`.

### 3.2 Shared Focus Core

Refactor `FocusOnCardCoroutine` into a single core that handles both attack peel and popup centering, including transitions:

```csharp
private IEnumerator FocusOnCardCoreCoroutine(
	CardScript targetCard,
	DeckFocusMode targetMode,
	bool animateTransition)
```

Responsibilities:
1. Resolve the target index in `physicalCardsInDeck`.
2. Compute `_deckFocusOffset = deckFocusTargetPos.position - CalculatePositionAtIndex(targetIndex, physicalCardDeckPos.position)`.
3. For each card in `physicalCardsInDeck`:
	- `AttackPeel`: cards above the target slide down by `peelSlideDistance` with stagger; target and below cards slide by `_deckFocusOffset`.
	- `PopupCenter`: all cards slide only by `_deckFocusOffset`.
4. For the reveal-zone physical card:
	- `AttackPeel`: target position = `physicalCardRevealPos.position + Vector3.down * revealCardExitDistance`.
	- `PopupCenter`: target position = `physicalCardRevealPos.position + _deckFocusOffset`.
5. Animate all targets in one phase.
6. On completion, set `_deckFocusMode = targetMode`, `_currentFocusCard = targetCard`, and clear `_peeledCards` when mode is `PopupCenter`.

### 3.3 Public Entry Points

```csharp
// Existing attack peel behavior (kept for compatibility)
public IEnumerator FocusOnCardCoroutine(CardScript targetCard)
	=> FocusOnCardCoreCoroutine(targetCard, DeckFocusMode.AttackPeel, animateTransition: false);

// New popup centering (no fan)
public IEnumerator FocusOnCardForPopupCoroutine(CardScript targetCard)
	=> FocusOnCardCoreCoroutine(targetCard, DeckFocusMode.PopupCenter, animateTransition: true);

// For AddTempCard: target is an index, not a physical card yet
public IEnumerator FocusOnDeckIndexForPopupCoroutine(int deckIndex)
{
	// Compute offset as if a card exists at deckIndex.
	// If a card is about to be inserted, use post-insertion count for position consistency.
}
```

### 3.4 Transition from Existing Peel

When `_deckFocusMode != DeckFocusMode.None` and the caller requests a different target or mode:

- Start from the current layout (including fanned cards and reveal card position).
- Tween every card to the new layout in parallel.
- `_deckFocusOffset` interpolates from old value to new value.
- Reveal card interpolates from its old position to the new reveal position.
- Fanned cards return to normal relative positions.
- This satisfies "restore and translate simultaneously" in a single animation.

### 3.5 `UpdateAllPhysicalCardTargets` Adjustment

The method currently early-returns when `_isDeckFocused` to avoid overwriting the peel layout. Keep this behavior via `_deckFocusMode != DeckFocusMode.None`. While focused, only manual focus transitions update card positions.

### 3.6 `RecorderAnimationPlayer` Integration

#### A. Off-reveal popup source

In `PlayRecorderCoroutine`, replace the existing condition:

```csharp
bool sourceNeedsAttackPeel = sourceIsOffReveal && HasAttackRequest(recorder);
bool sourceNeedsPopupCenter = sourceIsOffReveal && !HasAttackRequest(recorder)
	&& CombatUXManager.me != null
	&& CombatUXManager.me.enablePopupDeckCentering;
```

Playback order:

```csharp
if (sourceNeedsAttackPeel)
	yield return CombatUXManager.me.FocusOnCardCoroutine(
		recorder.cardObject.GetComponent<CardScript>());

if (sourceNeedsPopupCenter)
	yield return CombatUXManager.me.FocusOnCardForPopupCoroutine(
		recorder.cardObject.GetComponent<CardScript>());
```

Then continue with the normal popup requests (`PopUp`, `PopUpBatch`, etc.).

#### B. `AddTempCard` `MoveToPopUpPosition`

In `PlayRequestCoroutine`, when handling `AnimationRequestType.MoveToPopUpPosition`:

```csharp
case AnimationRequestType.MoveToPopUpPosition:
	if (CombatUXManager.me != null && CombatUXManager.me.enablePopupDeckCentering)
	{
		yield return CombatUXManager.me.FocusOnDeckIndexForPopupCoroutine(request.deckIndex);
	}
	// existing MoveToPopUpPosition logic
	break;
```

### 3.7 Restore Timing

Recommended: restore deck focus after the popup phase finishes, before any deck-move requests (e.g., `MoveToBottomBatch`, `MoveToTopBatch`, `Destroy`) begin. This keeps the effect movement animations playing from the normal deck layout, which is more stable with the existing `ApplyAnimationResult` flow.

If the design later requires keeping the deck centered through the entire effect, move the restore call to after the recorder's last request.

---

## 4. Files to Modify

| File | Change |
|------|--------|
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | Add `DeckFocusMode`, config field, core focus coroutine, popup focus methods, adjust reveal card behavior, update restore logic. |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | Call popup centering for off-reveal non-attack popups and `MoveToPopUpPosition`. |
| `docs/RegressionChecklist.md` | Add regression check row per project rules. |

---

## 5. Edge Cases

| Case | Handling |
|------|----------|
| Target card not in `physicalCardsInDeck` (`AddTempCard`) | Use the logical `deckIndex` and compute offset based on the post-insertion deck count. |
| Deck already centered on the same target | Skip the focus animation. |
| Deck in `AttackPeel`, need `PopupCenter` | Single transition: un-fan cards + new offset + reveal card returns to deck-side position. |
| Deck in `PopupCenter`, need `AttackPeel` | Reverse transition: fan cards + reveal card slides off-screen. |
| `enablePopupDeckCentering == false` | Fall back to existing pure popup behavior. |
| `CombatUXManager.me == null` | Fall back to existing path. |

---

## 6. Visual Bug Prevention Notes

Because this is a visual/presentation change in `UXPrototype/` and `Managers/`, follow `docs/VisualBugPrevention_Guide.md`:

- Add `VISUAL-FIX(YYYY-MM-DD):` comment blocks around the new popup centering code.
- Append a row to `docs/RegressionChecklist.md` describing the behavior and how to verify it.

---

## 7. Open Decisions

1. **Restore timing**: Should the deck restore immediately after the popup phase, or stay centered through the effect movement? Recommendation: restore after popup.
2. **AddTempCard index offset base**: Should `FocusOnDeckIndexForPopupCoroutine` compute positions using the deck count before or after the new card is inserted? Recommendation: after insertion, for visual consistency with the final `SlotIn` target.

Confirm these two decisions before implementation.
