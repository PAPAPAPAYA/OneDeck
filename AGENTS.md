# OneDeck - AI Agent Documentation

Unity roguelike card game. Both decks are merged, shuffled, and cards are revealed one by one to trigger effects.

## Development Standards

| Item | Requirement |
|------|-------------|
| **Line Endings** | `\r\n` (CRLF) |
| **Indentation** | Tab (`\t`), spaces forbidden |
| **Command Separator** | PowerShell uses `;` instead of `&&` |
| **Comments & Docs** | English only |
| **Encoding** | UTF-8 only |

## Agent Behavior
- **Code Changes**: Do not execute code modifications except adding logs, unless the user explicitly says "修改代码". Otherwise, provide plans and solutions only.
- **Document Format**: If any non Unity-generated file is found to violate the CRLF + Tab leading-indent standard, convert it to the compliant format before editing.

## Core Loop

`Shop` -> `Combat` -> `Result` -> `Shop`

## Project Structure

```
Assets/
├── Scripts/
│   ├── Managers/       # CombatManager, ShopManager, PhaseManager, CombatFuncs, EffectChainManager, GameEventStorage, ValueTrackerManager, EnumStorage, AnimationStateTracker, AttackAnimationManager, CardFactory, CardIDRetriever, CombatInfoDisplayer, CombatLog, CombatStartCardGiver, CombatStatsLogger, CostResultPresenter, DeckTester, EffectRecorder, RecorderAnimationPlayer, GameEventListener, ICombatVisuals + Null*, ShopStatsManager, StartingCardManager, UtilityFuncManagerScript, WriteRead/
│   ├── Effects/        # EffectScript, HPAlterEffect, ShieldAlterEffect, StageEffect, BuryEffect, ExileEffect, CurseEffect, AddTempCard, AddTextEffect, CardManipulationEffect, ChangeCardTarget, ChangeHpAlterAmountEffect, HPMaxAlterEffect, PrintEffect, TransferStatusEffectEffect, BuryCostEffect, DelayCostEffect, ExposeCostEffect, MinionCostEffect, StartCardShuffleEffect, shop/DeckSizeIncreaseEffect, StatusEffect/
│   ├── Card/           # CardScript, CostNEffectContainer, CardEventTrigger
│   ├── SOScripts/      # GameEvent, PlayerStatusSO, StatusEffectSO, DeckSO, BoolSO, CostCheckResult, GamePhaseSO, IntSO, ShopRarityWeightSO, StringSO
│   └── UXPrototype/    # CombatUXManager, ShopUXManager, CardPhysObjScript, CombatCardView, ShopCardView, CombatHPBarPresenter
├── Prefabs/Cards/      # 3.0 no cost (current), System/, StatusEffectResolvers/
└── docs/
```

## External References

- **Obsidian Vault**: `C:/Users/damen/Documents/Obsidian Vault/OneDeck`

## Core Architecture

- **Singletons**: `CombatManager.Me`, `ShopManager.me`, `GameEventStorage.me`, `ValueTrackerManager.me`, `EffectChainManager.Me`, `CombatFuncs.me`, `CardFactory.me`, `CardIDRetriever.Me`, `AnimationStateTracker.me`, `CombatInfoDisplayer.me`, `CombatLog.me`, `CostResultPresenter.me`, `RecorderAnimationPlayer.me`
- **Event-driven**: `GameEvent` SO + `GameEventListener`
- **Component-based Cards**: `CardScript` + `EffectContainers` + `Effects`
- **Visual Abstraction**: `ICombatVisuals` interface. `CombatManager.visuals` falls back to `CombatUXManager.visuals`, or inject via `visualsOverride` (e.g. `NullCombatVisualsBehaviour` for headless tests).

## Combat System

### Flow
1. **GatherDecks**: Merge both decks, add Start Card to bottom.
2. **Reveal**: Reveal cards one by one.
3. **Start Card**: Triggers shuffle effect → captures `AnimationRequestType.Shuffle`. Skips `onMeRevealed` / `onAnyCardRevealed`. `afterShuffle` fires **after** shuffle animation completes and next card reaches reveal zone.

### Zones
- `combinedDeckZone` - Merged deck (index 0 = bottom, index Count-1 = top)
- `revealZone` - Currently revealed card

### Deck Index & Direction
- `index 0` = bottom = **last revealed** = furthest back in visual stack.
- `index Count-1` = top = **first revealed** = frontmost in visual stack.
- Reveal flow always pops `combinedDeckZone[^1]` (the top card).
- **"Next" in deck order** means cards at lower indices (closer to bottom, revealed later). This is the direction `BuryNextXCards` travels.
- **"Before this card in deck order"** also means lower indices — do not confuse with "before" in time/reveal order, which would mean higher indices.
- **Bury** sends cards to `index 0` (bottom, last revealed).
- **Stage** sends cards to `index Count-1` (top, first revealed).
- **Delay** moves a card toward `index 0` by 1 slot (later reveal).

### Physical Deck Layout (Cascade)
- The combat physical deck uses the **Smooth Curve Cascade Stack**: the front card (deck top) is largest at the `physicalCardDeckPos` anchor; the front segment sweeps up-left with progressively shrinking size/spacing; after the turning point the tail hooks back at minimum spacing. Shop layout is unaffected.
- `CombatUXManager.enableCascadeDeckLayout` (default `true`) toggles cascade vs the legacy linear fan. Legacy `xOffset/yOffset/zOffset` fields only serve the fallback path.
- `CombatUXManager.revealCardCountsAsDeckFront` (default `true`) counts the reveal-zone card as cascadeIndex 0 (the front slot), so every deck card sits one cascade step deeper while a card is revealed. Single source of truth: `GetCascadeDeckCount()` = `physicalCardsInDeck.Count` + 1 when the toggle is on, cascade is enabled, and `physicalCardInRevealZone != null`. Effect: revealing a card no longer re-lays-out the deck; the deck slides forward one step only when the card returns to the bottom. In `MoveRevealedCardToBottom` the legacy `effectiveCount = Count - 1` (next card leaving) and the +1 front slot cancel out, so it passes `physicalCardsInDeck.Count` when the toggle is on.
- All position math funnels through one seam: `DeckPositionCalculator.CalculatePositionAtIndex(..., CascadeConfig)`. Every caller (layout, popup peaks, slot-in, reveal entry, reveal-to-bottom, `MoveCardToIndex`, peel focus) inherits the curve with no caller-side changes.
- `DeckCascadeLayout` (pure static, unit-testable) holds the Bezier + arc-length math ported 1:1 from `docs/demo/CardArrangementDemo.html`; results are cached per `(deckCount, pxToWorld, Params)`.
- Index mapping: `cascadeIndex = deckCount - 1 - unityIndex` (cascadeIndex 0 = front card = deck top). Z depth keeps the existing formula `basePos.z - zOffset * index`.
- Per-index scale: `GetDeckScaleAtIndex(i)` = `physicalCardDeckSize` × cascade scale; position jitter is multiplied by the card's cascade scale when `cascadeScaleJitterWithCard` is on so the tight tail stays clean.
- **Coverage normalization (Plan B)**: per-card steps are scaled by one shared factor `clamp(cascadeCoverageTarget × curveLength / rawStepSum, 1, cascadeCoverageCap)` (stretch only, never compress), so small decks still reach the curve's hook region instead of looking straight. Defaults: normalize on, target 0.62, cap 2.5. Large decks sit above the target coverage naturally → factor 1 → layout unchanged.
- EditMode coverage: `Assets/Scripts/Editor/Tests/DeckCascadeLayoutTests.cs` (golden values generated from the demo).

### Controls
- First click: Reveal next card.
- Second click: Trigger effect and place card at bottom.

### Auto Reveal
`CombatManager.autoReveal` (bool) skips all player confirmations inside the combat phase when set to `true`:
- Revealing the next card.
- Triggering the current card's effect.
- Continuing after combat finishes.
It does **not** affect shop/result phase transitions. For backward compatibility, `DeckTester.autoSpace` still acts as a global auto-confirm across all phases.

### Input Blocking
`CombatManager.IsInputBlocked` uses reference counting via `BlockInput(requester)` / `UnblockInput(requester)`.

### Fatigue / Overtime
- `fatigueRevealThreshold` + `totalCardsRevealed` - Fatigue after N reveals.
- `overtimeRoundThreshold` + `fatigueAmount` - Fatigue after N rounds.

## Effect System

### Trigger Flow
`CostNEffectContainer.InvokeEffectEvent()` returns `CostCheckResult`.
Flow: Check cost -> `preEffectEvent` -> Check effect chain -> Execute effect.

### Effect Chain Manager
- **Chain creation**: Starts when no chains open, or same card triggers a *different* effect object.
- **Loop guard**: Same card instance + same effect component instance cannot be invoked twice within an open chain (checked by GameObject reference, not effectID string).
- **Depth limit**: `chainDepth` > **99** blocks further effects.
- **Chain closing**: `CloseOpenedChain()` finalizes recorders and clears state.

### Cost Types
| Method | Description |
|--------|-------------|
| `Mana(n)` | Requires n Mana stacks |
| `Rested()` | Consumes Rest status |
| `Revive(n)` | Requires n Revive stacks |
| `HasEnemyCard(n)` | Requires n enemy cards in deck |
| `Token Cost` | Consume N friendly Minions of specified type |
| `Bury Cost` | Place N friendly cards at bottom |
| `Delay Cost` | Delay N own cards by 1 position |
| `Expose Cost` | Expose N enemy cards to top |

### Status Effects
```csharp
enum StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive, Counter }
```
| Effect | Description |
|--------|-------------|
| `Power` | Damage +1 |
| `HeartChanged` | Ownership change |
| `Rest` | Skip trigger |
| `Counter` | Counter-attack / block |

### Tags
```csharp
enum Tag { None, Linger, ManaX, DeathRattle }
```

## Events

### Card-Specific
`onMeRevealed`, `onMeBought`, `onMeStaged`, `onMeBuried`, `onMeGotPower`, `onMeGotStatusEffect`, `onThisTagResolverAttached`

### Global (use `Raise()`)
`onAnyCardRevealed`, `onHostileCardRevealed`, `afterShuffle`, `beforeRoundStart`, `onAnyCardBuried`, `onAnyCardGotPower`

### Faction-Specific (use `RaiseOwner()` / `RaiseOpponent()`)
`onTheirPlayerTookDmg`, `onMyPlayerTookDmg`, `onTheirPlayerHealed`, `onMyPlayerHealed`, `onMyPlayerShieldUpped`, `onTheirPlayerShieldUpped`, `onFriendlyMinionAdded`, `onFriendlyCardExiled`, `onFriendlyFlyExiled`, `onFriendlyCardBuried`, `onEnemyCurseCardRevealed`, `onEnemyCurseCardGotPower`, `onFriendlyCardGotPower`, `onEnemyCardGotPower`

### Target-Specific (use `RaiseSpecific()`)
`RaiseSpecific(GameObject target)` raises event only on target and its children listeners.

## Key Files

| Name | Path |
|------|------|
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` |
| `CombatFuncs` | `Assets/Scripts/Managers/CombatFuncs.cs` |
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| `CombatUXManager` | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |
| `CombatHPBarPresenter` | `Assets/Scripts/UXPrototype/CombatHPBarPresenter.cs` |
| `DeckCascadeLayout` | `Assets/Scripts/UXPrototype/DeckCascadeLayout.cs` |
| `DeckPositionCalculator` | `Assets/Scripts/UXPrototype/DeckPositionCalculator.cs` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` |
| `EnumStorage` | `Assets/Scripts/Managers/EnumStorage.cs` |
| `AnimationStateTracker` | `Assets/Scripts/Managers/AnimationStateTracker.cs` |
| `RecorderAnimationPlayer` | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |
| `CardFactory` | `Assets/Scripts/Managers/CardFactory.cs` |
| `ICombatVisuals` | `Assets/Scripts/Managers/ICombatVisuals.cs` |
| `CombatLog` | `Assets/Scripts/Managers/CombatLog.cs` |
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` |
| `StartCardShuffleEffect` | `Assets/Scripts/Effects/StartCardShuffleEffect.cs` |
| `GameRules` | `docs/GameRules.md` |

## Minion Cost Mechanism

Consumes N eligible Minion cards (`isMinion == true`) from `combinedDeckZone`.
- `minionCostCount` - Number required
- `minionCostCardTypeID` - Filter by card type ID (empty = no restriction)
- `minionCostOwner` - `Me` / `Them` / `Random`

## Animation System

### Two-Phase Execution Model
1. **Logic Phase** — All effect logic executes synchronously. Effects capture `AnimationRequest`s into the current `EffectRecorder` instead of playing visuals immediately. Deck state, HP, and shields resolve immediately.
2. **Animation Phase** — After the chain closes, `CombatManager.PlayRecorderAnimationsAndWait()` collects root recorders and yields to `RecorderAnimationPlayer.PlayRecordersCoroutine()` for sequential playback.

### EffectRecorder Tree
- `EffectRecorder` MonoBehaviour carries `animationRequests` (captured intents) and `animationPlayed` flag.
- Tree navigation uses existing Transform parent-child hierarchy under `EffectChainManager`.
- Traversal order is **effect-instance-boundary interleave**: play all requests in current recorder, then recurse into unplayed direct children by sibling order.

### AnimationRequest Types
```csharp
enum AnimationRequestType { Attack, MoveToBottom, MoveToBottomBatch, MoveToTop, MoveToTopBatch, MoveToIndex, Destroy, StatusEffectChange, StatusEffectProjectile, PopUp, SlotIn, MoveToPopUpPosition, PopUpBatch, SlotInBatch, MoveToTopPopUpBatch, Shuffle, Shake }
```
- `HPAlterEffect` captures `Attack` requests (damage already resolved in logic phase; `onHit` is null).
- `BuryEffect` captures `PopUpBatch` then `MoveToBottomBatch`.
- `StageEffect` captures `MoveToTopPopUpBatch` (arc via showPos to pop-up peak, then slot in to deck top).
- `StartCardShuffleEffect` captures `Shuffle` (sourceCard = startCard, targetCards = shuffled deck). `RecorderAnimationPlayer` handles it via `PlayShuffleAnimation`; `onComplete` calls `CombatManager.OnStartCardShuffleAnimationComplete()`.
- `ExileEffect` captures `Destroy` (preceded by `PopUp` so the player sees the card being exiled).
- `ApplyStatusEffectCore` and `ManaAlterEffect` capture `StatusEffectChange` requests (status effect visuals are deferred to the animation phase; resolver instantiation stays in the logic phase).
- `StatusEffectGiverEffect` — `GiveSelfStatusEffect` runs `ApplyStatusEffectCore` (auto-captures `StatusEffectChange` only). `GiveStatusEffect`, `GiveAllFriendlyStatusEffect`, `GiveStatusEffectToLastXCards`, and `GiveStatusEffectToXFriendly` run `ApplyStatusEffectCore` synchronously then capture `PopUpBatch` + `StatusEffectProjectile` + `SlotInBatch` via `CaptureBatchStatusEffectAnimation`.
- `AddTempCard` captures `MoveToPopUpPosition` + `SlotIn` for each newly created card so it visibly enters the deck.
- `CurseEffect` captures `PopUp` + `StatusEffectProjectile` + `SlotIn` (single-target). `ConsumeHostileCursePower` captures batch `StatusEffectChange` + `PopUpBatch` + `StatusEffectProjectile` (toward `statusEffectConsumePos`, per-layer projectiles) + `SlotInBatch`.
- `ConsumeStatusEffect` — `ConsumeOwnStatusEffect` captures `PopUp` + `StatusEffectProjectile` (with `customProjectileEndPosition`) + `StatusEffectChange` + `SlotIn`. `ConsumeRandomEnemyCardsStatusEffect` captures `StatusEffectChange` + `PopUpBatch` + `StatusEffectProjectile` (`reverseProjectile=true`) + `SlotInBatch` via `CaptureBatchStatusEffectConsumeAnimation`.
- Batch types run all card movements in parallel and yield until the last completes.

**`StatusEffectProjectile` semantics:**
- `targetCard` populated, `targetCards` null/empty → single-target projectile (back-compat).
- `targetCard` null, `targetCards` populated → multi-target projectile; all targets fly in parallel with stagger.
- Do not populate both simultaneously.

### Per-Projectile Status Effect Display Commit

- `AnimationRequest.statusEffectDelta` carries the signed display delta for every `StatusEffectChange` request.
- `RecorderAnimationPlayer` computes a per-card display baseline (`myStatusEffects - sum of all pending deltas`) across the entire recorder tree before playing any root recorder.
- Deltas are applied incrementally during playback:
	- Non-deferred `StatusEffectChange`: delta applied immediately when the request plays.
	- Deferred `StatusEffectChange` (targets with a matching `StatusEffectProjectile` in the same recorder): delta applied when the projectile completes.
- This ensures nested same-target status giving (e.g. `PowerReactionEffect`) updates the card text per projectile instead of committing the full card state on the first landing.

### Snapshot Target Indices
`AnimationRequest` carries an optional `List<int> targetIndices` (parallel to `targetCards`). Effects that move cards within the deck must **snapshot** each target card's logical index at capture time **before** raising reactive events (e.g. `onMeBuried` → `StageSelf`), because reactive effects may modify deck order and pollute the index.

### ApplyAnimationResult
`ICombatVisuals` exposes `ApplyAnimationResult(AnimationRequest request)`. `RecorderAnimationPlayer` calls it **before** each deck-move request (alongside `UpdateAllPhysicalCardTargets`) so that:
1. `physicalCardsInDeck` order is advanced to the post-animation state **before** the tween starts.
2. All cards tween to their new positions in parallel (the moved card plays its arc/special animation while other cards slide smoothly).
3. Reactive chains (e.g. bury → stage) display correctly: the first animation's result is preserved instead of being overwritten by the final deck state.

### RecorderAnimationPlayer
- Singleton. Owns the animation-phase coroutine.
- Wraps playback in `AttackAnimationManager.HoldDeckFocus()` / `ReleaseDeckFocus()`.
- For deck-move requests, calls `ApplyAnimationResult(request)` **before** `UpdateAllPhysicalCardTargets()` so the physical deck order matches the animation intent and all cards tween in parallel.
- Falls back to old visual path when `RecorderAnimationPlayer.me == null`.

### Emphasize Animation
Before playing an effect recorder's requests, the source card (`recorder.cardObject`) plays a brief scale pulse (1.2x over 0.25s, then back) to visually signal which card triggered the effect. Skipped if the recorder has no requests or no card object.

### Source-Card PopUp / SlotIn
- Off-reveal source cards (`recorder.sourceWasInRevealZone == false`) are automatically **popped up** before the first recorder's emphasize/shake and **slotted in** once after the last recorder that shares the same source card finishes.
- Pop-up/slot-in is scoped **per card**, not per recorder: if the same source card appears in multiple recorders (multiple `CostNEffectContainer`s or reactive children), it stays at the popup peak across all of them and only returns to the deck once.
- Built-in `PopUp`/`PopUpBatch`/`SlotIn`/`SlotInBatch` requests targeting the source card are skipped as duplicates; target cards still use those requests normally.
- `MoveToTopPopUpBatch` is kept unchanged: a staged source card moves from its current popup peak to the top peak and slots in. If this slots the source card back in early, later recorders for the same source will pop it up again.
- Off-reveal **Attack** recorders skip popup and keep the existing peel-deck focus path. If the source card is already being held at peak, the attack recorder reuses that popup instead of peeling.
- If the source card is destroyed, exiled, or moved to the reveal zone before the final slot-in, automatic slot-in is skipped.

### AnimationStateTracker (Legacy Safety Net)
Still active as a secondary guard. `PlayRecorderAnimationsAndWait` yields until `HasActiveBatch == false` before closing the chain, ensuring any legacy-queued events flush naturally.

### Important Animation Implementation Details
- `EffectRecorder` fields: `sessionID`, `chainID`, `processedEffectID`, `cardObject`, `effectObject`, `animationRequests`, `animationPlayed`.
- `EffectChainManager.recorderStack` tracks nested recorder creation; reactive effects attach as children of the **recorder that triggered them**.
- `CurseEffect.ApplyPowerToCardWithProjectile()` captures `StatusEffectProjectile`.
- `CombatManager.isPlayingEffectAnimations` blocks reveal/effect input during playback; reset **after** `UpdateAllPhysicalCardTargets()`.
- `PlayRecorderAnimationsAndWait`: wait `HasActiveBatch` → `CloseOpenedChain()` → play roots → `finally` `ResetInputBlock()` → `UpdateAllPhysicalCardTargets()` → `isPlayingEffectAnimations = false`.
- **Deck Focus Restoration**: `RecorderAnimationPlayer` restores normal deck layout before any deck-move request if `CombatUXManager.IsDeckFocused` is true.
- Batch moves use `correctedIndex` absolute positions, ignoring `snapshotDeckSize` offsets.
- `HPAlterEffect.isStatusEffectDamage = true` skips `Attack` animation capture.
- **Recorder path**: `BuryEffect`/`StageEffect`/`ExileEffect` no longer call `SyncPhysicalCardsWithCombinedDeck` in the logic phase; deck reordering/destruction is applied by `RecorderAnimationPlayer` via `ApplyAnimationResult` during animation playback.
- `ExileEffect` sets `revealZone = null` when exiling the revealed card, and chains `Destroy` requests with `onComplete` on the last card.
- `CombatManager.Awake()` auto-creates `RecorderAnimationPlayer` if missing.
- **afterShuffle timing**: Raised **after** shuffle animation completes, next card reaches reveal zone, and `PlayRecorderAnimationsAndWait()` finishes. Round Start path waits for reveal-zone movement via `MoveCardToRevealZone` callback before raising.
- **Global Combat Animation Speed**: `CombatAnimationSpeed.SpeedScale` scales all Combat-phase card animation durations. `CombatManager.combatAnimationSpeedScale` initializes it. `CardPhysObjScript` only applies the scale when `currentGamePhaseRef` is `Combat`, so Shop card animations stay at normal speed.

### Card Movement (`ICombatVisuals` / `CombatUXManager`)
- `MoveCardToRevealZone(card, onComplete)` — Move from deck to reveal zone; callback fires when movement finishes.
- `MoveCardToBottom(card, duration, useArc, onComplete)`
- `MoveCardToTop(card, duration, useArc, onComplete)`
- `MoveCardToIndex(card, index, duration, useArc, onComplete)`
- `DestroyCardWithAnimation(card, onComplete)`
- `AddCardToDeckVisual(card)`
- `SyncPhysicalCardsWithCombinedDeck()`
- `ApplyAnimationResult(request)` — Updates `physicalCardsInDeck` order to reflect a completed animation request.
- `PlayShuffleAnimation(startCard, shuffledCards, onComplete)`
- `PlayStatusEffectProjectileToPosition(giverCard, endPosition, onComplete, ...)` — Single/projectile flight to a world position (e.g. `statusEffectConsumePos`), used by self-consume effects.

**Note:** `MoveCardWithAnimation` skips `UpdateAllPhysicalCardTargets()` in its `OnComplete` when `RecorderAnimationPlayer.me != null`, because `RecorderAnimationPlayer` handles deck sync per-request via `ApplyAnimationResult`.

**Deck Layout:** All deck position targets come from the cascade curve via `DeckPositionCalculator.CalculatePositionAtIndex` (see Combat System → Physical Deck Layout). `UpdateAllPhysicalCardTargets` sets per-index cascade scales via `GetDeckScaleAtIndex`.

## Critical Rules

- **HPAlterEffect**: Automatically adds `baseDmg.value`; set `baseDmg` to 0 when passing a specific value.
- **cardTypeID**: Used for saving / statistics / Minion cost filtering (not instance ID).
- **Anti-loop**: Do not attach multiple looping effect instances to the same card.
- **GameEvent.Raise**: Use `Raise()` only for non-faction-specific events. For owner/opponent events, use `RaiseOwner()` / `RaiseOpponent()` based on the trigger object's faction. Direct `Raise()` on faction events is prohibited.
- **Neutral Cards**: `isStartCard == true` cards are neutral and skipped by `ShouldSkipEffectProcessing()`.
- **CardScript Cost Fields**: `buryCost`, `delayCost`, `exposeCost`, `minionCostCount`, `minionCostCardTypeID`, `minionCostOwner`.
- **CardScript Properties**: `displayName` (falls back to GameObject name via `GetDisplayName()`), `shopRollWeightMultiplier`, `IsNeutralCard`, `CanBeAffectedByEffects`, `takeUpSpace` (`false` cards stay in DeckSO but are not instantiated in shop/combat and cannot be sold).
- **Graveyard Removed**: Graveyard mechanic is deprecated. `CardManipulationEffect.Revive*` methods are no-ops.
- **Input Block Reference Counting**: `BlockInput`/`UnblockInput` use reference counting; always pair them.
- **Visual Bug Comments**: When fixing a visual/presentation bug in `Effects/`, `UXPrototype/`, or `Managers/Animation*.cs`, use the `VISUAL-FIX(YYYY-MM-DD):` block format defined in `docs/VisualBugPrevention_Guide.md`. Search existing `VISUAL-FIX` comments before editing.
- **Regression Checklist**: Every visual bug fix must append or update a row in `docs/RegressionChecklist.md`. Do not delete obsolete rows; mark them `~~strikethrough~~` with `(Obsolete YYYY-MM-DD)`.

## Color Tags

Damage `<color=red>`, Heal `<color=#90EE90>`, Shield `<color=grey>`, Friendly `<color=#87CEEB>`, Enemy `<color=orange>`

---

## Unity MCP `execute_code`

Roslyn compiler is installed (verified 2026-07-18). Default `compiler: "auto"` resolves to Roslyn (C# 12+); string interpolation, null-conditional, pattern matching, `using` declarations, etc. all work. `codedom` (C# 6) remains only as a fallback — if Roslyn is ever unavailable, respect these constraints:

| Forbidden (codedom only) | Alternative |
|-----------|-------------|
| `using` declarations | Fully-qualified names (`UnityEngine.Debug.Log`) |
| `return;` (void) | `return <value>;` on **all** paths |
| `$""` interpolation | `+` or `string.Format` |
| `?.` null-conditional | Explicit `!= null` checks |
| `yield return` | No coroutines |

If a project type is not resolved (e.g. `GameEventListener`), use `System.Type.GetType("GameEventListener, Assembly-CSharp")`.

---

## Agent Post-Mortem Notes

- Trace full flow independently; PRDs can miss branches. Watch for sentinel conditions (`return`/`else`/`continue`). After moving code, do a reachability check. Read the full method body — earlier branches may be the real path. **Glob**: Use `Assets/**/FileName.cs` instead of `**/FileName.cs`
