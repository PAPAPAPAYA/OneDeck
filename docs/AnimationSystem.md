# Animation System

(Moved from AGENTS.md on 2026-09-05 during the size diet; that file keeps a condensed summary.)

## Two-Phase Execution Model
1. **Logic Phase** — All effect logic executes synchronously. Effects capture `AnimationRequest`s into the current `EffectRecorder` instead of playing visuals immediately. Deck state, HP, and shields resolve immediately.
2. **Animation Phase** — After the chain closes, `CombatManager.PlayRecorderAnimationsAndWait()` collects root recorders and yields to `RecorderAnimationPlayer.PlayRecordersCoroutine()` for sequential playback.

## EffectRecorder Tree
- `EffectRecorder` MonoBehaviour carries `animationRequests` (captured intents) and `animationPlayed` flag.
- Tree navigation uses existing Transform parent-child hierarchy under `EffectChainManager`.
- Traversal order is **effect-instance-boundary interleave**: play all requests in current recorder, then recurse into unplayed direct children by sibling order.

## AnimationRequest Types
```csharp
enum AnimationRequestType { Attack, MoveToBottom, MoveToBottomBatch, MoveToTop, MoveToTopBatch, MoveToIndex, Destroy, StatusEffectChange, StatusEffectProjectile, PopUp, SlotIn, MoveToPopUpPosition, PopUpBatch, SlotInBatch, MoveToTopPopUpBatch, Shuffle, Shake }
```
- `HPAlterEffect` captures `Attack` requests (damage already resolved in logic phase; `onHit` is null).
- `BuryEffect` captures `PopUpBatch` then `MoveToBottomBatch`.
- `StageEffect` captures `MoveToTopPopUpBatch` (arc to pop-up peak, then slot in to deck top).
- `StartCardShuffleEffect` captures `Shuffle` (sourceCard = startCard, targetCards = shuffled deck). `RecorderAnimationPlayer` handles it via `PlayShuffleAnimation`; `onComplete` calls `CombatManager.OnStartCardShuffleAnimationComplete()`.
- `ExileEffect` captures `Destroy` (preceded by `PopUp` so the player sees the card being exiled).
- `ApplyStatusEffectCore` and `ManaAlterEffect` capture `StatusEffectChange` requests (status effect visuals are deferred to the animation phase; resolver instantiation stays in the logic phase).
- `StatusEffectGiverEffect` — `GiveSelfStatusEffect` runs `ApplyStatusEffectCore` (auto-captures `StatusEffectChange` only). The other `Give*` methods (`GiveStatusEffect`, `GiveAllFriendlyStatusEffect`, `GiveStatusEffectToLastXCards`, `GiveStatusEffectToXFriendly`) run it synchronously then capture `PopUpBatch` + `StatusEffectProjectile` + `SlotInBatch` via `CaptureBatchStatusEffectAnimation`.
- `AddTempCard` captures `MoveToPopUpPosition` + `SlotIn` for each newly created card so it visibly enters the deck.
- `CurseEffect` captures `PopUp` + `StatusEffectProjectile` + `SlotIn` (single-target). `ConsumeHostileCursePower` captures batch `StatusEffectChange` + `PopUpBatch` + `StatusEffectProjectile` (toward `statusEffectConsumePos`, per-layer projectiles) + `SlotInBatch`.
- `ConsumeStatusEffect` — `ConsumeOwnStatusEffect` captures `PopUp` + `StatusEffectProjectile` (`customProjectileEndPosition`) + `StatusEffectChange` + `SlotIn`. `ConsumeRandomEnemyCardsStatusEffect` captures batch `StatusEffectChange` + `PopUpBatch` + `StatusEffectProjectile` (`reverseProjectile=true`) + `SlotInBatch` via `CaptureBatchStatusEffectConsumeAnimation`.
- Batch types run all card movements in parallel and yield until the last completes.

**`StatusEffectProjectile` semantics:**
- `targetCard` populated, `targetCards` null/empty → single-target projectile (back-compat).
- `targetCard` null, `targetCards` populated → multi-target projectile; all targets fly in parallel with stagger.
- Do not populate both simultaneously.

## Per-Projectile Status Effect Display Commit

- `AnimationRequest.statusEffectDelta` carries the signed display delta for every `StatusEffectChange` request.
- `RecorderAnimationPlayer` computes a per-card display baseline (`myStatusEffects - sum of all pending deltas`) across the recorder tree before playback.
- Deltas apply incrementally: non-deferred requests when played; deferred ones (targets with a matching `StatusEffectProjectile` in the same recorder) when the projectile completes — so nested same-target giving (e.g. `PowerReactionEffect`) updates card text per projectile instead of committing full state on the first landing.

## Snapshot Target Indices
`AnimationRequest` carries an optional `List<int> targetIndices` (parallel to `targetCards`). Effects that move cards within the deck must **snapshot** each target card's logical index at capture time **before** raising reactive events (e.g. `onMeBuried` → `StageSelf`), because reactive effects may modify deck order and pollute the index.

## ApplyAnimationResult
`ICombatVisuals` exposes `ApplyAnimationResult(AnimationRequest request)`. `RecorderAnimationPlayer` calls it **before** each deck-move request (alongside `UpdateAllPhysicalCardTargets`) so that:
1. `physicalCardsInDeck` order is advanced to the post-animation state **before** the tween starts.
2. All cards tween to new positions in parallel (the moved card plays its arc; others slide smoothly).
3. Reactive chains (e.g. bury → stage) display correctly: the first animation's result is preserved instead of overwritten by the final deck state.

## RecorderAnimationPlayer
- Singleton. Owns the animation-phase coroutine; falls back to old visual path when `RecorderAnimationPlayer.me == null`.
- Wraps playback in `AttackAnimationManager.HoldDeckFocus()` / `ReleaseDeckFocus()`.

## Emphasize Animation
Before playing an effect recorder's requests, the source card (`recorder.cardObject`) plays a brief scale pulse (1.2x over 0.25s, then back) to visually signal which card triggered the effect. Skipped if the recorder has no requests or no card object.

## Source-Card PopUp / SlotIn
- Off-reveal source cards (`recorder.sourceWasInRevealZone == false`) are automatically **popped up** before the first recorder's emphasize/shake and **slotted in** once after the last recorder that shares the same source card finishes.
- Pop-up/slot-in is scoped **per card**, not per recorder: the same source card across multiple recorders stays at the popup peak and returns to the deck only once.
- Built-in `PopUp`/`PopUpBatch`/`SlotIn`/`SlotInBatch` requests targeting the source card are skipped as duplicates; target cards still use those requests normally.
- `MoveToTopPopUpBatch` is kept unchanged: a staged source card moves from its current popup peak to the top peak and slots in; later recorders for the same source pop it up again.
- Off-reveal **Attack** recorders skip popup and keep the peel-deck focus path; if the source is already held at peak, the attack recorder reuses that popup.
- Automatic slot-in is skipped if the source card is destroyed, exiled, or moved to the reveal zone first.

## AnimationStateTracker (Legacy Safety Net)
Still active as a secondary guard. `PlayRecorderAnimationsAndWait` yields until `HasActiveBatch == false` before closing the chain, ensuring any legacy-queued events flush naturally.

## Important Animation Implementation Details
- `EffectRecorder` fields: `sessionID`, `chainID`, `processedEffectID`, `cardObject`, `effectObject`, `animationRequests`, `animationPlayed`.
- `EffectChainManager.recorderStack` tracks nested recorder creation; reactive effects attach as children of the **recorder that triggered them**.
- `CurseEffect.ApplyPowerToCardWithProjectile()` captures `StatusEffectProjectile`.
- `CombatManager.isPlayingEffectAnimations` blocks reveal/effect input during playback; reset **after** `UpdateAllPhysicalCardTargets()`.
- `PlayRecorderAnimationsAndWait`: wait `HasActiveBatch` → `CloseOpenedChain()` → play roots → `finally` `ResetInputBlock()` → `UpdateAllPhysicalCardTargets()` → `isPlayingEffectAnimations = false`.
- **Deck Focus Restoration**: `RecorderAnimationPlayer` restores normal deck layout before any deck-move request if `CombatUXManager.IsDeckFocused` is true.
- Batch moves use `correctedIndex` absolute positions, ignoring `snapshotDeckSize` offsets.
- `HPAlterEffect.isStatusEffectDamage = true` skips `Attack` animation capture.
- **Recorder path**: `BuryEffect`/`StageEffect`/`ExileEffect` no longer call `SyncPhysicalCardsWithCombinedDeck` in the logic phase; `RecorderAnimationPlayer.ApplyAnimationResult` applies reordering/destruction during playback.
- `ExileEffect` sets `revealZone = null` when exiling the revealed card, and chains `Destroy` requests with `onComplete` on the last card.
- `CombatManager.Awake()` auto-creates `RecorderAnimationPlayer` if missing.
- **afterShuffle timing**: Raised after shuffle animation completes, next card reaches reveal zone, and `PlayRecorderAnimationsAndWait()` finishes. Round Start path waits for the `MoveCardToRevealZone` callback before raising.
- **Global Combat Animation Speed**: `CombatAnimationSpeed.SpeedScale` (init from `CombatManager.combatAnimationSpeedScale`) scales all Combat-phase card animation durations; `CardPhysObjScript` applies it only in the Combat phase, so Shop animations stay at normal speed.

## Face-Down / Flip System

Deck cards are face-down by default; state lives on `CardPhysObjScript` (`isFaceUp` / `everRevealed` / `SetFaceUp`), flips are triggered from `CombatUXManager`. Hardcoded **never-cover rule**: a card once shown stays face-up until exiled or shuffled (shuffle force-covers at the arc midpoint). Full details: `docs/FaceDownFlipSystem.md`.

## Card Movement (`ICombatVisuals` / `CombatUXManager`)
- `MoveCardToRevealZone(card, onComplete)` — Move from deck to reveal zone; callback fires when movement finishes.
- `MoveCardToBottom/Top/Index(card, duration, useArc, onComplete)`
- `DestroyCardWithAnimation(card, onComplete)`
- `AddCardToDeckVisual(card)`
- `SyncPhysicalCardsWithCombinedDeck()`
- `ApplyAnimationResult(request)` — Updates `physicalCardsInDeck` order to reflect a completed animation request.
- `PlayShuffleAnimation(startCard, shuffledCards, onComplete)`
- `PlayStatusEffectProjectileToPosition(giverCard, endPosition, onComplete, ...)` — Single/projectile flight to a world position (e.g. `statusEffectConsumePos`), used by self-consume effects.

**Note:** `MoveCardWithAnimation` skips `UpdateAllPhysicalCardTargets()` in its `OnComplete` when `RecorderAnimationPlayer.me != null`, because `RecorderAnimationPlayer` handles deck sync per-request via `ApplyAnimationResult`.

**Deck Layout:** All deck position targets come from the cascade curve via `DeckPositionCalculator.CalculatePositionAtIndex` (see `docs/DeckLayouts.md`). `UpdateAllPhysicalCardTargets` sets per-index cascade scales via `GetDeckScaleAtIndex`.
