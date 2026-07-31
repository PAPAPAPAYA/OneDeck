# Plan: Hover Pop-up Interrupt — Fast A→B Gesture

Date: 2026-07-31
Status: Implemented 2026-07-31 (Fix 1 + Fix 2; deferred parallel-pop-up option not implemented)

## Goal

In Combat, when the player moves the cursor quickly from a face-up deck card A (whose hover pop-up is still playing) to another face-up deck card B, the system should:

1. Immediately interrupt A's pop-up and slot A back from its mid-air position (already happens, but the pop-up tween is left fighting the slot-in tween).
2. Pop B up as soon as the gesture settles — today B's `OnMouseEnter` is rejected during the blocked window and never retried, so B stays dead until the cursor leaves and re-enters it.

## Root Cause

- `CardPhysObjScript.OnMouseEnter` (`Assets/Scripts/UXPrototype/CardPhysObjScript.cs:1010`) silently returns when `IsHoverBlockedByCombatState()` is true (`isPlayingEffectAnimations || IsInputBlocked`, line 1022). There is no queue and no retry.
- `CombatUXManager.PopUpCard` (`CombatUXManager.cs:3086`) holds an input block from pop-up start until `popUpDuration + popUpHoldDuration` elapse (`BlockInput(this)` at :3121, `UnblockInput(this)` in `seq.OnComplete` at :3132).
- `CombatUXManager.SlotInCard` (:3142) adds its own block for `slotInDuration` (:3214/:3230).
- Cursor-leaving-A is already handled per-frame: `UpdateHover` poll → `EndHover("cursor left card")` → `SlotInCard(A)` (`CardPhysObjScript.cs:1159`, `:1135`). But:
	1. The pop-up `Sequence` is never killed (`SlotInCard` does not call `KillTweens()`, and the seq is not one of the tracked `_positionTween/_scaleTween/_rotationTween` anyway) → pop-up and slot-in tweens fight over the same transform until the pop-up's move portion ends.
	2. The pop-up seq's input block lives on through the hold interval even though the hover is already over — pure dead window.
- Unity fires `OnMouseEnter` only on cursor entry. B's single entry event lands inside the blocked window → rejected → nothing ever re-checks B while the cursor stays on it.

## Design

### Fix 1 — Interruptible pop-up/slot-in sequences

Files: `Assets/Scripts/UXPrototype/CombatUXManager.cs`, `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` (two new fields).

- Track the active pop-up/slot-in sequence per physical card, next to the existing `popUpOriginalPosition` fields on `CardPhysObjScript`:
	- `public Tween activePopUpSlotInSeq;`
	- `public Action activePopUpSlotInOnComplete;`
	- Set in `PopUpCard` / `SlotInCard` after `seq.Play()`; cleared in each seq's `OnComplete` (normal path) so the interrupt check no-ops after completion.
- New private helper in `CombatUXManager`:
	- `InterruptActivePopUpSlotIn(CardPhysObjScript physScript, string reason)`
	- If the tracked seq exists and `IsActive() && IsPlaying()`: `seq.Kill()`, then run the bookkeeping that `OnComplete` would have run, exactly once: `AnimationStateTracker.me?.CompleteAnimation()`, `UnblockInput(this)`, and invoke the saved `activePopUpSlotInOnComplete`. Clear both fields.
	- The saved `onComplete` MUST be invoked on kill: `RecorderAnimationPlayer.PlayOffRevealPopupCoroutine` / `SlotInSourceCardCoroutine` block on `WaitUntil(done)` fed by these callbacks (`RecorderAnimationPlayer.cs:442`, `:461`). Killing without invoking would soft-lock the whole animation phase.
- Call the helper:
	- At the top of `SlotInCard` (after the null guards, before computing targets). This is the hover-leave path: A's pop-up seq dies, its input block is released immediately (skipping the rest of the hold interval), and the slot-in tween owns the transform from the current mid-air position — no tween fight.
	- At the top of `PopUpCard` (after the null guards; the existing `physScript.KillTweens()` stays for layout tweens). Covers the reverse race: a recorder off-reveal source pop-up requested while a hover force-hide slot-in is still mid-flight.
- No `ICombatVisuals` interface change; `NullCombatVisuals` untouched.

### Fix 2 — Pending hover retry

File: `Assets/Scripts/UXPrototype/CardPhysObjScript.cs`.

- New field: `private bool _hoverPending;`
- `OnMouseEnter`: the two **transient** rejections set `_hoverPending = true` (with `TestManager.Log`) instead of silently returning:
	- `IsHoverBlockedByCombatState()` (input blocked / effect animations playing);
	- z-arbitration loss (an owner exists and is strictly closer).
	- Non-transient rejections stay silent no-ops with no pending: `cardImRepresenting == null` (start card), `!isFaceUp` (Rule 1).
- New `UpdatePendingHover()`, called from `Update()` before `UpdateHover()`:
	- Clear pending and return when: `cardImRepresenting == null`, `!isFaceUp`, or `!IsCursorOverCard()` (cursor left before the gates opened).
	- Keep waiting (return, pending intact) while `IsHoverBlockedByCombatState()` or z-arbitration still loses.
	- Resume when gates pass: if `_currentHoverOwner != null && != this`, call `_currentHoverOwner.EndHover("ownership lost to " + name + " (pending)")`; set `_currentHoverOwner = this`; `_hoverPending = false`; `BeginHover()`.
- Make `_hoverCollider` lazy-initialized inside `IsCursorOverCard()` (it is currently only set in `BeginHover`, which pending cards never reached).
- `_hoverPending` is also cleared in `BeginHover()` and `OnDestroy()`.
- Resulting A→B timeline: cursor leaves A → `SlotInCard(A)` kills the pop-up (Fix 1) → B's `OnMouseEnter` is still blocked (slot-in block) → `_hoverPending` set on B → slot-in completes (~`slotInDuration`) → next frame `UpdatePendingHover` resumes B → `PopUpCard(B)`.

### Deferred (superseded — see Addendum)

True simultaneity — B rises while A is still sliding back — requires distinguishing hover-animation input blocks from gameplay blocks (dedicated requester token + a `CombatManager` blocker query), since `PopUpCard`/`SlotInCard` share the `BlockInput(this)` requester with all other `CombatUXManager` animations. Re-evaluate only if playtesting finds the one-`slotInDuration` serial delay too slow.

## Addendum 2026-07-31: Immediate parallel pop-up (implemented)

Playtest verdict: the serial wait was still too slow — B must pop up the instant it is hovered, while A's pop-up/slot-in is still playing. Implemented via a pop-up/slot-in input-block sub-count instead of a requester-token system (`CombatManager.BlockInput` never actually stored requesters):

- `CombatManager.InputBlockCount` exposes the raw reference count; `ResetInputBlock()` also calls `CombatUXManager.ResetPopUpSlotInInputBlockCount()` so the sub-count can never drift out of sync and mask real blocks.
- `CombatUXManager`: `_popUpSlotInInputBlockCount` + the `BlockPopUpSlotInInput()` / `UnblockPopUpSlotInInput()` pair now wrap every `BlockInput`/`UnblockInput` in `PopUpCard`, `SlotInCard` (both paths) and `InterruptActivePopUpSlotIn`.
- `CardPhysObjScript.IsInputBlockedByNonPopUp(cm)` = `InputBlockCount - min(popUpSlotInCount, InputBlockCount) > 0`. All hover gates use it: `OnMouseEnter`, `UpdatePendingHover` (via `IsHoverBlockedByCombatState`) and the `UpdateHover` force-hide check.
- Hover is still blocked by: `isPlayingEffectAnimations` (covers all recorder-driven pop-ups), reveal-entry, reveal-to-bottom, shuffle, batch moves and any other non-pop-up input block. Gameplay input (clicks, autoReveal advance) still respects the full `IsInputBlocked`, so pop-up/slot-in keep blocking clicks exactly as before.
- `_hoverPending` now only covers z-arbitration losses and genuine input blocks; the A→B case resolves through `OnMouseEnter` directly or within 1–2 frames.

## Edge Cases

- Sweep A→B→off-deck inside the blocked window: B's pending clears when the cursor leaves B's collider → nothing pops.
- Effect animations start while pending: pending survives (`IsHoverBlockedByCombatState` covers `isPlayingEffectAnimations`); the card pops after playback settles if the cursor is still over it.
- Recorder off-reveal source pop-up (normal flow): its pop-up seq completes before its slot-in is requested → interrupt helper no-ops → behavior unchanged.
- Hover force-hide during recorder playback (`animPlaying` branch in `UpdateHover`): slot-in and recorder pop-up are now interrupt-clean in both directions via Fix 1.
- `autoReveal`: A's `EndHover` restores it, B's `BeginHover` re-pauses — a one-frame true window, identical to today's ownership-transfer path.
- Face-down cards (Rule 1) and start cards: untouched, still silent skips with no pending.
- Shop phase: `BeginHover`'s tooltip-only branch is reused unchanged by pending resumes.

## Docs / Process Requirements

- Grep `VISUAL-FIX` in both target files before editing; re-verify any nearby `Regress` scenario (per `docs/VisualBugPrevention_Guide.md` §5).
- Add `VISUAL-FIX(2026-07-31):` blocks:
	- At `InterruptActivePopUpSlotIn` — symptom: pop-up tween fights slot-in; pop-up input block outlives the hover by the hold interval.
	- At the pending-hover region — symptom: fast A→B hover gesture leaves B unresponsive until cursor re-entry.
- Append rows to `docs/RegressionChecklist.md` (scenarios 1 and 4 below).

## Test Plan (manual Play Mode — hover is cursor-driven)

1. Fast A→B gesture mid-pop-up: A returns from mid-air in one clean motion (no fight/jump); B pops up ≈ one `slotInDuration` after the cursor leaves A; afterwards input is free (click-to-reveal works, `IsInputBlocked == false`).
2. Hover A → cursor off all cards mid-pop-up: A slots back immediately, nothing else pops, input unblocks.
3. A→B→off-deck within the slot-in window: B never pops (pending cleared by cursor-leave).
4. Cascade overlap: frontmost card still wins arbitration; a deeper pending card does not steal ownership.
5. Face-down card hover: nothing happens (Rule 1), no pending.
6. `autoReveal` on: hover pauses it; fast A→B keeps it paused until B's hover ends; no permanent pause.
7. Regression — off-reveal reactive source card (recorder pop-up→slot-in, e.g. any `onMeBuried → StageSelf` card): animation completes, no hang; check `[RecorderAnimationPlayer]` logs.
8. Regression — hover A, then click to reveal while A is at peak (post-hold, input free): force-hide slot-in + effect animations complete; no stuck `isPoppedUp` flags.
9. Shop: hover tooltip unchanged.

## Implementation Files

| Change | File |
|--------|------|
| `activePopUpSlotInSeq` / `activePopUpSlotInOnComplete` fields | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` |
| `InterruptActivePopUpSlotIn` + calls in `PopUpCard` / `SlotInCard` | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |
| `_hoverPending` + `OnMouseEnter` pending branches + `UpdatePendingHover` + lazy `_hoverCollider` | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` |
| VISUAL-FIX blocks + checklist rows | both files above, `docs/RegressionChecklist.md` |

## Addendum 2026-07-31 (round 2): Re-pop-up climb fix (implemented)

**Symptom:** an already popped-up card could be popped up again mid-flight; each re-pop-up stacked
another `popUpYOffset`, so cards climbed without bound (stair-stepping deck).

**Root causes (two, compounding):**

1. `PopUpCard` computed the peak as `current position + popUpYOffset`. Once the parallel pop-up
   fix (Addendum above) allowed hover re-entry mid pop-up/slot-in, every re-pop-up launched from
   an airborne position and ratcheted the peak upward.
2. `CardPhysObjScript.OnMouseEnter` had no re-entry guard: a moving card's collider can re-cross a
   stationary cursor (during its own pop-up/slot-in or another card's move), firing `OnMouseEnter`
   again for a card that is already hovering and re-running `BeginHover` → second pop-up.

**Fix:**

- `PopUpCard` anchors the peak to the card's **logical deck slot**
  (`GetFinalDeckPositionForCard(physScript, physicalCardsInDeck.IndexOf(physicalCard)) + popUpYOffset`),
  so re-pop-ups always target the same peak; non-deck cards (reveal zone) keep the legacy
  current-position math.
- On a mid-flight interrupt (`InterruptActivePopUpSlotIn` returns true), `PopUpCard` no longer
  overwrites `popUpOriginalPosition` / `popUpOriginalScale` — the first pop-up's restore point
  stays the fallback for reveal-zone cards. (`InterruptActivePopUpSlotIn` now returns bool.)
- `OnMouseEnter` early-outs when `_hoverActive` is already true (duplicate enter).
- `VISUAL-FIX(2026-07-31)` blocks at both sites; `docs/RegressionChecklist.md` row 61.

**Verification (manual Play Mode):**

- Wiggle the cursor on/off the same face-up deck card rapidly: the card always rises to the same
  peak above its deck slot, never higher.
- Hold the cursor still while other deck cards move across it: no popup restart glitch.
- Sweep A→B→A quickly: A returns to its deck slot, no climb.
- Reveal-zone card pop-up unchanged.
