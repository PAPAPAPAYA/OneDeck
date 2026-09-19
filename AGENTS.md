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
- **SaveScene is pre-approved** (user ruling 2026-09-19): `EditorSceneManager.SaveScene` / `SaveOpenScenes` is considered a safe operation and may be called without asking. That is the only editor state-write with blanket permission — everything else still follows the rules above. Typical use: save before a Test Runner run so the "Scene(s) Have Been Modified" modal never appears.
- **Document Format**: If any non Unity-generated file is found to violate the CRLF + Tab leading-indent standard, convert it to the compliant format before editing.
- **Editing AGENTS.md**: When adding content, condense wording or move detail into `plans/`/`docs/` files and reference them. Never finish an edit with the file over the 32 KB limit — the size check is part of the edit.

## Parallel Session Registry
- Before any task that edits files or uses Unity Editor tools, create `.agent_registry/<YYYYMMDD-HHMMSS>-<task>.md` (UTC+8): task summary, planned file paths, editor claims (`execute_code` / `run_tests` / SaveScene / Play).
- Scan all claims before starting; overlap → report to the user and let them decide; never block-wait.
- Only create/delete your own claim file; never modify others'. Delete yours on completion or abort. Claims with mtime older than 48h are stale — any session may delete.
- Spec: `docs/AgentRegistry.md`.

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

## Code Placement Guide

Route new work by capability domain; god files (`CombatManager` / `RecorderAnimationPlayer` / `CombatUXManager`) are under managed split (`plans/plan-manager-split-combatmanager-rap-cux-2026-09-16.md`):

| New work | Where it goes |
|---|---|
| Card behavior / new effects | `Effects/` + `GameEventListener` bindings; pure math → static helpers, not managers |
| Combat orchestration (zones / fatigue / throne / input block) | Child component on the CombatManager GameObject behind the `CombatManager.Me` facade — do not grow `CombatManager.cs` |
| Combat presentation / visuals | Own component or `CombatUXManager.<Area>.cs` partial — never the `CombatUXManager.cs` main file |
| Animation playback | `RecorderAnimationPlayer` per-request-type coroutines / partials; pre-play pure computation → static planner class |
| Shop generation | `ShopBoardPipeline` (pure, injected Random); shop state/UI → `ShopManager` / `ShopUXManager` |

Clean-as-You-Code rule: edits inside the three god files stay bug-fix sized; feature-sized additions must land in a new/partial file or child component, wiring through the existing facade singletons.

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

### Trigger Order Config
- Global/faction `GameEvent.Raise` fires listeners in **reverse instantiation order** (`GameEventListener` registers in `OnEnable`).
- `CardTriggerOrderConfig` SO (`Assets/SORefs/CardTriggerOrderConfig.asset`, wired on `CombatManager`) + `CardTriggerOrderManager` reorder deck instantiation in `GatherDecks`: smaller `triggerOrder` = earlier trigger, applied per side; configured cards fire before unconfigured ones; mid-combat spawned cards always fire first (not configurable).

### Deck Index & Direction
- `index 0` = bottom = **last revealed** = furthest back in visual stack.
- `index Count-1` = top = **first revealed** = frontmost in visual stack.
- Reveal flow always pops `combinedDeckZone[^1]` (the top card).
- **"Next" / "before this card" in deck order** means lower indices (closer to bottom, revealed later) — not "before" in time/reveal order. This is the direction `BuryNextXCards` travels.
- **Bury** sends cards to `index 0` (bottom, last revealed).
- **Stage** sends cards to `index Count-1` (top, first revealed).
- **Delay** moves a card toward `index 0` by 1 slot (later reveal).

### Physical Deck Layout (Cascade / Arc Loop / Float Stack)
- Selector: `deckLayoutMode` enum {Linear, Cascade, ArcLoop, FloatStack} — single source of truth. All position math funnels through `DeckPositionCalculator.CalculatePositionAtIndex(...)`; per-index scale via `GetDeckScaleAtIndex`. EditMode: `DeckCascadeLayoutTests` / `DeckArcLoopLayoutTests` / `DeckFloatStackLayoutTests` (demo goldens).
- Cascade math ported 1:1 from `docs/demo/CardArrangementDemo.html` (`DeckCascadeLayout`, pure static, cached); `revealCardCountsAsDeckFront` (default `true`) keeps the reveal-zone card on the layout front slot.
- Peel deck focus: `_focusSegmentCount` drives the layout seams via `GetLayoutDeckCount()`; `deckFocusTargetPos` deprecated. Dynamic arc midpoint (`useDynamicArcMidpoint`, default on) replaces `showPos`. Float Stack big shadow is follow-driven (`BigShadowFollower`, enable/disable only).
- Full mode details (Arc Loop / Float Stack / shadow follow / coverage normalization): `docs/DeckLayouts.md`.

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
- **Loop guard**: Same card instance + same effect component instance cannot be invoked twice within a chain generation (GameObject reference, not effectID string). History persists across mid-cascade closes (2026-09-17, starves the 09-13 bury-recursion); `EndAttackSegmentScope` re-arms per attack segment.
- **Depth limit**: `chainDepth` > 99 blocks effects (per-cascade backstop; the 2026-09-14 "12" commit only changed a log var, never enforced — reverted 2026-09-17).
- **Chain closing**: `CloseOpenedChain()` finalizes recorders only; guard history + `chainDepth` reset via `ResetGenerationGuards()` at CombatManager phase/reveal boundaries.

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
- Hover tooltip: `CardTagTooltip` from `CardPhysObjScript` hover; `CombatUXManager.hoverPopUpDelay` (default 0.1s) gates `PopUpCard` (0 = next frame). Shows a card-type block before tag blocks via `CardTypeTooltipDatabaseSO` (`Assets/Resources/CardTypeTooltipDatabase.asset`): Creature=实体/可攻击, None=现象/不可攻击; Token and Start Card show no type block (types without a DB entry are excluded).

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

Two-half panel for the just-finished combat; store `CombatPerCardStatsTracker.Me` (session-scoped, rows keyed `(cardTypeID, creatorSide)` = the card's CREATOR); neutral/start cards and utility passives excluded (`EnsureRecord`). Damage = actual HP lost, creator-relative. Details: `docs/ResultPerCardStats.md`; plan `plans/plan-result-per-card-stats-2026-07-23.md`; EditMode tests `CombatPerCardStatsTrackerTests.cs`.

## Shop Systems

- **Duplicate slot rule**: `ShopManager.duplicateCopiesShareSlotRef` (BoolSO, default OFF) — same-`cardTypeID` copies share 1 deck slot, stacked; only the stack base shows price. Empty slots are persistent background objects, never consumed by buy/sell.
- **Utility passives & board pipeline**: `CardScript.utilityKind` + `IsUtilityPassive`; bonuses recomputed from the deck by `UtilityShopBonus` (no accumulating listeners); board generation in `ShopBoardPipeline.GenerateBoard` (pure static, injected Random); deck-slot meter card via `DeckSizeIncreaseEffect` (cap 16). Utility passives are slot-free (`occupiesDeckSlot` = false, skips buy capacity) but physical (`physicalDeckCard`, ex-`takeUpSpace`: displayed/sellable/combat-instantiated). Shop HP invariant: shop phase always full HP.
- **World-only interaction (2026-09-18)**: all interactive shop chrome is world-space `PhysButton` (world mode only — the UI mode is deleted with "Shop Canvas"); runtime-built `ShopChrome` (chip rows + exit/options) pinned by `ShopChromeAnchor`. No uGUI input in the shop — one physics pipeline, gated by `ShopInputGate` (referenced by button presses AND card-body enlarge/restore clicks). Layout contract: shelf stays below the chrome band (`ShopChrome.CheckShelfClearance` warns). Plan: `plans/plan-world-entity-shop-chrome-2026-09-18.md`.
- **Shop page panels (2026-09-18, UIKitDemo §08 port)**: runtime-built `ShopSectionPanels` draws the translucent Shop/Deck/Upgrades panels + headers + `03/05` deck counter; the reroll button lives in the Shop panel header (NOT the chrome). Owned utility passives render in the Upgrades row (`_spawnedUtilityCards`, grid base = first slot after the deck band); panel refit fires from `RelayoutPlayerDeckCards`. Plan: `plans/plan-shop-panels-port-2026-09-18.md`.
- **Shop top bar v3 (2026-09-19)**: §08 three-row column — row 0 = 离开商店 / reused combat avatar+username / reused combat HP pill / money / ❚❚ (one line); row 1 = rarity odds; row 2 = wins/hearts/income, rows 1-2 left-aligned. The avatar (`CombatIconPresenter` player side) and HP pill (`HPNumericDisplayHorizontal` player side) are the SAME combat components, re-anchored per phase from `ShopTopBarLayout` viewport constants and restored on combat entry (`PlayerIconShopScale` 0.28 — the icon's `small shadow` child is 222×306, so larger scales clip). Glyphs resolved: ♥/❚ baked into `NotoSansSymbols2 SDF`; 🜲 via the new OFL `NotoSansSymbolsAlchemical SDF` last on the chrome font's fallback chain. Open: the Shop panel header still sits inside the chrome band (cosmetic; fixing it needs a shop-pitch re-tune). Plan: `plans/plan-shop-topbar-combat-hud-reuse-2026-09-19.md`.
- Details: `docs/ShopSystems.md`; plans `plans/plan-duplicate-cards-share-deck-slot-2026-07-31.md`, `plans/plan-utility-passive-shop-pipeline-2026-08-31.md`.

## Animation System

**Two-phase**: logic phase executes effects synchronously and captures `AnimationRequest`s into `EffectRecorder`s (tree under `EffectChainManager`; traversal = effect-instance-boundary interleave); after the chain closes, `CombatManager.PlayRecorderAnimationsAndWait()` plays them via `RecorderAnimationPlayer` (singleton, auto-created in `CombatManager.Awake()`). Per-effect capture table, `AnimationRequestType` semantics, projectile/delta-commit details: `docs/AnimationSystem.md`.

Critical points:
- Effects moving cards within the deck **snapshot** `targetIndices` at capture time, before raising reactive events (e.g. `onMeBuried` → `StageSelf`).
- `RecorderAnimationPlayer.ApplyAnimationResult` advances `physicalCardsInDeck` **before** each deck-move request so reactive chains (bury → stage) display correctly.
- Source-card auto popup/slot-in is scoped **per card**, not per recorder; skipped if the source is destroyed/exiled/revealed first.
- `CombatManager.isPlayingEffectAnimations` blocks input during playback; `CombatAnimationSpeed.SpeedScale` scales Combat-phase durations only (shop stays normal speed).
- Face-down/flip: state on `CardPhysObjScript` (`isFaceUp`/`everRevealed`/`SetFaceUp`); never-cover rule — a card once shown stays face-up until exiled or shuffled. Details: `docs/FaceDownFlipSystem.md`.

## Critical Rules

- **HPAlterEffect**: Automatically adds `baseDmg.value`; set `baseDmg` to 0 when passing a specific value.
- **CardType**: `CardScript.cardType` (`EnumStorage.CardType` {None, Creature, Token}, append-only) replaced the `isCreature` bool (2026-09-02; 2026-09-14 Status identifier renamed → Token 衍生物, serialized value 2 unchanged). Creature = attack-bearing 实体 (ATK column non-empty; desc 谓词 2026-09-14 起「生物/非生物」→「实体/现象」); Token = 衍生物 tokens (信徒 RIFT + 诅咒 JU_ON) — outside every creature predicate and creature-only aura (BATTLE_HORN, RELIC_GRAVE_LORD), no special-cases anywhere. "Damaging card" predicates = `IsCreature || HasAttackAttribute` (damage capability, not type); attack face display = Creature always, else `HasAttackAttribute` or `alwaysShowAttack` flag (JU_ON). Plans: `plans/plan-card-type-status-2026-09-02.md`, `plans/plan-cardtype-shiti-xianxiang-2026-09-14.md`.
- **cardTypeID**: Used for saving / statistics / card-type filtering (not instance ID).
- **Anti-loop**: Do not attach multiple looping effect instances to the same card.
- **GameEvent.Raise**: Use `Raise()` only for non-faction-specific events. For owner/opponent events, use `RaiseOwner()` / `RaiseOpponent()` based on the trigger object's faction. Direct `Raise()` on faction events is prohibited.
- **Neutral Cards**: `isStartCard == true` cards are neutral and skipped by `ShouldSkipEffectProcessing()`.
- **CardScript Cost Fields**: removed in the 3.0 no-cost redesign (no `buryCost`/`delayCost`/`exposeCost`/`minionCost*` fields).
- **CardScript Properties**: `displayName` (falls back to GameObject name via `GetDisplayName()`), `shopRollWeightMultiplier`, `IsNeutralCard`, `CanBeAffectedByEffects`, `takeUpSpace` (`false` cards stay in DeckSO but are not instantiated in shop/combat and cannot be sold).
- **Card Face Template v1.1 (2026-09-05; desc multi-line 2026-09-08)**: zones = art (unchanged) → rarity `✦` right-aligned under the art's bottom-right → `> `-prefixed desc (multi-line, never truncated, bottom-anchored above divider) → `cardDivider` line → bottom row (name left, attack right & larger, shared baseline). Standalone tag row and status/life print are hidden (tags live in desc `<tag:X>` + hover tooltip). Glyph `✦` comes from `Assets/Fonts/NotoSansSymbols2 SDF.asset` (static atlas, self-typeface; fallback on both RobotoCondensed SDFs) — no other font has it; fullwidth punctuation (`：`、`，`) has no glyph anywhere — cardDesc must use halfwidth punctuation. Port details & TMP pitfalls: `plans/plan-card-template-v1.1-port-2026-09-05.md`.
- **Graveyard Removed**: Graveyard deprecated; legacy `CardManipulationEffect.Revive*` removed. Revive engine = `Effects/ReviveEffect` (plan in `plans/`); `StatusEffect.Revive` enum slot kept deprecated (implicit values, never renumber).
- **Input Block Reference Counting**: `BlockInput`/`UnblockInput` use reference counting; always pair them.
- **Visual Bug Comments**: When fixing a visual/presentation bug in `Effects/`, `UXPrototype/`, or `Managers/Animation*.cs`, use the `VISUAL-FIX(YYYY-MM-DD):` block format defined in `docs/VisualBugPrevention_Guide.md`. Search existing `VISUAL-FIX` comments before editing.
- **Regression Checklist**: Every visual bug fix must append or update a row in `docs/RegressionChecklist.md`. Do not delete obsolete rows; mark them `~~strikethrough~~` with `(Obsolete YYYY-MM-DD)`.
- **Deterministic RNG**: all logic-path randomness (combat/shop/setup) reads the `Rng` service (`RngChannel.Deck/Target/Shop/Setup`, `Assets/Scripts/Managers/RngService.cs`); never add `UnityEngine.Random` in logic paths (visual jitter/stagger only). Repro: `TestManager.overrideCombatSeed` / cmdline `-odseed N`; per-combat `Seed_*.txt` + `DeterminismDigest_*.txt` land in `CombatLogs/`. Docs: `docs/RngDeterminism.md`.

## Color Tags

Damage `<color=red>`, Heal `<color=#90EE90>`, Shield `<color=grey>`, Friendly `<color=#87CEEB>`, Enemy `<color=orange>`

**Single source of truth**: all colors live in `ColorSO` assets under `Assets/SORefs/Colors/`, aggregated by `GameColorPalette`. Log/rich-text: `GameColorPalette.Me.<name>.OpenTag`/`.Hex` — never hardcode hex. `CardPhysObjScript` reads palette statics at runtime (serialized ColorSO fields removed 2026-08-17); HUD components read `GameColorPalette.<Name>Color` statics — HUD colors live in the palette's "HP Bar / Numeric"/"Damage Floater" groups, Edit Mode previews live-update.

---

## Unity MCP `execute_code`

Roslyn compiler installed; `compiler: "auto"` resolves to Roslyn (C# 12+, all modern syntax works). `codedom` (C# 6) is fallback only — if forced, avoid `using` declarations, bare void `return;`, `$""` interpolation, `?.`, and `yield return` (use fully-qualified names, explicit null checks, `string.Format`, return a value on all paths, no coroutines).

If a project type is not resolved (e.g. `GameEventListener`), use `System.Type.GetType("GameEventListener, Assembly-CSharp")`.

---

## Agent Post-Mortem Notes

- Trace full flow independently; PRDs can miss branches. Watch for sentinel conditions (`return`/`else`/`continue`). After moving code, do a reachability check. Read the full method body — earlier branches may be the real path. **Glob**: Use `Assets/**/FileName.cs` instead of `**/FileName.cs`
- Pixelation shaders: toggle `PixelationEffectController.me`; canvases SSC. Plan: `plans/plan-pixelation-shader-2026-08-05.md`.
- **A dirty scene makes the Test Runner raise the "Scene(s) Have Been Modified" modal, which blocks the main thread** — the job then ends as `Test job failed to initialize (tests did not start within timeout)` or just hangs **while `mcpforunity://editor/state` still reports `ready_for_tools: true`** and `MainWindowTitle` looks unchanged, so it reads as a broken runner. After such a failure the dead job id remains as `tests.current_job_id` and blocks every later run — clear it with `run_tests` `clear_stuck: true`. SaveScene is pre-approved (see Agent Behavior), so the practical rule is to save PROACTIVELY whenever a run is about to happen; the modal itself can still only be dismissed by the user, so never click it. **Root cause found and FIXED 2026-09-19**: the Edit Mode harness used to build its scratch GameObjects in the ACTIVE scene, so a run dirtied GameScene by itself (measured with an `EditorSceneManager.sceneDirtied` counter: clean title before the run, `*` after it, no user edit in between). Both `HeadlessCombatRig` and `HeadlessCombatTestFixture` now create those objects in an `EditorSceneManager.NewPreviewScene()`; the same A/B measured 1 -> 0 dirt events, the sim results stayed byte-identical, and a full 581-test run now leaves GameScene clean. Residual dirt is still possible — a test that lets PRODUCTION code create objects in the active scene (e.g. `ResultStatsPanel.Build`) or any real editor edit — so saving before a run stays good hygiene. Unity exposes no API to clear the dirty flag (only `MarkSceneDirty`).
- **Play-mode verification trap**: `Application.runInBackground` is `false`, so the player loop FREEZES while the Unity Editor window is unfocused (the normal case during MCP-driven testing). `Update()` stops running and any polled-phase component (`gamePhaseRef.Value()` vs a `_lastPhase` field) reads STALE — it looks exactly like a "component never repositions" bug and has already caused one phantom handoff anomaly. Always assert `Time.frameCount` is climbing in the same probe, and set `UnityEngine.Application.runInBackground = true` at the start of a verification session (runtime-only; does not touch ProjectSettings). Also: use the in-session Unity MCP tools (`execute_code` / `manage_editor` / `read_console` / `run_tests`) rather than hand-rolled HTTP helpers, and remember edits made while in play mode need an exit + `refresh_unity` compile before they take effect.
