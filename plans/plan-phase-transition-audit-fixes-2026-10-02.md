# Phase Transition Audit Fixes (Shop ↔ Combat) — Audit Report + Fix Plan

- **Date**: 2026-10-02
- **Status**: audit complete; fixes designed, NOT implemented (each step waits for 「修改代码」 + step-gate confirmation)
- **Audit scope**: `PhaseTransitionDriver` / `PhaseFlightPlanner` / `PhaseManager` enter-exit pipeline / scene UnityEvent wiring (`GameScene.unity`) / `ShopChrome` / `ShopSectionPanels` / `ShopUXManager` / `ShopCardView` / `ShopHudBinder` / `PhysButton` / `CombatIconPresenter` / `HPNumericDisplayHorizontal` / `CombatManager` enter-exit / `PhaseTransitionConfig.asset` / `docs/PhaseTransition.md` declared contracts
- **Relation to prior work**: supplements `plans/plan-phase-transition-world-camera-2026-09-21.md` (§ Arbitration) and `docs/PhaseTransition.md`; interacts with the 10-01/10-02 shop interaction commits (`7b1b64f2` hover lift, `1007baf3` enlarge arbitration, `c6ae434f` enlarge modal) which did not exist when the transition shipped

## 1. Verdict

Core orchestration is **sound**: bypass symmetry (headless/seed), the deck-card borrow contract (`OwnsDeckCards` → `ReleasePlayerDeckCardsToDriver` → clear-on-next-entry self-heal), the landing guard (`combinedDeckZone` vs `physicalCardsInDeck` counts are consistent — `InstantiateDeckSide` skips non-physical cards, `CombatManager.cs:389`), the reveal state machine auto-advancing after unblock, and the input gating of every world-space interaction path (PhysButton ×3, card hover/enlarge/buy/sell, camera scroll/reset).

Five findings: one real logic hole (F1), two medium defects (F2, F3), two low/cosmetic (F4, F5). F1–F3 have concrete fix designs; F4 is latent hardening; F5 is a decision only.

## 2. Findings

### F1 — HIGH: second Space during Result→Shop travel double-enters combat mid-flight

**Mechanism.** `RequestShopToCombat` / `RequestResultToShop` return `false` while `IsTransitioning` (`PhaseTransitionDriver.cs:176, 186`), and every call site treats `false` as "run the legacy hard cut" (`PhaseManager.cs:136-138`, `ShopHudBinder.cs:119-121`, `PhaseManager.cs:249-250`). The Result→Shop travel flips the phase to Shop **at travel start** (`AdvanceFromResultToShop` runs synchronously inside the coroutine before its first yield, `PhaseTransitionDriver.cs:267`), so for the whole ~0.85 s travel the phase is Shop and the Space shortcut is live again:

	PhaseManager.Update, Shop branch:
	  RequestShopToCombat → false (busy) → ExitingShopPhase(); EnteringCombatPhase();

**Consequence chain** (all verified against code, none speculative):

1. `ClearSpawnedCards` destroys the shop content that was spawned moments earlier at travel start — `OwnsDeckCards` is only set by `ShopToCombatRoutine`, so the Result→Shop variant has no protection (`ShopUXManager.cs:275, 291` only guard the Shop→Combat direction). Empty shop flashes mid-flight.
2. The re-entered `EnteringCombatPhase` → `CombatManager.EnterCombat` → `ResetInputBlock` (`CombatManager.cs:236`) force-clears the driver's `BlockInput(this)`, so `RevealCards` starts running while the camera is still flying down.
3. Landing: coroutine unblocks (no-op), `_transitioning = false`, `ShopChrome.ShowIfActive()` re-shows the chrome, then `Update`'s snap block (`PhaseTransitionDriver.cs:137-145`) hard-cuts the camera back up to the combat page. Player experience: mashing Space at the Result screen **skips the shop entirely** with a visual glitch sandwich.
4. Bookkeeping: `RunRecorder.CloseShopVisit` closes a zero-duration visit (`PhaseManager.cs:588`); `DeckSaver.PopulateEnemyDeckBySessionNumber` + `SavePlayerDeckSnapshot` re-run (scene wiring `GameScene.unity:11379-11384`).

**Reachability per trigger path**:

| Trigger | During Result→Shop travel | Verdict |
|---|---|---|
| 离开商店 button | chrome hidden + `PhysButton` gated by `ShopInputGate` (`PhysButton.cs:250/260/289`), gate blocked | safe |
| Mouse click | Result branch requires phase == Result; phase already Shop | safe |
| **Space (real player)** | Shop branch fires, request busy → legacy fallback runs | **BUG** |
| **`DeckTester.autoSpace`** | fires every frame → guaranteed every cycle | **BUG (dev)** |

The Shop→Combat direction is safe (phase is Combat, `combatFinished=false` blocks Space, `PhaseManager.cs:142`).

**Root cause**: the driver's `false` return overloads "driver unavailable → legacy is correct" with "driver busy → legacy would be a bug"; call sites cannot distinguish the two.

### F2 — MEDIUM: Space bypasses the enlarge-preview modal (10-01 `c6ae434f`)

The modal holds `ShopInputGate` (`ShopCardView.cs:734`), which kills every button and card interaction — but `PhaseManager`'s Space shortcut reads raw input (`PhaseManager.cs:133`) and never consults the gate. Pressing Space while a preview is open starts the transition with the modal up. Consequences:

- The enlarged **shelf** card survives the transition (clear is skipped while `OwnsDeckCards`) and lingers through the entire combat on the abandoned page below.
- `ShopInputGate` stays blocked for the whole combat; the only combat-path reader is the hover tooltip timer (`CardPhysObjScript.cs:2106`), so **combat hover tooltips are suppressed** from landing until the player's first click — which "heals" the gate via `HandleClickToRestore` (`ShopCardView.cs:359-369`, no phase check) triggering an off-screen `RestoreCard` + `Unblock`. That is an unintended self-heal channel; the intended heal is `OnDestroy` (`ShopCardView.cs:778-785`) at next shop entry.
- The enlarged **deck**-card variant is benign: it flies as a dummy (DOScale pulls it back to deck scale) and `OnDestroy` releases the gate at landing.

### F3 — MEDIUM: landing wait covers only 3 cards (demo heritage)

`PhaseFlightPlanner.TotalDuration = transDur + 2 × cardStagger` (`PhaseFlightPlanner.cs:55-58`, demo `:739` had exactly 3 cards), while dummy *i* finishes at `i × stagger + transDur` (`PhaseTransitionDriver.cs:301-315`). With the shipped config (`transDur 0.8`, `cardStagger 0.07`) the coroutine waits 0.99 s, then the guard passes and **all** dummies are destroyed (`PhaseTransitionDriver.cs:223-244`):

| Deck cards | last dummy lands at | destroyed early by |
|---|---|---|
| 3 | 0.94 s | 0 (covered — why Play verification passed) |
| 4 | 1.01 s | 0.02 s |
| 6 | 1.15 s | 0.16 s |
| 8 | 1.36 s | 0.37 s |
| 12 | 1.64 s | 0.65 s |

For decks ≥ 4 cards the tail dummies are destroyed mid-arc and the real face-down cards pop into the stack slots — a visible teleport that grows with deck size (cap 12). Early-game 3-card decks are why this never surfaced in play tests.

### F4 — LOW: dummy landing math is FloatStack-only

`FlyDummiesToCombatStack` hand-rolls a vertical stack (`anchor + stepWorld*i`, `PhaseTransitionDriver.cs:292, 303`) using `floatStackStepY × floatStackPxToWorld`. Correct for the shipped scene (`deckLayoutMode: 3 = FloatStack`, `GameScene.unity:7823`), but `CombatUXManager`'s code default is Cascade (`CombatUXManager.cs:119`), and in Cascade/ArcLoop the real physicals land on curve layouts — the "destroying the dummies is invisible" swap property breaks (every card pops to a different pose). Latent: harmless until someone switches layout mode.

### F5 — LOW: show/hide timing asymmetries (decision only)

- `ShopSectionPanels.ShowIfActive` has no `IsTransitioning` gate (`ShopSectionPanels.cs:173-179`) while `ShopChrome.ShowIfActive` does (`ShopChrome.cs:135`): on Result→Shop the panels are visible during the descent, the top bar pops at landing only.
- `ExitShop` hides chrome + panels at travel start (`GameScene.unity:11497` wiring), so the departing page goes bare while its cards fly away.

Both read as intentional page-content behavior; listed for an explicit accept/deviate ruling.

## 3. Fix designs

### 3.1 F1 — busy guard at the call sites (recommended: PhaseManager-side)

Keep the driver's `false` = "legacy is correct" contract pure; make busy non-reachable:

	// PhaseManager.Update, Shop branch — LOAD-BEARING (reachable during Result→Shop travel):
	if (PhaseTransitionDriver.IsTransitioning) return;
	if (PhaseTransitionDriver.RequestShopToCombat(this)) return;
	ExitingShopPhase();
	EnteringCombatPhase();

	// PhaseManager.Update, Result branch — defensive (currently unreachable, phase flips at travel start):
	if (PhaseTransitionDriver.IsTransitioning) return;

	// ShopHudBinder.Execute(LeaveShop) — defensive (gate-blocked in practice):
	if (PhaseTransitionDriver.IsTransitioning) return;

Cost: one static-bool read per frame per branch. Bypass paths (`Available == false`) untouched. `autoSpace` semantics unchanged (it waits out the travel, then advances — same as its legacy behavior, minus the double-fire).

### 3.2 F2 — transition-start dismiss of all enlarge previews

- New helper on `ShopUXManager` (not a god file): `RestoreAllEnlargedCards()` — reuse the existing `CollectEnlargedViews` collector (`ShopUXManager.cs:956`), call `RestoreCard()` on each. `RestoreCard` is already transition-safe for this purpose (target writes only; deck-card restores get killed by `FlyDummiesToCombatStack`'s `KillTweens` anyway).
- Call it in `ShopToCombatRoutine` right after `_ownsDeckCards = true`, **before** the phase calls. Gate bookkeeping stays balanced (the modal's `Block` is returned by `RestoreCard`'s `Unblock`; the driver's own pair is unaffected).
- Result: Space-during-modal produces a clean transition; the lingering-card and tooltip-suppression symptoms disappear; the accidental `HandleClickToRestore` combat heal stops being load-bearing.
- **Optional add-on (user call)**: also gate PhaseManager's Space by `ShopInputGate`. Trade-off: Space is the only exit from a fully scrolled shop (`ShopChrome.cs:21-22`) — with the modal open, click-dismiss exists, so the gate check is semantically fine; it also swallows Space during the ~0.5 s reroll animation (arguably desirable). Not required if the dismiss fix lands.

### 3.3 F3 — count-aware total flight wait

Add an overload and keep the demo golden intact:

	// PhaseFlightPlanner:
	public static float TotalDuration(float duration, float stagger, int cardCount)
	{
		return duration + Mathf.Max(0, cardCount - 1) * stagger;
	}

- `count = 3` yields the identical `dur + 2*stagger` → the existing `PhaseFlightPlannerTests` 3-card golden stays valid byte-for-byte; add goldens for count = 1 / 4 / 8.
- Driver passes `dummies.Count` at `PhaseTransitionDriver.cs:223`.
- Accepted trade-off: combat content appears `(count-3) × 0.07 s` later for large decks (12 cards → +0.63 s). Alternative (unblock at camera-land, destroy dummies per-tween) rejected: real physicals would pop into slots while dummies are still flying toward them — double vision is worse than a slightly later reveal. If the extra wait feels long in Play, a tail cap (e.g. clamp the stagger tail at ~0.4 s) is a one-line tuning knob — user decides after feeling it.
- Per Critical Rules this is a visual bug fix in `Managers/`: VISUAL-FIX comment block in `PhaseTransitionDriver.cs` + `docs/RegressionChecklist.md` row are mandatory.

### 3.4 F4 (optional hardening) — layout-agnostic dummy landing

Route the dummy slot-i target through `DeckPositionCalculator.CalculatePositionAtIndex` (the single source of truth per AGENTS.md; combat already seeds it from `physicalCardDeckPos`), replacing the hand-rolled stack math. Exact signature to be confirmed from `DeckPositionCalculator.cs` at implementation time. Only worth doing if layout switching is on the roadmap; FloatStack ships today.

### 3.5 F5 — document as accepted deviations

Add both asymmetries to the "Deviations from the demo (accepted)" list in `docs/PhaseTransition.md`. No code.

## 4. Implementation steps (step-gated: each step stops for confirmation before the next)

| Step | Scope | Files | Tests / obligations |
|---|---|---|---|
| 1 | F1 busy guards | `Managers/PhaseManager.cs` (×2), `UXPrototype/ShopHudBinder.cs` (defensive), `docs/PhaseTransition.md` (Arbitration bullet) | RegressionChecklist row (mash-Space scenario); Play: double-Space on Result screen → shop reached exactly once |
| 2 | F2 transition-start dismiss | `UXPrototype/ShopUXManager.cs` (helper), `Managers/PhaseTransitionDriver.cs` (call) | RegressionChecklist row; Play: enlarge+Space both card kinds; combat tooltip alive before first click |
| 3 | F3 count-aware wait | `UXPrototype/PhaseFlightPlanner.cs`, `Managers/PhaseTransitionDriver.cs`, `Editor/Tests/PhaseFlightPlannerTests.cs`, `docs/PhaseTransition.md` | New goldens; VISUAL-FIX block + checklist row (mandatory); Play: 8-card deck tail lands before swap; 3-card deck unchanged |
| 4 | F4 layout-agnostic landing (optional) | `Managers/PhaseTransitionDriver.cs` | Only if layout switching is planned |
| 5 | F5 deviations doc | `docs/PhaseTransition.md` | — |

All `.cs` edits: CRLF + Tab; `refresh_unity` before any test run (stale-assembly ruling); `SaveOpenScenes` before `run_tests`.

## 5. Verification plan

**EditMode**: updated `PhaseFlightPlannerTests` green; full suite zero-drift vs the current baseline (675/674).

**Play matrix** (manual, per step-gate protocol — user runs or authorizes Play):

1. F1: mash Space at the Result screen (≥ 2 presses inside the travel) → shop visit intact, one `[ShopButton] EnteringCombatPhase` log pair per cycle; `autoSpace` full-run smoke ×1.
2. F2: enlarge shelf card → Space → clean travel; enlarge deck card → Space → flight at deck scale; combat hover tooltip shows without any prior click.
3. F3: transition with an 8+ card deck → every dummy visibly lands before the swap; 3-card deck visually identical to today.
4. Regression: full Shop→Combat→Result→Shop ×2; Space exit from a fully scrolled shop (must keep working); reroll → immediate Space (gate pair integrity — no stuck input).

## 6. Risks / notes

- Step 1 touches `PhaseManager.Update`, the hottest input path — the added cost is one static property read; the legacy hard-cut path (driver off / headless / seed override) is byte-identical.
- Step 3 delays the reveal for large decks by up to +0.63 s (12 cards). Feel call belongs to the user; tail cap is the escape hatch.
- Steps 1–3 are independent of each other; step 2 alone does not fix step 1's double-entry and vice versa (Space-during-modal also fires the F1 fallback once the travel starts).
- Findings evidence is pinned to today's line numbers (`main` @ `9eacd549`); re-locate before editing if further commits land.
