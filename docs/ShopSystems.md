# Shop Systems

(Moved from AGENTS.md on 2026-09-05 during the size diet; that file keeps a condensed summary.)

## Duplicate Slot Rule

`ShopManager.duplicateCopiesShareSlotRef` (BoolSO in `Assets/SORefs/ShopRefs/`, default OFF). ON: copies sharing a `cardTypeID` take 1 deck slot (first copy only), stack upper-left in the shop display, and only the stack base shows price (`ShopCardView.suppressPriceDisplay`); count via `UtilityFuncManagerScript.CountCardsTakingUpSpace`; empty `cardTypeID` never deduped; `CombatStartCardGiver` and enemy deck unaffected. Shop empty slots are persistent background objects (`ShopUXManager._spawnedEmptySlots`, one per deckSize grid slot) — never consumed/respawned by buy/sell; `_spawnedPlayerCards` holds real cards only; buy/sell just add/remove + `RelayoutPlayerDeckCards()`. Plan: `plans/plan-duplicate-cards-share-deck-slot-2026-07-31.md`.

## Shop Utility Passives & Board Pipeline

Plan & execution state: `plans/plan-utility-passive-shop-pipeline-2026-08-31.md` (§6).

- Metadata: `CardScript.utilityKind` (+ `utilityValue`/`utilityValue2`/`utilityRarityWeightMults`/`reservedTag`); `IsUtilityPassive` = isPassive && kind != None. Passive utility = deck-resident, occupies a slot, sellable (sell removes it); all bonuses are RECOMPUTED from the deck — no accumulating listeners.
- `UtilityShopBonus` (pure static) derives all from `playerDeckRef`: baseline growth (payday/hpMax/deckSize per session), per-kind effects, owned dedup. Sole stateful exception: deck-slot meter card — `DeckSizeIncreaseEffect` bumps run counter `DeckSlotPurchasesRef` (reset in `PhaseManager.ResetRun`), self-exiles on buy (BuyFunc skips deck add), escalating price, capped by static `maxDeckSize` (16).
- Shop HP invariant: `ApplyHpMaxFromDeck` (entry/buy/sell) sets `hp = hpMax`; shop phase is always full HP, so combat starts full (`HPMaxAlterEffect` removed 2026-09-04).
- `ShopBoardPipeline.GenerateBoard` (pure static, injected System.Random): board-type roll (`sessionUtilityBoardChances` + OddsUtility; ODDS_1 forces the visit's first board; empty utility pool → combat board) → reserved slots (boardIndex from 0, fires when `boardIndex % every == every - 1`; candidates from the CLASSIFIED pool only — combat boards never offer utility; utility drought → any-rarity fallback; combat drought → skip) → wave filters (combat generic slots, creature first) → weighted rolls (session table × shopRollWeightMultiplier × RarityWeight mults). `CurrentBoardIsUtility` drives the shop UX marker.
- Stats: result rows and `CountGraveyardCardsOf` exclude `IsUtilityPassive` (latter: no enemy GRAVE_CURSE feed); deck-population axis still counts passives; ShopStatsManager logs utility appear/buy share.
