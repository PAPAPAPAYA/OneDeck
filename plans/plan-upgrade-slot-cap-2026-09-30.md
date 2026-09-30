# Plan: Upgrade-Slot Cap (upgrade-slot cap) — 2026-09-30

Status: implemented 2026-09-30.

## Ruling (user, 2026-09-30)
Shop utility passives (Upgrades panel, "Shop Upgrades") get a deck-slot-style limit that only
applies to them:

- Independent cap: owning upgrade cards never touches the 16-slot deck (occupiesDeckSlot stays
  false); a separate dynamic cap limits how many upgrade copies can be owned.
- Each copy counts 1 (no cardTypeID dedup).
- At cap: buy silently blocked (same as the deck-full gate); the only visible feedback is the
  N/M counter on the Upgrades panel header. No shelf stop-offering, no button graying.
- Cap value mirrors the deck-size system: base + per-session baseline growth + a
  repeat-purchasable meter card, clamped to a static ceiling. Numbers mirror the deck side.
- Copy/text authored by the agent.

## System mirror (deck side -> upgrade side)

| Deck side (existing) | Upgrade side (new) |
|---|---|
| `deckSize` IntSO (valueOg 3) `PlayerDeckSizeRef` | `upgradeCap` IntSO (valueOg 3) `PlayerUpgradeCapRef` |
| `maxDeckSize` IntSO (12) `MaxDeckSizeRef` | `maxUpgradeSlots` IntSO (12) `MaxUpgradeSlotsRef` |
| `deckSlotPurchasesRef` IntSO run counter | `upgradeSlotPurchasesRef` IntSO run counter |
| `DeckSizeIncreaseEffect` (+ self-exile) | `UpgradeCapIncreaseEffect` (+ self-exile) |
| Meter card `IncreaseDeckSizeLite` Long Night | Meter card `IncreaseUpgradeCap` Tian Kan |
| `GetDeckSlotPrice(4, +2/purchase)` | `GetUpgradeSlotPrice(4, +2/purchase)` |
| `deckSizeGrowthPerStep=1 / sessionsPerDeckSizeStep=1` | `upgradeCapGrowthPerStep=1 / sessionsPerUpgradeCapStep=1` |
| `ApplyBaselineGrowth` shop-entry recompute | same method, parallel upgradeCap recompute |
| Run reset `PhaseManager` ResetToDefault | same, two extra refs |
| `deckSizeAtCeiling` pipeline stop-offering | `upgradeCapAtCeiling` (meter card only) |

## Mechanics

- Counting predicate: `CardScript.IsUtilityPassive` (disjoint from slot-occupying cards — audited
  4.0 prefabs, no passive has occupiesDeckSlot=1). Helper `UtilityFuncManagerScript.CountUpgradeCards`.
- `ShopManager.BuyFunc` gate order: index -> deck-meter ceiling -> **upgrade-meter ceiling** ->
  **upgrade cap full** (dynamic `upgradeCap.value`, not the ceiling) -> deck-full -> price.
  Meter card self-exiles (`!isDeckSlotCard && !isUpgradeCapCard`), still fires onMeBought.
- Own-once immunity: the owned-type pool exclusion collects only from deck utility passives;
  the meter card never enters the deck, so it is re-offerable forever.
- Price funnel: `GetCardPrice` single-arg gains the upgrade meter branch;
  `ShopCardView` caches both probes (display matches `GetEffectiveBuyPrice`).
- `GenerateBoard` gains `bool upgradeCapAtCeiling = false` optional param (before rng) —
  ShopBoardPipelineTests' ~45 call sites stay untouched; `ClassifyPools` skips the upgrade
  meter card at its ceiling and classifies it utility-board-only.
- Upgrades panel counter: `M = shop.upgradeCap.value` (dynamic, grows live on meter buy);
  reuses `FormatCounterUsed/Total`; refresh rides the existing `RefreshIfActive` paths.

## Assets

- `Assets/SORefs/ShopRefs/UpgradeSize/PlayerUpgradeCapRef.asset` (3, resetOnStart)
- `Assets/SORefs/ShopRefs/UpgradeSize/MaxUpgradeSlotsRef.asset` (12, resetOnStart)
- `Assets/SORefs/ShopRefs/UpgradeSize/UpgradeSlotPurchasesRef.asset` (0, resetOnStart)
- `Assets/Prefabs/Cards/4.0/0_Common/IncreaseUpgradeCap.prefab` — YAML copy of
  IncreaseDeckSizeLite with in-place component-type swap (script guid swap keeps fileIDs so
  the UnityEvent retarget is only the method name): cardTypeID `SYSTEM_INCREASE_UPGRADE_CAP`,
  displayName Tian Kan, cardDesc `购买: 升级位 <b>+1</b>, 放逐自身`, rarity Common,
  occupiesDeckSlot 0, `IncreaseUpgradeCapBy(1)`.
- Appended to `ShopPoolRef`.

## Tests

- `UpgradeSlotCountTests` (mirror DuplicateSlotCountTests): counting helper cases.
- `UpgradeCapMeterTests` (mirror DeckSlotMeterTests): effect bump/clamp/null-counter +
  formula reproduce.
- Baseline before: EditMode 664 total / 663 passed / 1 skipped.

## Known follow-ups

- Three-way consistency tool may flag the new SYSTEM_ card (Notion/server catalog has no row).
- Shop board shares: meter card enters the common/Common price band via meter price only;
  no rarity-weight tuning done.
