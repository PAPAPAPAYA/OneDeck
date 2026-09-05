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
| **AGENTS.md Size** | Hard limit 32 KB (32,768 bytes). After any edit, run `wc -c AGENTS.md`; trim if over. Keep ≥ 1 KB headroom |

## Agent Behavior
- **Code Changes**: Do not execute code modifications except adding logs, unless the user explicitly says "修改代码". Otherwise, provide plans and solutions only.
- **Play Mode Tests**: Do not run Play Mode tests unless the user explicitly requests them (Strategy B / `unity-card-playmode-test`).
- **Document Format**: If any non Unity-generated file is found to violate the CRLF + Tab leading-indent standard, convert it to the compliant format before editing.
- **Editing AGENTS.md**: When adding content, condense wording or move detail into `plans/`/`docs/` files and reference them. Never finish an edit with the file over the 32 KB limit — the size check is part of the edit.

## Core Loop

`Shop` -> `Combat` -> `Result` -> `Shop`

## Project Structure

```
Assets/
├── Scripts/
│   ├── Managers/       # CombatManager, ShopManager, PhaseManager, CombatFuncs, EffectChainManager, GameEventStorage, ValueTrackerManager, EnumStorage, AnimationStateTracker, AttackAnimationManager, CardFactory, CardIDRetriever, CombatInfoDisplayer, CombatLog, CombatStartCardGiver, CombatStatsLogger, CostResultPresenter, DeckTester, EffectRecorder, RecorderAnimationPlayer, GameEventListener, ICombatVisuals + Null*, ShopStatsManager, StartingCardManager, UtilityFuncManagerScript, WriteRead/ (CardWinRateTracker, CombatPerCardStatsTracker, DeckSaver, EnemyDeckRecorder)
│   ├── Effects/        # EffectScript, HPAlterEffect, ShieldAlterEffect, StageEffect, BuryEffect, ExileEffect, CurseEffect, AddTempCard, AddTextEffect, CardManipulationEffect, ChangeCardTarget, ChangeHpAlterAmountEffect, PrintEffect, TransferStatusEffectEffect, StartCardShuffleEffect, shop/DeckSizeIncreaseEffect, StatusEffect/
│   ├── Card/           # CardScript, CostNEffectContainer, CardEventTrigger
│   ├── SOScripts/      # GameEvent, PlayerStatusSO, StatusEffectSO, DeckSO, BoolSO, CostCheckResult, GamePhaseSO, IntSO, ShopRarityWeightSO, StringSO
│   └── UXPrototype/    # CombatUXManager, ShopUXManager, CardPhysObjScript, CombatCardView, ShopCardView, CombatHPBarPresenter, CombatIconPresenter, HPNumericDisplay, HPNumericCounter, ResultStatsPanel, DamageFloaterPresenter, DamageFloaterTimeline
├── Prefabs/Cards/      # 3.0 no cost (current), System/, StatusEffectResolvers/
└── docs/
server/onedeck-api/     # Async-PvP backend (Express + better-sqlite3, single file). Runs on ECS, not Unity. See its README.md
```

## External References

- **Obsidian Vault**: `C:/Users/damen/Documents/Obsidian Vault/OneDeck`

## Core Architecture

- **Singletons**: `CombatManager.Me`, `ShopManager.me`, `GameEventStorage.me`, `ValueTrackerManager.me`, `EffectChainManager.Me`, `CombatFuncs.me`, `CardFactory.me`, `CardIDRetriever.Me`, `AnimationStateTracker.me`, `CombatInfoDisplayer.me`, `CombatLog.me`, `CostResultPresenter.me`, `RecorderAnimationPlayer.me`, `CombatPerCardStatsTracker.Me`
- **Event-driven**: `GameEvent` SO + `GameEventListener`
- **Component-based Cards**: `CardScript` + `EffectContainers` + `Effects`
- **Visual Abstraction**: `ICombatVisuals` interface. `CombatManager.visuals` falls back to `CombatUXManager.visuals`, or inject via `visualsOverride` (e.g. `NullCombatVisualsBehaviour` for headless tests).

## Combat System

### Flow
1. **GatherDecks**: Merge both decks, add Start Card to bottom.
2. **Reveal**: Reveal cards one by one.
3. **Start Card**: Triggers shuffle effect → captures `AnimationRequestType.Shuffle`. Skips `onMeRevealed` / `onAnyCardRevealed`.

### Zones
- `combinedDeckZone` - Merged deck (index 0 = bottom, index Count-1 = top)
- `revealZone` - Currently revealed card

### Deck Index & Direction
- `index 0` = bottom = **last revealed** = furthest back in visual stack.
- `index Count-1` = top = **first revealed** = frontmost in visual stack.
- Reveal flow always pops `combinedDeckZone[^1]` (the top card).
- **"Next" / "before this card" in deck order** means lower indices (closer to bottom, revealed later) — not "before" in time/reveal order. This is the direction `BuryNextXCards` travels.
- **Bury** sends cards to `index 0` (bottom, last revealed).
- **Stage** sends cards to `index Count-1` (top, first revealed).
- **Delay** moves a card toward `index 0` by 1 slot (later reveal).

### Physical Deck Layout (Cascade / Arc Loop / Float Stack)
- Selector: `deckLayoutMode` enum {Linear, Cascade, ArcLoop, FloatStack} — single source of truth.
- Cascade: front card (deck top) largest at the `physicalCardDeckPos` anchor; front sweeps up-left shrinking; tail hooks back tight. Shop unaffected. Legacy `xOffset/yOffset/zOffset` fields only serve the Linear fallback.
- `revealCardCountsAsDeckFront` (default `true`, cascade/arc only): the reveal-zone card holds the layout front slot, so revealing does not re-layout the deck; it slides one step on return. Source of truth: `GetCascadeDeckCount()`.
- All position math funnels through one seam: `DeckPositionCalculator.CalculatePositionAtIndex(...)`; every caller (layout, popup, slot-in, reveal, peel focus) inherits the active layout.
- **Peel deck focus**: `_focusSegmentCount` (focus index + 1) drives the layout seams via `GetLayoutDeckCount()` — focus card on the front slot at the anchor (max scale); peeled cards slide off-screen; restore = full re-layout. `deckFocusTargetPos` deprecated.
- `DeckCascadeLayout` (pure static, unit-testable): Bezier + arc-length math ported 1:1 from `docs/demo/CardArrangementDemo.html`; cached per `(deckCount, pxToWorld, Params)`.
- Cascade index mapping: `cascadeIndex = deckCount - 1 - unityIndex` (0 = front card = deck top). Z: `basePos.z - zOffset * index`.
- Per-index scale: `GetDeckScaleAtIndex(i)` = `physicalCardDeckSize` × layout scale; `cascadeScaleJitterWithCard` multiplies position jitter by the same scale.
- **Coverage normalization**: one stretch-only factor (cap `cascadeCoverageCap`) lets small decks reach the curve's hook region; large decks unaffected.
- EditMode coverage: `DeckCascadeLayoutTests.cs` / `DeckArcLoopLayoutTests.cs` / `DeckFloatStackLayoutTests.cs` (demo goldens).
- **Dynamic arc midpoint (replaces `showPos`)**: `useDynamicArcMidpoint` (default on) — deck-bound arcs take their midpoint from the layout walk at `arcMidpointCurveT` + `arcMidpointOffset` (`TryGetArcMidpointPosition`). Fallback: explicit `CardMoveConfig.arcMidpoint` > dynamic > `showPos`.
- **Arc Loop mode**: superellipse loop; slots by curvature-weighted arc length (w=0 = uniform); deck top = tilted loop's visual lowest point, deck bottom adjacent up the right; scale by screen height, z by depth rank; cards upright. PRD: `plans/plan-arc-loop-deck-layout-2026-08-12.md`.
- **Float Stack mode**: centered stack; slot 0 (lowest) drives reveal & big-shadow offsets. Math: `DeckFloatStackLayout.ComputeFrame`. PRD: `plans/plan-float-stack-center-scale-2026-08-15.md`; demo: `docs/demo/CardStackRevealDemo.html`.
- **Float Stack big shadow is follow-driven**: `BigShadowFollower` tracks the reveal card per frame; anti-light lift via `SetBigShadowLift` (ramped by `AttackAnimationManager` / `RecorderAnimationPlayer`). Enable/disable only (never destroy+re-add); self-destructs the shadow GO if the card dies mid-drive. Tunables: `plans/plan-float-stack-shadow-follow-2026-08-16.md`.

### Controls
- First click: Reveal next card.
- Second click: Trigger effect and place card at bottom.

### Auto Reveal
`CombatManager.autoReveal` (bool) skips all player confirmations inside the combat phase when set to `true`:
- Revealing the next card, triggering its effect, continuing after combat finishes.
It does **not** affect shop/result phase transitions. `DeckTester.autoSpace` is a global auto-confirm across all phases.

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
Cost checks are `CheckCost_*` methods on `CostNEffectContainer`: `Mana`, `Rested`, `Revive`, `Infected`, `Power`, `Counter`, `InGrave`, `HasEnemyCardInCombinedDeck`, `HasOwnCardOfType`, `IndexBeforeStartCard`, `EnemyCursedCardHasPower`. Failures call `SetCostNotMet(message)`.

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

#### Tag Display Name & Tooltip
- `TagTooltipDatabaseSO` (`Assets/Resources/TagTooltipDatabase.asset`, lazy singleton `Me`) maps each tag to a `displayName` StringSO (`Assets/SORefs/Strings/TagNames/`) and a tooltip `description` StringSO (`Assets/SORefs/Strings/TagTooltips/`). All StringSO assets must have `reset = false`.
- **Single source of truth**: every user-visible tag text resolves through `TagTooltipDatabaseSO.GetTagDisplayName(tag)` (falls back to the enum name when unconfigured) — in-card tag print, hover tooltip title (`CardTagTooltip`), and cardDesc `<tag:EnumName>` placeholders. To rename a tag, edit only the `TagName_*.asset` value.
- **cardDesc tag-reference v2**: `<tag:X>` renders display names via `ComputeDynamicCardDesc`; tag refs = `tag为[<tag:X>]的…卡`; `[ ]` reserved for tag phrases, card refs bare (信徒/诅咒 = tokens); 【】 deprecated 2026-09-04 — font lacks glyphs (search prefabs for `u3010` escapes). Docs: `docs/CardDesc_TagReference_Convention_v2.md`.
- Hover tooltip: `CardTagTooltip` from `CardPhysObjScript` hover; `CombatUXManager.hoverPopUpDelay` (default 0.1s) gates `PopUpCard` (0 = next frame).

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

Under `Assets/Scripts/` by role: `Managers/` CombatManager, CombatFuncs, GameEventStorage, ValueTrackerManager, EnumStorage, AnimationStateTracker, RecorderAnimationPlayer, CardFactory, ICombatVisuals, CombatLog, WriteRead/CombatPerCardStatsTracker; `Effects/` HPAlterEffect, StatusEffect/StatusEffectGiverEffect, StartCardShuffleEffect; `Card/` CardScript, CostNEffectContainer; `UXPrototype/` CombatUXManager, CombatHPBarPresenter, DeckCascadeLayout, DeckArcLoopLayout, DeckFloatStackLayout, DeckPositionCalculator, BigShadowFollower, ResultStatsPanel. Rules: `docs/GameRules.md`; UI/UX: `docs/UIUX_Guidelines.md`.

## Result Screen Per-Card Stats

Two-half panel (player-created top / enemy-created bottom) for the combat that just finished (plan: `plans/plan-result-per-card-stats-2026-07-23.md`).

- **Store**: `CombatPerCardStatsTracker.Me` (auto-created by `CombatManager.Awake()`), session-scoped: `BeginSession()` wipes all state in `GatherDecks()`. Rows keyed `(cardTypeID, creatorSide)` — the faction that CREATED the card; initial deck via `RegisterDeckComposition(combinedDeckZone)`, mid-combat via `RegisterGeneratedCard` (funneled through `CombatFuncs.AddCard_TargetSpecific`); both paths pre-create all-zero rows.
- **Exclusions** (`EnsureRecord` — the single exclusion point): neutral/start cards (`IsNeutralCard`) and `IsUtilityPassive` (passives have no combat effect chain — would only ever be all-zero rows).
- **Damage** = actual HP lost (ProcessDamage delta), creator-relative: `DamageDealtToOpponent` when the victim opposes the creator, else `DamageDealtToSelf`. Bury/Stage split by SOURCE's owner; neutral victims count neither side.
- **Hooks**: `CostNEffectContainer.InvokeEffectEvent()`, `HPAlterEffect.CheckDmgTargets_DealingDmgToOpponent/Self`, `EffectScript.ApplyStatusEffectCore` Power branch, `BuryEffect`/`StageEffect` moved-cards loops.
- **UI**: `ResultStatsPanel` builds itself fully at runtime (no prefab/scene wiring); `PhaseManager.resultStatsPanelLayout` tunes it (Play Mode Inspector edits rebuild via OnValidate). `Rounds: N` = `roundsLastCombat - 1` — the start card's opening shuffle is not a round.
- **Pitfall**: after setting stretch anchors on a fresh RectTransform, always zero `offsetMin/offsetMax` — the default 100×100 sizeDelta otherwise leaks into the final rect.
- EditMode tests: `Assets/Scripts/Editor/Tests/CombatPerCardStatsTrackerTests.cs`.

## Duplicate Slot Rule

`ShopManager.duplicateCopiesShareSlotRef` (BoolSO in `Assets/SORefs/ShopRefs/`, default OFF). ON: copies sharing a `cardTypeID` take 1 deck slot (first copy only), stack upper-left in the shop display, and only the stack base shows price (`ShopCardView.suppressPriceDisplay`); count via `UtilityFuncManagerScript.CountCardsTakingUpSpace`; empty `cardTypeID` never deduped; `CombatStartCardGiver` and enemy deck unaffected. Shop empty slots are persistent background objects (`ShopUXManager._spawnedEmptySlots`, one per deckSize grid slot) — never consumed/respawned by buy/sell; `_spawnedPlayerCards` holds real cards only; buy/sell just add/remove + `RelayoutPlayerDeckCards()`. Plan: `plans/plan-duplicate-cards-share-deck-slot-2026-07-31.md`.

## Shop Utility Passives & Board Pipeline

Plan & execution state: `plans/plan-utility-passive-shop-pipeline-2026-08-31.md` (§6).

- Metadata: `CardScript.utilityKind` (+ `utilityValue`/`utilityValue2`/`utilityRarityWeightMults`/`reservedTag`); `IsUtilityPassive` = isPassive && kind != None. Passive utility = deck-resident, occupies a slot, sellable (sell removes it); all bonuses are RECOMPUTED from the deck — no accumulating listeners.
- `UtilityShopBonus` (pure static) derives all from `playerDeckRef`: baseline growth (payday/hpMax/deckSize per session), per-kind effects, owned dedup. Sole stateful exception: deck-slot meter card — `DeckSizeIncreaseEffect` bumps run counter `DeckSlotPurchasesRef` (reset in `PhaseManager.ResetRun`), self-exiles on buy (BuyFunc skips deck add), escalating price, capped by static `maxDeckSize` (16).
- Shop HP invariant: `ApplyHpMaxFromDeck` (entry/buy/sell) sets `hp = hpMax`; shop phase is always full HP, so combat starts full (`HPMaxAlterEffect` removed 2026-09-04).
- `ShopBoardPipeline.GenerateBoard` (pure static, injected System.Random): board-type roll (`sessionUtilityBoardChances` + OddsUtility; ODDS_1 forces the visit's first board; empty utility pool → combat board) → reserved slots (boardIndex from 0, fires when `boardIndex % every == every - 1`; candidates from the CLASSIFIED pool only — combat boards never offer utility; utility drought → any-rarity fallback; combat drought → skip) → wave filters (combat generic slots, creature first) → weighted rolls (session table × shopRollWeightMultiplier × RarityWeight mults). `CurrentBoardIsUtility` drives the shop UX marker.
- Stats: result rows and `CountGraveyardCardsOf` exclude `IsUtilityPassive` (latter: no enemy GRAVE_CURSE feed); deck-population axis still counts passives; ShopStatsManager logs utility appear/buy share.

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

### Per-Projectile Status Effect Display Commit

- `AnimationRequest.statusEffectDelta` carries the signed display delta for every `StatusEffectChange` request.
- `RecorderAnimationPlayer` computes a per-card display baseline (`myStatusEffects - sum of all pending deltas`) across the recorder tree before playback.
- Deltas apply incrementally: non-deferred requests when played; deferred ones (targets with a matching `StatusEffectProjectile` in the same recorder) when the projectile completes — so nested same-target giving (e.g. `PowerReactionEffect`) updates card text per projectile instead of committing full state on the first landing.

### Snapshot Target Indices
`AnimationRequest` carries an optional `List<int> targetIndices` (parallel to `targetCards`). Effects that move cards within the deck must **snapshot** each target card's logical index at capture time **before** raising reactive events (e.g. `onMeBuried` → `StageSelf`), because reactive effects may modify deck order and pollute the index.

### ApplyAnimationResult
`ICombatVisuals` exposes `ApplyAnimationResult(AnimationRequest request)`. `RecorderAnimationPlayer` calls it **before** each deck-move request (alongside `UpdateAllPhysicalCardTargets`) so that:
1. `physicalCardsInDeck` order is advanced to the post-animation state **before** the tween starts.
2. All cards tween to new positions in parallel (the moved card plays its arc; others slide smoothly).
3. Reactive chains (e.g. bury → stage) display correctly: the first animation's result is preserved instead of overwritten by the final deck state.

### RecorderAnimationPlayer
- Singleton. Owns the animation-phase coroutine; falls back to old visual path when `RecorderAnimationPlayer.me == null`.
- Wraps playback in `AttackAnimationManager.HoldDeckFocus()` / `ReleaseDeckFocus()`.

### Emphasize Animation
Before playing an effect recorder's requests, the source card (`recorder.cardObject`) plays a brief scale pulse (1.2x over 0.25s, then back) to visually signal which card triggered the effect. Skipped if the recorder has no requests or no card object.

### Source-Card PopUp / SlotIn
- Off-reveal source cards (`recorder.sourceWasInRevealZone == false`) are automatically **popped up** before the first recorder's emphasize/shake and **slotted in** once after the last recorder that shares the same source card finishes.
- Pop-up/slot-in is scoped **per card**, not per recorder: the same source card across multiple recorders stays at the popup peak and returns to the deck only once.
- Built-in `PopUp`/`PopUpBatch`/`SlotIn`/`SlotInBatch` requests targeting the source card are skipped as duplicates; target cards still use those requests normally.
- `MoveToTopPopUpBatch` is kept unchanged: a staged source card moves from its current popup peak to the top peak and slots in; later recorders for the same source pop it up again.
- Off-reveal **Attack** recorders skip popup and keep the peel-deck focus path; if the source is already held at peak, the attack recorder reuses that popup.
- Automatic slot-in is skipped if the source card is destroyed, exiled, or moved to the reveal zone first.

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
- **Recorder path**: `BuryEffect`/`StageEffect`/`ExileEffect` no longer call `SyncPhysicalCardsWithCombinedDeck` in the logic phase; `RecorderAnimationPlayer.ApplyAnimationResult` applies reordering/destruction during playback.
- `ExileEffect` sets `revealZone = null` when exiling the revealed card, and chains `Destroy` requests with `onComplete` on the last card.
- `CombatManager.Awake()` auto-creates `RecorderAnimationPlayer` if missing.
- **afterShuffle timing**: Raised after shuffle animation completes, next card reaches reveal zone, and `PlayRecorderAnimationsAndWait()` finishes. Round Start path waits for the `MoveCardToRevealZone` callback before raising.
- **Global Combat Animation Speed**: `CombatAnimationSpeed.SpeedScale` (init from `CombatManager.combatAnimationSpeedScale`) scales all Combat-phase card animation durations; `CardPhysObjScript` applies it only in the Combat phase, so Shop animations stay at normal speed.

### Face-Down / Flip System

Deck cards are face-down by default; state lives on `CardPhysObjScript` (`isFaceUp` / `everRevealed` / `SetFaceUp`), flips are triggered from `CombatUXManager`. Hardcoded **never-cover rule**: a card once shown stays face-up until exiled or shuffled (shuffle force-covers at the arc midpoint). Full details: `docs/FaceDownFlipSystem.md`.

### Card Movement (`ICombatVisuals` / `CombatUXManager`)
- `MoveCardToRevealZone(card, onComplete)` — Move from deck to reveal zone; callback fires when movement finishes.
- `MoveCardToBottom/Top/Index(card, duration, useArc, onComplete)`
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
- **CardType**: `CardScript.cardType` (`EnumStorage.CardType` {None, Creature, Status}, append-only) replaced the `isCreature` bool (2026-09-02). Creature = attack-bearing 生物 (ATK column non-empty); Status = curse-type tokens (e.g. JU_ON) — outside every creature predicate and creature-only aura (BATTLE_HORN, RELIC_GRAVE_LORD), no special-cases anywhere. "Damaging card" predicates = `IsCreature || HasAttackAttribute` (damage capability, not type). Plan: `plans/plan-card-type-status-2026-09-02.md`.
- **cardTypeID**: Used for saving / statistics / card-type filtering (not instance ID).
- **Anti-loop**: Do not attach multiple looping effect instances to the same card.
- **GameEvent.Raise**: Use `Raise()` only for non-faction-specific events. For owner/opponent events, use `RaiseOwner()` / `RaiseOpponent()` based on the trigger object's faction. Direct `Raise()` on faction events is prohibited.
- **Neutral Cards**: `isStartCard == true` cards are neutral and skipped by `ShouldSkipEffectProcessing()`.
- **CardScript Cost Fields**: removed in the 3.0 no-cost redesign (no `buryCost`/`delayCost`/`exposeCost`/`minionCost*` fields).
- **CardScript Properties**: `displayName` (falls back to GameObject name via `GetDisplayName()`), `shopRollWeightMultiplier`, `IsNeutralCard`, `CanBeAffectedByEffects`, `takeUpSpace` (`false` cards stay in DeckSO but are not instantiated in shop/combat and cannot be sold).
- **Graveyard Removed**: Graveyard deprecated; legacy `CardManipulationEffect.Revive*` removed. Revive engine = `Effects/ReviveEffect` (plan in `plans/`); `StatusEffect.Revive` enum slot kept deprecated (implicit values, never renumber).
- **Input Block Reference Counting**: `BlockInput`/`UnblockInput` use reference counting; always pair them.
- **Visual Bug Comments**: When fixing a visual/presentation bug in `Effects/`, `UXPrototype/`, or `Managers/Animation*.cs`, use the `VISUAL-FIX(YYYY-MM-DD):` block format defined in `docs/VisualBugPrevention_Guide.md`. Search existing `VISUAL-FIX` comments before editing.
- **Regression Checklist**: Every visual bug fix must append or update a row in `docs/RegressionChecklist.md`. Do not delete obsolete rows; mark them `~~strikethrough~~` with `(Obsolete YYYY-MM-DD)`.

## Color Tags

Damage `<color=red>`, Heal `<color=#90EE90>`, Shield `<color=grey>`, Friendly `<color=#87CEEB>`, Enemy `<color=orange>`

**Single source of truth**: all colors live in `ColorSO` assets under `Assets/SORefs/Colors/`, aggregated by `GameColorPalette`. Log/rich-text: `GameColorPalette.Me.<name>.OpenTag`/`.Hex` — never hardcode hex. Components (`CardPhysObjScript`) use serialized `ColorSO` fields; HUD components read `GameColorPalette.<Name>Color` statics — HUD colors live in the palette's "HP Bar / Numeric"/"Damage Floater" groups, Edit Mode previews live-update.

---

## Unity MCP `execute_code`

Roslyn compiler installed; `compiler: "auto"` resolves to Roslyn (C# 12+, all modern syntax works). `codedom` (C# 6) is fallback only — if forced, avoid `using` declarations, bare void `return;`, `$""` interpolation, `?.`, and `yield return` (use fully-qualified names, explicit null checks, `string.Format`, return a value on all paths, no coroutines).

If a project type is not resolved (e.g. `GameEventListener`), use `System.Type.GetType("GameEventListener, Assembly-CSharp")`.

---

## Agent Post-Mortem Notes

- Trace full flow independently; PRDs can miss branches. Watch for sentinel conditions (`return`/`else`/`continue`). After moving code, do a reachability check. Read the full method body — earlier branches may be the real path. **Glob**: Use `Assets/**/FileName.cs` instead of `**/FileName.cs`
- Pixelation shaders: toggle `PixelationEffectController.me`; canvases SSC. Plan: `plans/plan-pixelation-shader-2026-08-05.md`.
