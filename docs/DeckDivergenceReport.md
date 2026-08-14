# Deck Divergence Report v2 (Recorded Decks 不重合度分析)

- Generated: 2026-08-13 (v2)
- Data: 70 DeckSO → dedupe 69, `Assets/SORefs/Decks/Recorded/Session0-7`
- Metric: multiset Jaccard `J = Σmin/Σmax`, divergence `D = 1 - J`; set Jaccard as control
- v2 changes: (1) two SYSTEM cards excluded from all similarity metrics; (2) exact-duplicate decks removed; (3) all pair stats exclude pairs recorded within 24h of each other; (4) rarity-weighted shop-offer baseline added; (5) full per-card selection table
- Interactive version: `docs/DeckDivergenceReport.html`

## TL;DR

1. **With system cards and same-day recordings removed, decks are statistically indistinguishable from random shop draws.** Overall meanD = 0.9427 across 1541 cross-day pairs vs **0.9424** for the rarity-weighted random baseline; median D = 1.0 (over half of cross-day deck pairs share **zero** core cards). Only 3.5% of pairs are more similar than the random baseline's most-similar 5%. At the pair level there is **no convergence** — builds are as spread out as the shop itself.
2. **The v1 "late-run convergence" was an artifact.** Within-session meanD no longer falls with progression (flat 0.92-0.95), adjacent-session meanD no longer falls (flat 0.92-0.94). Both trends were produced by the two universal system cards plus same-run re-recordings.
3. **The real funnel is the two system cards themselves.** `SYSTEM_INCREASE_DECK_SIZE` average copies per holding deck climb 1.0 (S0) → 4.0 (S6/S7); `SYSTEM_INCREASE_HP_MAX` appears from S2 and climbs to 2.2. Late decks hand 2-6 slots to these two cards regardless of build.
4. **23 of 71 selectable pool cards (32%) were never picked in 69 recordings** — full list in the card table below.

## Data & Method (v2)

- Corpus: 69 decks (1 exact-duplicate removed: `Session4_20260620_172453`, identical to `Session3_20260620_172453`).
- System cards `SYSTEM_INCREASE_DECK_SIZE` / `SYSTEM_INCREASE_HP_MAX` are stripped from every deck before all similarity metrics (separate section below).
- Pair filter: any pair with |Δt| ≤ 24h is excluded (805 of 2346 total pairs) — removes same-run lineage and same-evening re-recordings. No per-date grouping in the report.
- Baselines (Monte Carlo, seed 42, 20000 overall / 4000 per-session pairs, sizes sampled from observed core sizes of that scope):
	- Uniform: draw uniformly from the 71 selectable pool cards.
	- Rarity-weighted: draw with probability ∝ rarity weight × `shopRollWeightMultiplier`, using the live tables per `sessionRarityWeights` (`ShopManager.cs:343`): S0-1 early (C90/U9/R1), S2-3 mid (C75/U18/R7), S4+ late (C60/U30/R10).
- `belowXxx` = share of observed pairs whose D is below the given baseline's mean / 5th percentile.

## 1. Overall (1541 cross-day pairs, core cards only)

- meanD 0.9427, medianD 1, meanDset 0.9411
- Histogram (D range: pairs / share):

| Range | Pairs | Share |
|---|---|---|
| 0.0-0.1 | 0 | 0.0% |
| 0.1-0.2 | 0 | 0.0% |
| 0.2-0.3 | 1 | 0.1% |
| 0.3-0.4 | 3 | 0.2% |
| 0.4-0.5 | 0 | 0.0% |
| 0.5-0.6 | 7 | 0.5% |
| 0.6-0.7 | 26 | 1.7% |
| 0.7-0.8 | 64 | 4.2% |
| 0.8-0.9 | 308 | 20.0% |
| 0.9-1.0 | 1132 | 73.5% |

- 73.5% of pairs in 0.9-1.0; only 11 pairs (0.7%) with D ≤ 0.5.
- vs baselines: uniform mean 0.9701 (P5 0.8333); rarity-weighted mean 0.9424 (P5 0.75).
- Observed below baseline mean: uniform 0.3342, weighted 0.3342; below baseline P5: uniform 0.1084, weighted 0.035.

## 2. Within-Session (same progress point, cross-day pairs)

| Sess | Decks | Core sizes (mean) | Pairs | meanD | medD | minD | Dset | Uni base | Wgt base | belowWgtMean | belowWgtP5 | Most similar pair |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| S0 | 22 | 1-4 (2.4) | 178 | 0.9425 | 1 | 0.3333 | 0.9389 | 0.9801 | 0.9444 | 0.2079 | 0.0169 | 0_20260617_220337 ~ 0_20260621_120948 |
| S1 | 13 | 3-5 (3.5) | 51 | 0.9158 | 1 | 0.5 | 0.9158 | 0.9737 | 0.9241 | 0.3922 | 0.0784 | 1_20260618_090604 ~ 1_20260620_214318 |
| S2 | 11 | 4-6 (4.7) | 29 | 0.9477 | 1 | 0.75 | 0.9477 | 0.965 | 0.9367 | 0.3793 | 0 | 2_20260620_172257 ~ 2_20260802_215441 |
| S3 | 8 | 4-6 (5.4) | 19 | 0.9273 | 1 | 0.625 | 0.926 | 0.9588 | 0.9307 | 0.4737 | 0.0526 | 3_20260620_172453 ~ 3_20260802_220110 |
| S4 | 5 | 4-8 (6.0) | 7 | 0.9542 | 1 | 0.8462 | 0.9531 | 0.9573 | 0.9447 | 0.4286 | 0 | 4_20260621_135129 ~ 4_20260802_220343 |
| S5 | 6 | 5-8 (6.7) | 11 | 0.9325 | 0.9286 | 0.8182 | 0.9286 | 0.9512 | 0.9384 | 0.5455 | 0 | 5_20260619_224210 ~ 5_20260623_211852 |
| S6 | 3 | 7-8 (7.3) | 2 | 0.9167 | 1 | 0.8333 | 0.9167 | 0.9473 | 0.9327 | 0.5 | 0 | 6_20260620_173204 ~ 6_20260803_100803 |
| S7 | 1 | 7-7 (7.0) | 0 | - | - | - | - | - | - | - | - | - |

Median D = 1.0 in almost every session: most same-progress cross-day pairs share zero core cards. No monotonic trend across sessions.

## 3. Adjacent Sessions (one progression step, cross-day pairs)

| Pair | Pairs | meanD | Dset |
|---|---|---|---|
| S1×S0 | 200 | 0.925 | 0.9229 |
| S2×S1 | 80 | 0.9246 | 0.9246 |
| S3×S2 | 48 | 0.9447 | 0.9434 |
| S4×S3 | 24 | 0.9386 | 0.9374 |
| S5×S4 | 19 | 0.9359 | 0.9332 |
| S6×S5 | 10 | 0.9278 | 0.9254 |
| S7×S6 | 1 | 0.8333 | 0.8333 |

Flat at 0.92-0.94 (S7×S6 has 1 pair). The v1 monotonic decline is gone.

## 4. System Cards — Quantity Change per Session

| Session | Decks | DECK_SIZE: decks with (share) | copies | avg/holder | HP_MAX: decks with (share) | copies | avg/holder |
|---|---|---|---|---|---|---|---|
| S0 | 22 | 5 (23%) | 5 | 1.00 | 0 (0%) | 0 | 0 |
| S1 | 13 | 7 (54%) | 9 | 1.29 | 0 (0%) | 0 | 0 |
| S2 | 11 | 9 (82%) | 15 | 1.67 | 1 (9%) | 1 | 1.00 |
| S3 | 8 | 7 (88%) | 17 | 2.43 | 3 (38%) | 3 | 1.00 |
| S4 | 5 | 4 (80%) | 13 | 3.25 | 4 (80%) | 5 | 1.25 |
| S5 | 6 | 5 (83%) | 19 | 3.80 | 5 (83%) | 11 | 2.20 |
| S6 | 3 | 3 (100%) | 12 | 4.00 | 3 (100%) | 5 | 1.67 |
| S7 | 1 | 1 (100%) | 4 | 4.00 | 1 (100%) | 2 | 2.00 |

DECK_SIZE is present from the very first session and its average copies per holding deck climb monotonically 1.0 → 4.0; HP_MAX appears from S2 and climbs to ~2.2. Late decks devote 2-6 slots to these two cards.

## 5. Full Card Table (75 cards: 73 pool + 2 used outside pool)

| cardTypeID | Rarity | Mult | Pool | Decks | Share | Copies | Avg | Sessions |
|---|---|---|---|---|---|---|---|---|
| SYSTEM_INCREASE_DECK_SIZE | Uncommon | 1.2 | ✓ | 41 | 59.4% | 94 | 2.2927 | 0,1,2,3,4,5,6,7 |
| RIFT_INSECT | Common | 1 | ✓ | 19 | 27.5% | 20 | 1.0526 | 0,1,2,3,4,5 |
| SYSTEM_INCREASE_HP_MAX | Uncommon | 1 | ✓ | 17 | 24.6% | 27 | 1.5882 | 2,3,4,5,6,7 |
| SACRIFICE_RITUAL | Uncommon | 1 | ✓ | 16 | 23.2% | 16 | 1 | 0,1,2,3,4,5,6,7 |
| SOLDIER_SKELETON | Common | 1 | ✓ | 15 | 21.7% | 15 | 1 | 0,1,2,3,4,5 |
| RIFT_CURSE | Common | 1 | ✓ | 14 | 20.3% | 14 | 1 | 0,1,2,3,4,5 |
| UNDEAD_CURSER | Common | 1 | ✓ | 13 | 18.8% | 14 | 1.0769 | 0,1,2,3,4 |
| SACRIFICIAL_CURSE | Common | 1 | ✓ | 12 | 17.4% | 14 | 1.1667 | 0,1,2,3,4,5 |
| QUICK_RESPONSE_PROTOCOL | Uncommon | 1 | ✓ | 11 | 15.9% | 11 | 1 | 1,2,3,4,5,6,7 |
| SLIME | Rare | 1 | ✓ | 11 | 15.9% | 11 | 1 | 1,2,3,4,5,6,7 |
| CORPSE_CANON | Uncommon | 1 | ✓ | 10 | 14.5% | 10 | 1 | 1,2,3,4,5,6 |
| GRAVE_TOGETHER | Common | 1 | ✓ | 10 | 14.5% | 10 | 1 | 0,1,2,3,5,6,7 |
| POISONER | Common | 1 | ✓ | 10 | 14.5% | 11 | 1.1 | 0,1,2,3 |
| SPIKE_SKELETON | Uncommon | 1 | ✓ | 10 | 14.5% | 10 | 1 | 0,1,2,3,4,5,6 |
| FALL_INTO_RIFT | Common | 1 | ✓ | 9 | 13.0% | 9 | 1 | 0,1,2,3,4,5 |
| UNFINISHED_ROBOT | Rare | 1 | ✓ | 9 | 13.0% | 9 | 1 | 1,2,3,4,5,6 |
| GRAVE_PUNCH | Common | 1 | ✓ | 8 | 11.6% | 8 | 1 | 0,1,2,3,4,5,7 |
| THE_FOOL | Common | 1 | ✓ | 8 | 11.6% | 8 | 1 | 0,1,2 |
| SACRIFICIAL_SWORD | Common | 1 | ✓ | 7 | 10.1% | 7 | 1 | 1,2,3,4 |
| CURSE_THIRST_SHAMAN | Uncommon | 1 | ✓ | 5 | 7.2% | 5 | 1 | 0,2,3,4,5 |
| DEATHBED_CURSE | Rare | 1 | ✓ | 5 | 7.2% | 5 | 1 | 2,3,4,5 |
| LARGE_SCALE_DEATH | Rare | 1 | ✓ | 5 | 7.2% | 5 | 1 | 2,3,4,5,6 |
| UNSTABLE_PORTAL | Uncommon | 1 | ✓ | 5 | 7.2% | 5 | 1 | 5,6,7 |
| COFFIN_MAKER | Common | 1 | ✓ | 4 | 5.8% | 4 | 1 | 0,1,2 |
| CURSE_THIRST_SUMMONER_OLD | Common | 1 | ✗ (outside pool) | 4 | 5.8% | 4 | 1 | 0,1,2 |
| GOBLIN_CHARGE_TEAM | Uncommon | 1 | ✓ | 4 | 5.8% | 4 | 1 | 0,1 |
| GRAVE_KEEPER | Rare | 1 | ✓ | 4 | 5.8% | 4 | 1 | 3,5,6,7 |
| GRAVE_PORTAL | Common | 1 | ✓ | 4 | 5.8% | 4 | 1 | 1,2,3,4 |
| RIFT_DEVOURER | Rare | 1 | ✓ | 4 | 5.8% | 4 | 1 | 0,3,5,6 |
| RIFT_DRAGON | Uncommon | 1 | ✓ | 4 | 5.8% | 4 | 1 | 3,5,6 |
| SMALL_SCALE_DEATH | Uncommon | 1 | ✓ | 4 | 5.8% | 4 | 1 | 0,4,5,6 |
| AVENGER | Uncommon | 1 | ✓ | 3 | 4.3% | 3 | 1 | 2,5,6 |
| BODY_CANON | Rare | 1 | ✓ | 3 | 4.3% | 3 | 1 | 3,5,6 |
| CURSE_ENCHANTMENT | Rare | 1 | ✓ | 3 | 4.3% | 3 | 1 | 3,4,5 |
| ELDER_SORCERER | Rare | 1 | ✓ | 3 | 4.3% | 3 | 1 | 2,3,4 |
| POWER_SURGE | Uncommon | 1 | ✓ | 3 | 4.3% | 3 | 1 | 2,3,4 |
| RIFT_COFFIN | Uncommon | 1 | ✓ | 3 | 4.3% | 3 | 1 | 3,5,6 |
| TACTICAL_BREACHER | Uncommon | 1 | ✓ | 3 | 4.3% | 3 | 1 | 0,1,2 |
| ALL_FOR_ONE | Uncommon | 1 | ✓ | 2 | 2.9% | 2 | 1 | 4,5 |
| BLACKSMITH | Common | 1 | ✓ | 2 | 2.9% | 2 | 1 | 0,2 |
| CURSE_THIRST_BEAST | Uncommon | 1 | ✓ | 2 | 2.9% | 2 | 1 | 2,3 |
| CURSE_THIRST_SUMMONER | Common | 1 | ✓ | 2 | 2.9% | 2 | 1 | 0,4 |
| MOTH_MAN | Uncommon | 1 | ✓ | 2 | 2.9% | 2 | 1 | 1,2 |
| POWER_CRAVER | Uncommon | 1 | ✓ | 2 | 2.9% | 2 | 1 | 1,2 |
| WEAPON_SPIRIT | Uncommon | 1 | ✓ | 2 | 2.9% | 2 | 1 | 1,2 |
| ZOMBIE | Common | 1 | ✗ (outside pool) | 2 | 2.9% | 3 | 1.5 | 0 |
| CONFUSED_PORTALMANCER | Uncommon | 1 | ✓ | 1 | 1.5% | 1 | 1 | 2 |
| CROW_CROWD | Rare | 1 | ✓ | 1 | 1.5% | 1 | 1 | 2 |
| MARTYR | Rare | 1 | ✓ | 1 | 1.5% | 1 | 1 | 6 |
| RIFT_SUMMONER | Uncommon | 1 | ✓ | 1 | 1.5% | 1 | 1 | 2 |
| SCAPEGOAT | Uncommon | 1 | ✓ | 1 | 1.5% | 1 | 1 | 6 |
| SIDE_EFFECT_PORTAL | Common | 1 | ✓ | 1 | 1.5% | 1 | 1 | 0 |
| ADVANCE_PORTAL | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| ALMIGHTY | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| ANTI_CREATURE_WEAPON | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| BONE_COMBINATION | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| BOOSTER | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| CURSED_CORPSE | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| CURSED_SKELETON | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| CURSE_THIRST_ARCH_SUMMONER | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| DETERIORATION | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| DR_MANHATTAN | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| ETERNAL_GHOST | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| FLESH_COMBINATION | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| GOBLIN_ASSASSIN_TEAM | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| GRAVE_INVITATION | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| MAD_SCIENTIST | Common | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| POWER_SIPHONER | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| POWER_TRANSFER | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| PREMATURE | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| PROLIFERATING_CURSE | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| RIFT_GUIDE | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| RIFT_MONSTER | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| SNATCHER | Uncommon | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |
| WISE_BURIAL | Rare | 1 | ✓ | 0 | 0.0% | 0 | 0 |  |

### Never Selected (23 of 71 selectable, 32%)

ADVANCE_PORTAL, ALMIGHTY, ANTI_CREATURE_WEAPON, BONE_COMBINATION, BOOSTER, CURSED_CORPSE, CURSED_SKELETON, CURSE_THIRST_ARCH_SUMMONER, DETERIORATION, DR_MANHATTAN, ETERNAL_GHOST, FLESH_COMBINATION, GOBLIN_ASSASSIN_TEAM, GRAVE_INVITATION, MAD_SCIENTIST, POWER_SIPHONER, POWER_TRANSFER, PREMATURE, PROLIFERATING_CURSE, RIFT_GUIDE, RIFT_MONSTER, SNATCHER, WISE_BURIAL

Used but not in current shop pool: CURSE_THIRST_SUMMONER_OLD, ZOMBIE

## 6. Top-8 Most Similar Pairs (cross-day, core cards)

| D | Deck A (session, core size) | Deck B (session, core size) |
|---|---|---|
| 0.25 | S1 RecordedDeck_Session1_20260617_221111 (3) | S2 RecordedDeck_Session2_20260620_213426 (4) |
| 0.3333 | S0 RecordedDeck_Session0_20260617_220337 (2) | S0 RecordedDeck_Session0_20260621_120948 (3) |
| 0.3333 | S0 RecordedDeck_Session0_20260618_083500 (2) | S1 RecordedDeck_Session1_20260619_170312 (3) |
| 0.3333 | S0 RecordedDeck_Session0_20260618_095053 (2) | S0 RecordedDeck_Session0_20260621_134439 (3) |
| 0.5 | S0 RecordedDeck_Session0_20260617_220337 (2) | S2 RecordedDeck_Session2_20260620_213426 (4) |
| 0.5 | S0 RecordedDeck_Session0_20260619_154054 (2) | S1 RecordedDeck_Session1_20260620_222140 (4) |
| 0.5 | S0 RecordedDeck_Session0_20260619_220115 (3) | S1 RecordedDeck_Session1_20260618_090604 (3) |
| 0.5 | S0 RecordedDeck_Session0_20260621_120948 (3) | S1 RecordedDeck_Session1_20260617_221111 (3) |

## Interpretation

1. **The hypothesis "decks are too similar / experience not differentiated" is rejected for core cards.** After removing the two auto-pick system cards and same-day lineage pairs, cross-day decks share almost nothing (median D = 1.0) and the pair distribution matches rarity-weighted random draws almost exactly. The pool, not the player, is the binding constraint.
2. **v1's convergence curves were decomposition artifacts:** the monotone decline of within-session and adjacent-session divergence disappeared once (a) both system cards were stripped and (b) ≤24h pairs were removed. What v1 measured as "late-run convergence" was mostly every deck stacking the same 2 system cards.
3. **If anything needs design attention, it is the system cards** — DECK_SIZE at 1.0→4.0 avg copies per holder is a pure power-per-slot decision, and 6 of 13 slots in late decks can be system cards. That is the one real homogenizer left.
4. **A third of selectable cards never saw play.** Whether that means unviable cards or undiscovered strategies depends on win-rate data (`CardWinRateTracker`) — worth a follow-up cross-reference.

## Caveats

- Single player, 69 decks: measures one person's run-to-run variety, not a multi-player meta.
- Recordings span 6 weeks with balance changes; versions are not segmented (per your instruction, no date grouping).
- Weighted baseline draws each card independently with replacement; real offers are 3-card rolls without replacement plus rerolls. It is the right null model for availability, not a full shop simulation.
- S6 (3 decks) and S7 (1 deck) samples are tiny; S7×S6 adjacent = 1 pair.
- ≤24h exclusion also drops same-evening pairs of genuinely different runs (slightly conservative).

## Design Recommendations

1. **The differentiation problem is solved for regular cards — do not chase further card-pool dilution.** The binding constraint is availability: observed divergence equals the weighted-random ceiling, so builds cannot get more varied without changing offer mechanics, not card design.
2. **Address the system-card funnel:** cap or re-price DECK_SIZE/HP_MAX stacking (e.g. diminishing returns per copy), or make them build-dependent rather than universal.
3. **The 23 never-picked cards are the actual headroom.** Cross-reference them with `CardWinRateTracker` to separate "never offered enough" from "offered but bad"; fix the latter first.

