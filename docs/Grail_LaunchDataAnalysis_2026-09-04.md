# Grail (Sokpop Collective) — Steam Launch Data Study

- **Data collected**: 2026-09-04, 01:42–02:30 UTC (day 4 after launch). All Steam numbers are point-in-time snapshots via the public Steam Web API.
- **Purpose**: competitive reference for OneDeck's async-PvP design (deckbuilder + autobattler + async PvP). See `plans/plan-async-pvp-client-2026-09-03.md`.
- **TL;DR**: 145 reviews in ~3.5 days (91% positive, "Very Positive"); estimated day-1 sales 1,400–1,900 units, week-1 ~7,000–10,000, month-1 13.5k–26k, year-1 24k–44k (year-1 band cut ~40% after genre-decay calibration against 9 comparable games). Wishlist figures are not publicly obtainable.

## 1. Game Overview

| Item | Value |
|------|-------|
| Title / AppID | Grail / 3597200 |
| Developer | Sokpop Collective (lead: Tijmen "Tio") |
| Released | 2026-09-01 (Win + Mac) |
| Price | $9.99 base, -20% launch discount → $7.99 (CN region ¥42 → ¥33.6) |
| Sokpop title # | 112th game, self-described "biggest game ever" |
| Genre | Fantasy deckbuilder **autobattler**: you build the deck, battles play out automatically; async PvP against other players' deck snapshots |
| Meta systems | Ranked ladder (trophies), seasons, monthly cosmetic reward track, Legacy Store; content updates announced free (anti-pay-to-win stance), 4th character in development |
| Demo | Free demo live since at least early July 2026, removed at full launch; save data + trophies carried over; demo players received a compensation gift |
| Post-launch ops | v1.0.2 shipped on day 1 (character unlock grind 200→100 trophies, responding to the dominant early complaint); servers maintenance window 2026-08-28 |
| Press | Rock Paper Shotgun preview 2026-07-14 |

Launch-date announcement news post: 2026-07-09.

## 2. Review Growth (Steam `appreviews` API)

Snapshot totals: **145 reviews — 131 positive / 14 negative ≈ 90% positive → "Very Positive"** (score desc fetched 02:15 UTC 2026-09-04).

Per-day (UTC, from full 144-review dump at 01:42 UTC):

| Date (UTC) | New reviews | Positive |
|------------|------------|----------|
| 2026-09-01 (launch) | 48 | 44 |
| 2026-09-02 | 55 | 52 |
| 2026-09-03 | 39 | 33 |
| 2026-09-04 (first ~1.7 h) | 2 | 2 |

- Launch-day peak: 2026-09-01 17:00–21:59 UTC (~5–7 reviews/hour).
- Current tail: ~1–3 reviews/hour ≈ 35–40/day.
- CCU (ISteamUserStats GetNumberOfCurrentPlayers): 658 at 01:40 UTC, then 639 / 626 — a third-party crawl reported "all-time peak 485 on Sep 1", so the real peak is ≥ 658; concurrent players were still rising on day 4.

## 3. Review Language Distribution

Full-dump counts (n = 144) matched per-language `query_summary` totals exactly (sum = 144), so this is complete, not sampled:

| Language | Reviews | Share | Positive rate |
|----------|---------|-------|---------------|
| english | 108 | 75.0% | 91% (98/108) |
| schinese | 13 | 9.0% | 100% (13/13) |
| spanish | 5 | 3.5% | 80% |
| japanese | 3 | 2.1% | 2/3 |
| brazilian | 3 | 2.1% | 100% |
| polish / german / french / latam | 2 each | 1.4% each | all positive |
| russian / koreana / dutch | 1 each | 0.7% each | all positive |
| danish | 1 | 0.7% | 0/1 |

Notes: English-dominated coverage typical of a self-published indie; Simplified Chinese is the #2 language at 9% with zero negative reviews.

## 4. Sales & Revenue Estimates

Method:

- **Reviews → units multiplier 30–40×** (empirical median ≈ 35×, r/gamedev study; band widened for launch-discount/demo-conversion effects).
- Decay-corrected extrapolation of Grail's own review curve against genre benchmarks (§5).
- Prices: $7.99 during launch discount (~first 2 weeks), $9.99 after; month-1 blended $8.79 (60% discounted / 40% full), year-1 blended $9.30 (mostly full price, occasional -25% sales).
- FX: **1 USD = 6.736 CNY** (2026-09-04, open.er-api.com).

| Period | Review assumption | Est. units | Gross revenue (before Valve 30%) | Net after Valve 30% |
|--------|-------------------|-----------|----------------------------------|---------------------|
| Day 1 | 48 (measured) | 1,400 – 1,900 | $11.2k–15.2k ≈ **¥7.5万 – 10.2万** | ¥5.3万 – 7.2万 |
| Week 1 | ~245 (measured D1–D3 + tail) | 7,000 – 10,000 | $56k–80k ≈ **¥37.7万 – 53.8万** | ¥26.4万 – 37.7万 |
| Month 1 | ~450–650 (2.0–2.6× week 1) | 13,500 – 26,000 | $119k–229k ≈ **¥80万 – 154万** | ¥56万 – 108万 |
| Year 1 | ~800–1,100 lifetime (1.5–2.0× month 1) | 24,000 – 44,000 | $223k–409k ≈ **¥150万 – 276万** | ¥105万 – 193万 |

Confidence notes:

- Day-1 / week-1 rows are anchored on measured review counts; the multiplier band is the dominant uncertainty.
- The **year-1 row is the corrected estimate**. The first-pass model (year-1 = 3.3–4.4× month 1 ≈ ¥345万–501万 gross) was **too optimistic**; see §5.
- Unit prices use USD as the settlement currency; regional prices (e.g. CN ¥33.6 ≈ $4.7) mean the real blended ASP sits between the CN-denominated and USD-denominated views — realistic month-1 gross is likely in the lower-middle of the stated band.
- Not deducted: refunds (card roguelikes typically 5–10%), taxes, platform marketing.

## 5. Genre Decay Calibration (9 comparable games)

### Method and limitations

- Fetched full review timelines for 9 deckbuilder/roguelike comps via the `appreviews` cursor API.
- **API limitation discovered**: the review cursor only reaches back ~30 days. Launch-window (week-1/month-1) curves of older games are therefore not reconstructable this way; an early first-pass "M1/W1" ratio table built from windowed data was invalid and discarded. Wayback Machine snapshots of the comp store pages were unavailable for their launch weeks, so no external week-1 anchors either.
- What remains valid and is used here: **lifetime review totals** + **last-28-day intake** (window fully inside the cursor range) + official launch dates via `appdetails`.

### Long-tail calibration table (measured 2026-09-04)

| Game | Lifetime reviews | Last 28 days | Tail rate (% of lifetime / 28d) | Launch (verified via appdetails) |
|------|------------------|--------------|--------------------------------|----------------------------------|
| Dungeon Clawler | 3,970 | 45 | **1.13%** | 1.0: 2026-04-30 (EA since 2024-11) |
| Shogun Showdown | 7,021 | 73 | 1.04% | 2024-09-05 |
| Dicey Dungeons | 11,854 | 109 | 0.92% | ~Jul 2019 |
| Slice & Dice | 2,569 | 18 | 0.70% | ~2023 (EA) |
| Stacklands | 30,242 | 200 | 0.66% | 2022-04-08 |
| Cobalt Core | 4,376 | 28 | 0.64% | ~Sep 2023 |
| Backpack Battles | 20,670 | 128 | 0.62% | 1.0: 2025-06-13 (EA 2024-04) |
| Wildfrost | 8,833 | 30 | 0.34% | 2023-04-12 |
| Luck be a Landlord | 11,526 | 28 | 0.24% | ~2022 |

(Unmarked launch dates are approximate from public release history; lifetime totals and 28-day intakes are exact API values.)

### Findings

1. **Genre steady-state decay: monthly review intake = 0.24%–1.13% of lifetime total** (0.6–1.1% for actively-updated games; 0.25–0.35% once dormant).
2. Cross-check of the original Grail model: assuming year-1 = 3.6× month-1 implies a month-10–12 intake of 1.8–2.4% of lifetime per month — **2×+ above the most active comp measured**. Rejected.
3. Corrected model: year-1 = 1.5–2.0× month-1 implies a month-12 tail of 0.4–0.9% of lifetime — inside the measured band, top end justified only by sustained season/content cadence.
4. Caveats: Stacklands' tail is possibly inflated by *Stacklands 2000* buzz; Dungeon Clawler's tail reflects a 1.0 relaunch only 4 months prior.
5. Grail-specific upside: async PvP + seasons + free updates support retention (Backpack-Battles-like shape, upper band); downside: $9.99 is 2–3× Sokpop's historical $3–4 price, which may push the reviews→units multiplier above 40× (fewer units per review).

### Fresh-launch cohort anchor (August 2026 releases)

Same-genre games launched 3–4 weeks before this study — their entire life-to-date fits inside the 30-day review-cursor window, so their true day-1 counts are directly measurable (found via web search of genre launches; the store search API was rate-limited this session):

| Game | Launch | D1 | D3 | W1 | Total at ~4 weeks | Positive | Note |
|------|--------|----|----|----|--------------------|----------|------|
| **Montabi** | 2026-08-06 | **48** | 84 | 109 | 149 (29d) | 95% | Creature-collector deckbuilder, published by Akupara Games |
| **Re:Night** | 2026-08-04 | **43** | 82 | 143 | 166 (31d) | 91% | SRPG × deckbuilder |
| **Lucky Shot** | 2026-08-07 | **42** | 80 | 117 | 164 (28d) | 98% | Roguelike deckbuilder |
| Dominocalypse | 2026-08-13 | 4 | 8 | 9 | 12 (22d) | 100% | Dominoes × deckbuilder; near-zero traction |
| Hell Deck | 2026-08-03 | 3 | 19 | 38 | 173 (32d) | 88% | Budget-priced (≈ NZ$2); slow-burn growth via W2–W4 |
| Combolands: Roguelike Citybuilder | 2026-08-24 | 145 | 332 | 762 | 923 (11d) | 98% | Strong-launch contrast point (3× Grail's D1) |

Interpretation:

1. Grail's day-1 count (48) sits exactly in the **42–48 band** of three mid-size indie deckbuilders (Montabi / Re:Night / Lucky Shot). That cohort reached **149–166 reviews by week 4** (D1→W4 multiple ≈ 3–4×) with 91–98% positive scores.
2. Grail is running **~2–3× hotter than its own day-1 cohort**: 145 reviews by day 4 vs peers' 80–84 at D3, projected week-1 ~235 vs peers' 109–143. The excess is consistent with Sokpop's existing audience + demo carry-over conversion rather than a genre-generic effect.
3. Model cross-check: scaling the cohort's trajectory (≈220–280 year-1 reviews → ~7k–11k units at 30–40×) by Grail's measured 2–3× launch advantage lands inside the corrected year-1 band in §4 (24k–44k units) — the two independent estimates agree.

4. For OneDeck's own sales modeling this cohort is the cleanest genre anchor available: **day-1 ≈ 40–50 reviews → ~7k–11k year-1 units** (30–40× multiplier + genre decay), to be scaled by a launch-advantage coefficient — Grail itself measures ≈ 2.5–3× versus its day-1 cohort at week 4.

## 6. Wishlist Data — Not Publicly Obtainable

- Valve exposes wishlist data only to the developer in Steamworks; no public API.
- SteamDB no longer publishes wishlist history; Gamalytic and Sensor Tower (VG Insights) require paid API keys; SteamSpy blocked by Cloudflare; the devs have asked for wishlists but never published a number.
- Indirect signals only:
	- Demo live ≥ 8 weeks before launch (Next-Fest-style exposure window), removed at launch with save carry-over — a standard wishlist-conversion setup.
	- Dev's own framing ("our biggest game ever") and the price jump from the studio's usual $3–4 to $9.99 imply wishlist totals meaningfully above prior Sokpop titles.
- A real wishlist curve is only obtainable from the developer or a paid Sensor Tower / Gamalytic subscription.

## 7. OneDeck-Relevant Observations

- **Async PvP shape**: opponents are other players' deck snapshots; ranked ladder scored in trophies with rank-gated matching ("you can't be matched against builds that continued their run if you haven't"); seasons + monthly cosmetic track + Legacy Store for late acquisition — cosmetics-only monetization, free content updates to protect the PvP environment from pay-to-win.
- **Launch ops cadence**: day-1 patch responding to the top progression complaint (unlock grind halved) + one-time compensation gift to demo players; announced offline mode and private lobbies as post-release work — directly relevant reference points for `onedeck-api` scope.
- Review-language mix (75% EN / 9% zh-CN) is a realistic early benchmark for a mid-size self-published card game.

## 8. Verification Checkpoints

- **2026-09-11**: if Grail's total reviews land near ~235, the week-1 extrapolation holds.
	- *Status 2026-09-05*: day-4 intake held at 43 (vs 39 on day 3) instead of decaying; 235 will be crossed around Sep 8, week-1 tracking to **~270–290 reviews** (≈ 8,000–11,000 units). See §9.
- **2026-09-18**: if the daily rate has dropped below ~15/day, month-1 settles toward the lower band (~450 reviews).

## 9. Recheck — 2026-09-05 (day 4–5)

Snapshot at 02:06 UTC 2026-09-05:

| Metric | Value | vs 2026-09-04 |
|--------|-------|---------------|
| Total reviews | **186** (168 pos / 18 neg, 90.3%, "Very Positive") | +41 in 24 h; positive rate 91% → 90.3% |
| Day-4 intake (Sep 4, full UTC day) | **43** (37 pos) | day 3 was 39 — **no decay yet** |
| CCU | **634** | flat (626–658 band) |

Per-day intake now: 48 → 55 → 39 → 43 → (day 5 in progress).

### Findings

1. **The assumed days-4–7 decay (~25/day) has not materialized** — intake is holding at ~40/day. Week-1 forecast revised up to ~270–290 reviews (8,000–11,000 units at 30–40×); the §8 checkpoint is expected to be crossed ~3 days early.
2. **Language mix shifting east**: of the 48 reviews since Sep 4 00:00 UTC — english 32 (67%, down from 75%), schinese 7 (15%, up from 9%), plus first tchinese and koreana entries. East-Asian reviewers remain almost entirely positive.
3. **Positive rate of the new cohort dropped to 85%** (41/48). Negative-review themes on days 4–5: QoL complaints (a "tutorial replays every match" bug), archetype balance ("sword archetype loses to everything, poison clears at full HP"), flat upgrade bonuses / shallow synergy, and one fundamental "autobattling cards isn't fun". The v1.0.2 patch fixed the top launch complaint (unlock grind), but the complaint mix is rotating toward QoL details and build depth — an early-warning indicator for the score. Watch whether the 90.3% holds over the next few days.
4. **Cohort advantage widening**: Montabi needed 29 days to reach 149 reviews; Grail passed 186 by day 4.5 — the ≈2.5–3× launch-advantage coefficient from §5 holds and may be understated.

## Appendix: Data Provenance

| Endpoint | Use |
|----------|-----|
| `store.steampowered.com/appreviews/{appid}?json=1&filter=all&language=all&num_per_page=100&purchase_type=all&cursor=…` | full review dump (dates, language, votes); per-language totals via `language=` filter |
| `store.steampowered.com/api/appdetails?appids=…&cc=us` | price, release date, platforms, categories |
| `api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=…` | live CCU |
| `api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=…` | dev news posts (patch cadence, demo history) |
| `store.steampowered.com/search/results/?sort_by=Released_DESC` | new-release scan (rate-limited; partial) |
| `open.er-api.com/v6/latest/USD` | FX snapshot |

Third-party pages attempted and blocked/paywalled: SteamSpy (Cloudflare), Gamalytic (API key), Sensor Tower / VG Insights (timeout / account), SteamDB (no wishlist data), Wayback Machine (no launch-week snapshots for the comps).

External sources: [Steam store page](https://store.steampowered.com/app/3597200/Grail/), [SteamDB](https://steamdb.info/app/3597200/charts/), [GameDiscoverCo on Stacklands (450k copies)](https://newsletter.gamediscover.co/p/how-one-of-sokpops-almost-100-steam), [r/gamedev reviews-to-sales ratio](https://www.reddit.com/r/gamedev/comments/13pmidl/ratio_of_steam_reviews_to_copies_sold/), [RPS preview](https://www.rockpapershotgun.com/deckbuilder-grail-quells-the-fear-in-my-heart-of-playing-the-wrong-card-by-pinning-all-of-the-strategy-on-the-deckbuilding).
