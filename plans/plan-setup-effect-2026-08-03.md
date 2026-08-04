# Plan: Setup Effect (准备)

Date: 2026-08-03

## Goal

New effect type **Setup**: moves friendly cards to `startCardIndex + 1` in `combinedDeckZone`
(the slot directly above the Start Card, revealed just before it). First iteration implements
one method: setup N random friendly cards.

## Confirmed Decisions

- Target position: `startCardIndex + 1` exactly. Buried cards (index < startCardIndex) still
  reveal later — that is accepted, not a bug.
- Display name "准备" via a **new Tag `Setup`** in `TagTooltipDatabaseSO` (card-face visible).
- No new game events (`onMeSetup` / `onAnyCardSetup`) in this iteration.
- No new per-card stats (`CombatPerCardStatsTracker.RecordSetup`) in this iteration.

## Ground Truth (from code exploration)

- `combinedDeckZone[^1]` = deck top = revealed first; index 0 = bottom = revealed last.
- Start Card is created in `CombatManager.GatherDecks()` and shuffled in at round start; there
  is no cached index. Existing lookup patterns:
  - `BuryEffect.GetStartCardIndex()` (`Assets/Scripts/Effects/BuryEffect.cs:69-79`) — linear
    scan for `cardScript.isStartCard`, returns -1 if absent. Copy this verbatim.
  - `CombatManager.FindStartCardInstance()` (`CombatManager.cs:456-473`) — also checks
    `revealZone`.
- `CheckCost_IndexBeforeStartCard()` (`CostNEffectContainer.cs:254-293`) confirms the index
  math: lower index = closer to bottom = revealed later.
- Recorder-path rules (VISUAL-FIX lessons from Stage/Bury): logic phase mutates
  `combinedDeckZone` ONLY; never call `SyncPhysicalCardsWithCombinedDeck` in the logic phase;
  snapshot `targetIndices` AFTER the logical move; physical reorder happens in
  `CombatUXManager.ApplyAnimationResult` during playback.
- `AnimationRequestType.MoveToIndex` already exists end-to-end:
  - capture example: `CardManipulationEffect.ExecuteDelay` (`CardManipulationEffect.cs:110-125`)
    — one request per card, `targetCard` + `targetIndex`.
  - playback: `RecorderAnimationPlayer` (:959-968) → `visuals.MoveCardToIndex(...)`.
  - apply: `CombatUXManager.ApplyAnimationResult` case `MoveToIndex` (:1748-1760) — Remove +
    Clamp + Insert on `physicalCardsInDeck`.
- Friendly-selection idiom (`StatusEffectGiverEffect.CollectFriendlyCards`, :85-118):
  skip `ShouldSkipEffectProcessing` (neutral/start), keep
  `cardScript.myStatusRef == myCardScript.myStatusRef`, exclude self.
- Chinese log text is hardcoded per effect (Stage `置顶` at `StageEffect.cs:360`, Bury
  `埋入牌库底端` at `BuryEffect.cs:358-359`); colors via `GameColorPalette.Me`.

## Implementation

### 1. New file `Assets/Scripts/Effects/SetupEffect.cs`

`public class SetupEffect : EffectScript`, mirroring StageEffect/BuryEffect structure.

Public UnityEvent-bindable method:

```csharp
public void SetupRandomFriendly(int amount)
```

Selection (mirror `StageMyCards` at `StageEffect.cs:94-113`):

- Copy deck: `UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, candidates, true)`.
- Reverse-loop filter:
  - `CombatManager.ShouldSkipEffectProcessing(cardScript)` (neutral / Start Card)
  - `cardScript.myStatusRef != myCardScript.myStatusRef` (friendly only)
  - `card == myCard` (exclude self)
  - `cardScript.isMinion` — excluded, matching `StageMyCards` (flip this if minions should
    be setup-able)
  - card already at `startCardIndex + 1` — excluded (no-op move)
- `UtilityFuncManagerScript.ShuffleList(candidates)`, take `Mathf.Min(amount, count)`.

Core `SetupChosenCards(List<GameObject> chosenCards)`:

1. If `chosenCards.Count == 0` → log nothing-to-setup and return (same as Stage/Bury guards).
2. Logical move, per card:
   ```csharp
   _combinedDeck.Remove(targetCard);
   int startCardIndex = GetStartCardIndex();          // recompute per card
   int insertIndex = (startCardIndex >= 0 ? startCardIndex : 0) + 1;
   insertIndex = Mathf.Min(insertIndex, _combinedDeck.Count);
   _combinedDeck.Insert(insertIndex, targetCard);
   ```
   Note: re-inserting at the same anchor stacks each later card BELOW the previous one
   (closer to the Start Card = revealed later). First-picked card reveals first among the
   group. Fallback when Start Card is not in the deck (e.g. currently in `revealZone`):
   insert at index 1 (just above the bottom) and `Debug.LogWarning` once.
3. Snapshot final indices (after ALL moves, before any event raising):
   ```csharp
   var setupTargetIndices = new List<int>();
   foreach (var card in chosenCards)
   {
       int idx = _combinedDeck.IndexOf(card);
       setupTargetIndices.Add(idx >= 0 ? idx : _combinedDeck.Count - 1);
   }
   ```
4. Capture animation (recorder path only, same as Stage — no legacy fallback):
   - `PopUpBatch` on all chosen cards (visibility, mirrors Bury).
   - One `MoveToIndex` request per card, in the SAME order as `chosenCards`:
     ```csharp
     recorder.animationRequests.Add(new AnimationRequest
     {
         type = AnimationRequestType.MoveToIndex,
         targetCard = card,
         targetIndex = setupTargetIndices[i],
         duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
         useArc = true
     });
     ```
     Per-card sequential playback is acceptable for v1 (only existing pattern,
     `ExecuteDelay`, does the same). A dedicated `MoveToIndexBatch` is future work only if
     parallel motion is wanted.
5. `CombatLog` AppendLog with hardcoded Chinese text, e.g.
   `准备：将 X 张友方牌移至起始牌之后`, friendly color via `GameColorPalette.Me`.

Do NOT: raise `onMeStaged`/any event, call `SyncPhysicalCardsWithCombinedDeck`, touch
`ValueTrackerManager`, or add stats hooks (all explicitly out of scope for v1).

### 2. Tag `Setup` with display name 准备

- `Assets/Scripts/Managers/EnumStorage.cs`: add `Setup` to `enum Tag` (append at the end —
  do NOT reorder existing values; serialized prefabs store the int).
- New StringSO assets (both `reset = false`):
  - `Assets/SORefs/Strings/TagNames/TagName_Setup.asset` → value `准备`
  - `Assets/SORefs/Strings/TagTooltips/TagTooltip_Setup.asset` → tooltip text (e.g.
    `准备：被移至起始牌之后，最后阶段才揭晓`)
- Register both in `Assets/Resources/TagTooltipDatabase.asset` (new entry mapping
  `Tag.Setup` → the two StringSOs). Then `<tag:Setup>` in cardDesc and
  `CardTagTooltip` resolve automatically via `TagTooltipDatabaseSO.GetTagDisplayName`.

### 3. Card Binding (per card prefab)

`GameEventListener` (e.g. `onMeRevealed`) → `CostNEffectContainer.InvokeEffectEventVoid` →
`effectEvent` → `SetupEffect.SetupRandomFriendly(int amount)`. Parameters are UnityEvent
static-int bindings, same as `StageMyCards(int)`.

## Edge Cases

- Start Card in `revealZone` (round start before it returns to deck): fallback insert at
  index 1 + warning log.
- `amount` > eligible friendly count: setup all eligible (ShuffleList + Min).
- Source card is the only friendly card: empty selection, early return.
- Setup effect triggered by a card that is itself in the reveal zone: `myCard` is not in
  `_combinedDeck`, self-exclusion is a no-op — harmless.
- Cards already sitting at `startCardIndex + 1`: filtered out so repeated triggers don't
  churn the same card and waste animations.

## Verification

- Manual: bind on a test card, run combat with `DeckTester`, confirm target lands directly
  above the Start Card and reveals just before it; confirm PopUp + arc MoveToIndex playback.
- Optional: Strategy B Play Mode test via the `unity-card-playmode-test` skill.
- EditMode unit test is low value here (logic is deck-list manipulation on live singletons);
  skip unless requested.

## Out of Scope (v1)

- `onMeSetup` / `onAnyCardSetup` events.
- `CombatPerCardStatsTracker.RecordSetup` / result-panel column.
- `MoveToIndexBatch` parallel animation type.
- Other selection variants (setup self, setup by tag, setup their cards) — trivial to add
  later by mirroring the Stage/Bury method family.
