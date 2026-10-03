# Phase Transition HUD Handoff (Pill/Avatar Swap + Compare-Bar World Pin) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the player HP pill / avatar mirror swap invisible (same-frame, scale-matched handoff driven by the world flight itself) and turn the full-screen HP compare bar into combat page content that slides with the camera instead of popping at landing.

**Architecture:** Two seams in the existing phase-transition system (`docs/PhaseTransition.md`). (1) The canvas-HUD ↔ world-mirror swap currently runs at driver-coroutine time vs presenter-poll time, producing a 1-frame blank (departure), a 1-frame double render (arrival), and a scale pop (combat 0.8 vs mirror `canvasShopScale` 0.41). The handoff moves into the presenters and is driven by `HudWorldFlight` edges: the mirror hides only after `flight.Begin` has world-locked the canvas copy at the mirror spot, and the mirror returns inside the flight's `OnComplete` while the canvas copy eases into the mirror's shop scale over the flight's final window. (2) The compare bar (`CombatHPBarPresenter`) is released by `SuppressCombatCanvasUI` at landing (a pop); a new `HudWorldFlight.Pin` world-locks it at its combat-page home for the duration of both travels so the camera carries it in/out exactly like page content.

**Tech Stack:** Unity 6 (C#, Roslyn via MCP `execute_code`), DOTween, uGUI Screen-Space-Camera HUD canvas, world-space `ShopHudPage.prefab` chrome mirrors (`ShopMirrorScale.canvasShopScale`).

**Spec:** `docs/PhaseTransition.md` (post-2026-10-02 state, seven fixes verified 2026-10-03) + user rulings from the 2026-10-03 review conversation: Q1 "player HP pill appearance should be more natural, e.g. swap mid-travel" — accepted as flight-end handoff with scale matching; Q2 "enemy hpbar should slide down in sync instead of appearing directly" — scoped to the full-screen HP compare bar (user confirmation 2026-10-03).

## Global Constraints

- Line endings CRLF, tab indentation, English comments/docs (AGENTS.md).
- Visual changes in `UXPrototype/` / `Managers/` MUST carry `VISUAL-FIX(2026-10-03):` blocks per `docs/VisualBugPrevention_Guide.md` (search existing blocks first).
- Every visual fix MUST append a row to `docs/RegressionChecklist.md` (next free numbers: 139, 140).
- After every `.cs` edit: Unity MCP `refresh_unity` (`compile: request`), confirm `EditorApplication.isCompiling == false` AND `Assembly-CSharp.dll` mtime > edited source mtime before `run_tests` (stale-assembly trap, AGENTS.md).
- Before every `run_tests`: `EditorSceneManager.SaveOpenScenes()` (pre-approved) as the immediately preceding step; EditMode full suite with `init_timeout` ~180000.
- Do NOT run Play Mode tests from the agent (AGENTS.md); the Play matrix in Task 6 is the user's manual checklist.
- Commit straight to `main` (no branches), one commit per task.
- All transition tweens run unscaled time (`SetUpdate(UpdateType.Normal, true)`) and are NOT scaled by `CombatAnimationSpeed.SpeedScale` (recorded decision, plan-phase-transition-world-camera §14).
- The demo's "no scale tween" rule (fix 7) applies to the FLIGHT between homes; the new handoff scale window is an emulation seam the demo does not have (the demo never swaps copies) — documented as a deviation in Task 6.
- Deterministic-RNG rule untouched: no randomness is added anywhere in this plan.

## Review Focus

1. `_handedOffToMirror` never resetting would keep the canvas pill/avatar hidden on the NEXT shop visit → reset on every travel edge, pinned by Task 2/3 reset steps and Play matrix step 4 (two full loops).
2. The handoff scale tween on the shared ease (OutBack) would undershoot BELOW the mirror scale and visibly breathe at the band → the window tween is pinned to `Ease.OutQuad` in every code step below; Play matrix step 1/3 watches for breathing.
3. The driver's synchronous departure hide (`PhaseTransitionDriver.ShopToCombatRoutine`) must be removed in the SAME commit as the presenter-driven hide — driver-only leaves the 1-frame blank, presenter-only leaves mirrors visible (double pill during travel) → Task 3 owns both edits, one commit.
4. `CombatHPBarPresenter.EnterCombat` at the ToCombat travel start reads the frozen displayed-HP queue from the PREVIOUS combat; the bar may slide in with the old split and then ease to the new combat's split via the existing `UpdateFills` poll (0.25 s share tween) — accepted behavior, Play matrix step 1 confirms it reads as a smooth re-split, not a snap.
5. Result→Shop: `AdvanceFromResultToShop` destroys the result overlay at travel start, so the re-pinned bar reappears on the combat page uncovered; if that ordering ever changes the bar would show UNDER the overlay for the whole descent — Play matrix step 3 watches for it.

---

### Task 1: `HudWorldFlight` — `Pin` + completion callback

**Files:**
- Modify: `Assets/Scripts/UXPrototype/PhaseHudFlight.cs:55-67` (Begin) and add `Pin` after it

**Interfaces:**
- Consumes: nothing new.
- Produces:
	- `public void Begin(Vector3 fromCenterWorld, Vector3 toCenterWorld, float duration, PhaseTransitionConfigSO cfg, System.Action onComplete = null)` — same flight as today plus an optional DOTween `OnComplete` hook (default keeps all existing call sites compiling).
	- `public void Pin(Vector3 centerWorld)` — world-locks the rect at a fixed world point (no tween) until `Kill()`; used by Task 5.

- [x] **Step 1: Extend `Begin` and add `Pin`**

Replace the current `Begin` body (`PhaseHudFlight.cs:55-67`) with:

```csharp
/// <summary>
/// Starts a straight world flight of the rect center from -> to (shared ease, unscaled
/// time — phase-boundary motion is never combat-speed-scaled). No scale change (demo
/// flyShared): the rect renders at whatever scale the caller snapped it to beforehand.
/// onComplete (optional) fires when the flight tween completes — the 2026-10-03 handoff
/// seam uses it for the same-frame mirror swap at flight end.
/// </summary>
public void Begin(Vector3 fromCenterWorld, Vector3 toCenterWorld, float duration, PhaseTransitionConfigSO cfg, System.Action onComplete = null)
{
	Kill();
	if (_rt == null || _canvasRoot == null || _cam == null) return;
	_centerOffset = _rt.TransformPoint(_rt.rect.center) - _rt.position;
	_center = fromCenterWorld;
	ApplyWorldPoint();
	_following = true;
	Tween tween = DOTween.To(() => _center, v => _center = v, toCenterWorld, Mathf.Max(0.05f, duration));
	if (cfg != null) cfg.ApplyEase(tween);
	else tween.SetEase(Ease.OutBack);
	if (onComplete != null) tween.OnComplete(() => onComplete());
	_tween = tween.SetUpdate(UpdateType.Normal, true);
}

/// <summary>
/// World-locks the rect at a fixed world point (no tween) until Kill() — the camera's
/// own motion then carries the element exactly like page content (2026-10-03 compare-bar
/// pin). Same re-projection path as Begin.
/// </summary>
public void Pin(Vector3 centerWorld)
{
	Kill();
	if (_rt == null || _canvasRoot == null || _cam == null) return;
	_centerOffset = _rt.TransformPoint(_rt.rect.center) - _rt.position;
	_center = centerWorld;
	ApplyWorldPoint();
	_following = true;
}
```

- [x] **Step 2: Refresh + verify compile**

Unity MCP `refresh_unity` (`compile: request`); confirm `isCompiling == false` and `Assembly-CSharp.dll` mtime > `PhaseHudFlight.cs` mtime. Expected: no console errors.

- [x] **Step 3: EditMode suite stays green**

`EditorSceneManager.SaveOpenScenes()` via `execute_code`, then `run_tests` (EditMode, full suite, `init_timeout: 180000`). Expected: same totals as the pre-change run (no test exercises this class; nothing may regress).

- [x] **Step 4: Commit**

```bash
git add Assets/Scripts/UXPrototype/PhaseHudFlight.cs
git commit -m "phase-transition: HudWorldFlight Pin + onComplete hook (handoff prep)"
```

### Task 2: Player HP pill — flight-driven, scale-matched mirror handoff

**Files:**
- Modify: `Assets/Scripts/UXPrototype/HPNumericDisplayHorizontal.cs` (fields ~137-139, `Update` visibility rule ~294-299, `OnTravelEdge` ~499-552, `SnapToCombatAnchor` ~561-568)

**Interfaces:**
- Consumes: Task 1 `Begin(..., onComplete)`; `ShopChrome.SetMirrorsActive(bool)` (`ShopChrome.cs:153`); `ShopTopBarLayout.ShopScaleHpDisplay` (`ShopTopBarLayout.cs:85`); `PhaseFlightPlanner.HudHomeAtPage(Vector3, float, float)`; `PhaseTransitionDriver.CombatPageY` / `.Travel` / `.IsTransitioning`; `HudWorldFlight.RigY`.
- Produces: `private const float HandoffScaleWindow = 0.35f` (per-presenter; Task 4 defines its own with the same value), `private bool _handedOffToMirror` pattern reused by Task 4. No public surface.

- [x] **Step 1: Add handoff state + scale-window constant**

Next to the existing flight fields (after `private HudWorldFlight _flight;`):

```csharp
// 2026-10-03 handoff seam: the flight completes INTO the world mirror — the canvas
// copy eases into the mirror's shop scale over the flight's final window, then the
// flight's onComplete swaps copies in the same frame. _handedOffToMirror keeps the
// visibility rule from re-activating the canvas copy while the driver winds down.
private bool _handedOffToMirror;
private const float HandoffScaleWindow = 0.35f; // flight fraction used to ease between combat scale and the mirror's shop scale (opening window on departure, final window on return)
```

- [x] **Step 2: `SnapToCombatAnchor` stops writing scale**

Replace the method (`HPNumericDisplayHorizontal.cs:561-568`) with:

```csharp
/// <summary>Snaps the pill to its combat anchor POSITION (the home math needs it) and returns its world position. Scale is no longer written here (2026-10-03 handoff seam): the flight endpoints own scale — player flights ease between the mirror's shop scale and combat scale, enemy flights set combat scale explicitly at their call sites. Runs for BOTH sides: a shop-park can have left the transform at the shop anchor.</summary>
private Vector3 SnapToCombatAnchor()
{
	KillTween(ref _placementTween);
	KillTween(ref _placementScaleTween);
	_selfRt.anchoredPosition = _combatAnchoredPos;
	return _selfRt.position;
}
```

- [x] **Step 3: Rewrite the player branches of `OnTravelEdge`; pin enemy scale explicitly**

In `OnTravelEdge` (`HPNumericDisplayHorizontal.cs:499-552`), replace the `ToCombat` case with:

```csharp
			case PhaseTransitionDriver.TransitionTravel.ToCombat:
			{
				if (side == Side.Player)
				{
					// Mirror check BEFORE the snap: the fallback glide needs the parked shop anchor intact.
					if (!mirrorOk) return;
					_handedOffToMirror = false;
					Vector3 to = PhaseFlightPlanner.HudHomeAtPage(SnapToCombatAnchor(), PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY);
					// VISUAL-FIX(2026-10-03): pill popped 0.41 -> 0.8 at travel start
					//   Cause:    SnapToCombatAnchor wrote the combat scale instantly while the
					//             replaced mirror renders at ShopMirrorScale.canvasShopScale.
					//   Affects:  HPNumericDisplayHorizontal.OnTravelEdge (player, both travels)
					//   Regress:  离开商店: the pill starts at the mirror's size and grows into
					//             the combat scale over the opening window — no size pop at the
					//             band; Result->Shop: symmetric shrink into the band.
					//   Related:  docs/PhaseTransition.md (HUD world flights), ShopMirrorScale
					_selfRt.localScale = Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay;
					KillTween(ref _placementScaleTween);
					_placementScaleTween = _selfRt.DOScale(_combatScale, cfg.transDur * HandoffScaleWindow)
						.SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					_flight = EnsureFlight();
					_flight.Begin(shopHome, to, cfg.transDur, cfg);
					// Presenter-driven mirror hide (replaces the driver's synchronous pre-phase
					// hide): the canvas copy is already active and world-locked at the mirror
					// spot, so the swap has no blank frame regardless of script execution order.
					ShopChrome.SetMirrorsActive(false);
				}
				else
				{
					// Entrance (fix 2): slide DOWN in from enemySlide above; the world position
					// keeps the pill outside the viewport until the camera's arrival, like the demo.
					Vector3 to = PhaseFlightPlanner.HudHomeAtPage(SnapToCombatAnchor(), PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY);
					_selfRt.localScale = _combatScale; // was SnapToCombatAnchor's job before 2026-10-03
					_flight = EnsureFlight();
					_flight.Begin(to + Vector3.up * SlideWorld(cfg), to, cfg.transDur, cfg);
				}
				break;
			}
```

and the `ToShop` case with:

```csharp
			case PhaseTransitionDriver.TransitionTravel.ToShop:
			{
				if (side == Side.Player)
				{
					if (!mirrorOk) return;
					_handedOffToMirror = false;
					Vector3 from = PhaseFlightPlanner.HudHomeAtPage(_selfRt.position, PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY);
					// Shrink into the mirror's shop scale over the final window so the swap
					// below lands size-matched (the old landing swap popped 0.8 -> 0.41).
					_selfRt.localScale = _combatScale;
					KillTween(ref _placementScaleTween);
					_placementScaleTween = _selfRt.DOScale(Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay, cfg.transDur * HandoffScaleWindow)
						.SetDelay(cfg.transDur * (1f - HandoffScaleWindow)).SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					_flight = EnsureFlight();
					var captured = this;
					_flight.Begin(from, shopHome, cfg.transDur, cfg, () =>
					{
						// Same-frame swap at flight end: the mirror shows, the canvas copy
						// hides, and the settled-shop rule must not re-activate it while the
						// driver winds down (its own SetMirrorsActive(true) stays as a
						// defensive idempotent call).
						ShopChrome.SetMirrorsActive(true);
						captured._handedOffToMirror = true;
						if (captured.displayRoot != null) captured.displayRoot.gameObject.SetActive(false);
					});
				}
				else
				{
					// Exit (fix 3): slides UP out while the camera descends (demo :727 -enemySlide).
					Vector3 home = PhaseFlightPlanner.HudHomeAtPage(_selfRt.position, PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY);
					_flight = EnsureFlight();
					_flight.Begin(home, home + Vector3.up * SlideWorld(cfg), cfg.transDur, cfg);
				}
				break;
			}
```

and the `default` case (resets the handoff flag on every settle):

```csharp
			default:
				_handedOffToMirror = false;
				if (_flight != null) _flight.Kill();
				break;
```

- [x] **Step 4: Visibility rule honors the handoff flag**

In `Update` (`HPNumericDisplayHorizontal.cs:294-299`), change the player branch to:

```csharp
	bool visible = side == Side.Player
		? inCombat || phase == EnumStorage.GamePhase.Result
			|| (phase == EnumStorage.GamePhase.Shop && PhaseTransitionDriver.IsTransitioning && !_handedOffToMirror)
		: (inCombat && !PhaseTransitionDriver.SuppressCombatCanvasUI)
			|| phase == EnumStorage.GamePhase.Result
			|| travel != PhaseTransitionDriver.TransitionTravel.None;
```

(After the handoff, the next Update's `!visible && _wasVisible` edge runs `ExitVisiblePhase`, which parks the pill at the shop anchor/scale — exactly the parked state the next cycle needs. This is the intended cleanup path; do not bypass it.)

- [x] **Step 5: Refresh + full EditMode suite**

`refresh_unity` (`compile: request`) → confirm assembly mtime > source mtime → `SaveOpenScenes()` → `run_tests` (EditMode full, `init_timeout: 180000`). Expected: green, same totals.

- [x] **Step 6: Commit**

```bash
git add Assets/Scripts/UXPrototype/HPNumericDisplayHorizontal.cs
git commit -m "phase-transition: player HP pill scale-matched flight-driven mirror handoff"
```

### Task 3: Driver departure-hide removal + avatar handoff (ONE commit)

**Files:**
- Modify: `Assets/Scripts/Managers/PhaseTransitionDriver.cs:211-216` (remove the synchronous departure hide)
- Modify: `Assets/Scripts/UXPrototype/CombatIconPresenter.cs` (fields ~56-60, `ApplyPhase` visibility ~148, `OnTravelEdge` ~198-238, `SnapPlayerToCombatAnchor` ~247-254)

**Interfaces:**
- Consumes: Task 1 `Begin(..., onComplete)`; `ShopChrome.SetMirrorsActive(bool)`; `ShopChrome.TryGetAvatarWorldCenter(out Vector3)`; `ShopTopBarLayout.ShopScaleAvatar` (`ShopTopBarLayout.cs:82`).
- Produces: nothing new. Review Focus item 3 lives here: the driver removal and the presenter hide MUST ship in this one commit.

- [x] **Step 1: Driver — remove the synchronous departure mirror hide**

In `PhaseTransitionDriver.ShopToCombatRoutine`, delete the `ShopChrome.SetMirrorsActive(false);` call (`PhaseTransitionDriver.cs:216`) and replace the comment block above it (`PhaseTransitionDriver.cs:212-215`) with:

```csharp
		// 2026-10-02 re-audit fixes 4/7: the chrome band (chips/buttons) is page content and
		// stays visible to scroll away with the camera (fix 4a — ExitShop skips its hide while
		// IsTransitioning). 2026-10-03 handoff seam: the departure mirror hide now lives in the
		// HUD presenters (right after flight.Begin world-locks the canvas copy at the mirror
		// spot), so this coroutine no longer touches the mirrors on departure — hiding here
		// raced the presenters' Update and could blank the avatar/HP for one frame. The
		// Result->Shop hide below stays (mirrors are already off; defensive) and the landing
		// SetMirrorsActive(true) stays as an idempotent safety net behind the flight's
		// onComplete handoff.
```

(Net effect: `ShopToCombatRoutine` makes no mirror call on departure; `ResultToShopRoutine`'s hide and both landing shows are unchanged.)

- [x] **Step 2: Avatar — handoff state + visibility rule**

In `CombatIconPresenter`, after the flight fields (`CombatIconPresenter.cs:58-60`):

```csharp
// 2026-10-03 handoff seam: same contract as HPNumericDisplayHorizontal — the flight
// completes INTO the world mirror (scale-matched), then swaps copies in one frame.
private bool _handedOffToMirror;
private const float HandoffScaleWindow = 0.35f; // keep in sync with HPNumericDisplayHorizontal.HandoffScaleWindow
```

In `ApplyPhase` (`CombatIconPresenter.cs:148`), change the player rule to:

```csharp
	playerIcon.SetActive(inCombat || inResult || (inShop && PhaseTransitionDriver.IsTransitioning && !_handedOffToMirror));
```

- [x] **Step 3: Avatar — `SnapPlayerToCombatAnchor` stops writing scale; rewrite player branches**

Replace `SnapPlayerToCombatAnchor` (`CombatIconPresenter.cs:247-254`) with:

```csharp
/// <summary>Snaps the player icon to its combat anchor POSITION (kill glide tweens; the home math needs it) and returns its world position. Scale is no longer written here (2026-10-03 handoff seam): the player flight endpoints own scale.</summary>
private Vector3 SnapPlayerToCombatAnchor()
{
	KillTween(ref _glideTween);
	KillTween(ref _scaleTween);
	_playerIconRt.anchoredPosition = _combatAnchoredPos;
	return _playerIconRt.position;
}
```

In `OnTravelEdge`, replace the `ToCombat` case's player block with:

```csharp
				if (_playerIconRt != null && ShopChrome.TryGetAvatarWorldCenter(out Vector3 shopHome))
				{
					_handedOffToMirror = false;
					Vector3 to = PhaseFlightPlanner.HudHomeAtPage(SnapPlayerToCombatAnchor(), PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY);
					// VISUAL-FIX(2026-10-03): avatar popped shop scale -> combat scale at travel start
					//   Cause:    SnapPlayerToCombatAnchor wrote the combat scale instantly while the
					//             replaced mirror renders at ShopMirrorScale.canvasShopScale.
					//   Affects:  CombatIconPresenter.OnTravelEdge (player, both travels)
					//   Regress:  离开商店: the avatar starts at the mirror's size and grows over the
					//             opening window; Result->Shop: symmetric shrink into the band; the
					//             landing swap is a same-frame, size-matched exchange.
					//   Related:  docs/PhaseTransition.md (HUD world flights), ShopMirrorScale
					_playerIconRt.localScale = Vector3.one * ShopTopBarLayout.ShopScaleAvatar;
					_scaleTween = _playerIconRt.DOScale(_combatScale, cfg.transDur * HandoffScaleWindow)
						.SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					EnsureFlight(ref _playerFlight, _playerIconRt).Begin(shopHome, to, cfg.transDur, cfg);
					// Presenter-driven mirror hide — see HPNumericDisplayHorizontal (2026-10-03).
					ShopChrome.SetMirrorsActive(false);
				}
```

and the `ToShop` case's player block with:

```csharp
				if (_playerIconRt != null && ShopChrome.TryGetAvatarWorldCenter(out Vector3 shopHome))
				{
					_handedOffToMirror = false;
					Vector3 from = PhaseFlightPlanner.HudHomeAtPage(_playerIconRt.position, PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY);
					_playerIconRt.localScale = _combatScale;
					_scaleTween = _playerIconRt.DOScale(Vector3.one * ShopTopBarLayout.ShopScaleAvatar, cfg.transDur * HandoffScaleWindow)
						.SetDelay(cfg.transDur * (1f - HandoffScaleWindow)).SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					var capturedRt = _playerIconRt;
					EnsureFlight(ref _playerFlight, _playerIconRt).Begin(from, shopHome, cfg.transDur, cfg, () =>
					{
						ShopChrome.SetMirrorsActive(true);
						_handedOffToMirror = true;
						if (capturedRt != null) capturedRt.gameObject.SetActive(false);
					});
				}
```

and the `default` case with:

```csharp
			default:
				_handedOffToMirror = false;
				if (_playerFlight != null) _playerFlight.Kill();
				if (_enemyFlight != null) _enemyFlight.Kill();
				break;
```

(The enemy blocks of both cases are unchanged — the enemy icon never parks at the shop and has no mirror.)

- [x] **Step 4: Refresh + full EditMode suite**

Same gate as Task 2 Step 5. Expected: green, same totals.

- [x] **Step 5: Commit**

```bash
git add Assets/Scripts/Managers/PhaseTransitionDriver.cs Assets/Scripts/UXPrototype/CombatIconPresenter.cs
git commit -m "phase-transition: avatar handoff + presenter-driven departure mirror hide"
```

### Task 4: Compare bar — world pin during both travels

**Files:**
- Modify: `Assets/Scripts/UXPrototype/CombatHPBarPresenter.cs` (fields ~58-62, `Update` ~160-178, `LateUpdate` ~223-232)

**Interfaces:**
- Consumes: Task 1 `HudWorldFlight.Pin(Vector3)` / `Kill()` / `Tick()`; `PhaseFlightPlanner.HudHomeAtPage(Vector3, float, float)`; `PhaseTransitionDriver.CombatPageY` / `.Travel`; `HudWorldFlight.RigY`.
- Produces: nothing new. `PhaseTransitionDriver.SuppressCombatCanvasUI` loses its last gating consumer for the bar but stays in place for the icon/pill enemy rules (retirement is a separate cleanup, not this plan).

- [x] **Step 1: Pin state fields**

After `private Vector2 _barRootBasePos;` (`CombatHPBarPresenter.cs:62`):

```csharp
// 2026-10-03: world pin during driver travels — the bar is combat PAGE content and
// slides with the page (in from the top edge on Shop->Combat, up out on Result->Shop)
// instead of popping in/out at landing. Same HudWorldFlight re-projection as the
// icon/pill flights, pinned (no tween).
private HudWorldFlight _pin;
private PhaseTransitionDriver.TransitionTravel _lastTravel = PhaseTransitionDriver.TransitionTravel.None;
```

- [x] **Step 2: Travel edge + new visibility rule in `Update`**

Replace the top of `Update` (the suppression comment + `inCombat` computation + edge block, `CombatHPBarPresenter.cs:160-178`) with:

```csharp
	private void Update()
	{
		// Travel edges drive the world pin BEFORE the visibility edge block (same ordering
		// convention as the icon/pill presenters).
		var travel = PhaseTransitionDriver.Travel;
		if (travel != _lastTravel)
		{
			OnTravelEdge(travel);
			_lastTravel = travel;
		}
		// VISUAL-FIX(2026-10-03): HP compare bar popped in/out at transition landings
		//   Cause:    SuppressCombatCanvasUI held the bar hidden until the camera landed,
		//             then EnterCombat SetActive'd it in one frame; on Result->Shop,
		//             ExitCombat hid it at travel start. The demo's combat HP display is
		//             page content (v1.1 topo: red band) and slides with the camera.
		//   Affects:  CombatHPBarPresenter.Update (visibility rule), OnTravelEdge (pin)
		//   Regress:  离开商店: the bar slides IN from the top edge with the page; combat
		//             behavior (fills/ghost/flash/shake/pulse) unchanged; Result->Shop: the
		//             bar reappears on the combat page at travel start and slides UP out;
		//             driver bypass (headless/seed): Travel stays None — the rule reduces to
		//             Combat-only, byte-identical to before.
		//   Related:  docs/PhaseTransition.md (HUD world flights), HudWorldFlight.Pin
		bool visible = gamePhaseRef.Value() == EnumStorage.GamePhase.Combat
			|| travel != PhaseTransitionDriver.TransitionTravel.None;
		if (visible && !_wasInCombat)
		{
			EnterCombat();
		}
		else if (!visible && _wasInCombat)
		{
			ExitCombat();
		}
		_wasInCombat = visible;
		if (!visible)
		{
			return;
		}
```

(the rest of `Update` is unchanged; `_wasInCombat` keeps its name but now tracks `visible`.)

- [x] **Step 3: `OnTravelEdge` + pin tick in `LateUpdate`**

Add after `Update`:

```csharp
	/// <summary>
	/// Travel edges pin the bar to its combat-page home (2026-10-03): world-locked with no
	/// tween, so the camera's travel carries it in from the top edge (Shop->Combat) and up
	/// out (Result->Shop) exactly like page content. Travel->None unpins; the normal
	/// placement writes resume seamlessly because anchoredPosition tracks the world writes.
	/// </summary>
	private void OnTravelEdge(PhaseTransitionDriver.TransitionTravel travel)
	{
		switch (travel)
		{
			case PhaseTransitionDriver.TransitionTravel.ToCombat:
			case PhaseTransitionDriver.TransitionTravel.ToShop:
				if (_pin == null) _pin = new HudWorldFlight(barRoot, canvas);
				_pin.Pin(PhaseFlightPlanner.HudHomeAtPage(barRoot.position, PhaseTransitionDriver.CombatPageY, HudWorldFlight.RigY));
				break;
			default:
				if (_pin != null) _pin.Kill();
				break;
		}
	}
```

and extend `LateUpdate` (`CombatHPBarPresenter.cs:223-232`) to:

```csharp
	private void LateUpdate()
	{
		if (_pin != null) _pin.Tick();
		if (!_wasInCombat)
		{
			return;
		}
		// Flash overlays are Filled mirrors of their segment.
		playerFlash.fillAmount = playerSeg.fillAmount;
		enemyFlash.fillAmount = enemySeg.fillAmount;
	}
```

- [x] **Step 4: Refresh + full EditMode suite**

Same gate as Task 2 Step 5. Expected: green, same totals.

- [x] **Step 5: Commit**

```bash
git add Assets/Scripts/UXPrototype/CombatHPBarPresenter.cs
git commit -m "phase-transition: compare bar world-pinned during travels (slides with the page)"
```

### Task 5: Docs + regression rows

**Files:**
- Modify: `docs/PhaseTransition.md:29-31` (HUD world flights paragraph), `:19-21` (orchestration bullets), `:70-90` (deviations)
- Modify: `docs/RegressionChecklist.md` (append rows 139, 140 after row 138)

- [x] **Step 1: `docs/PhaseTransition.md` updates**

- In the "HUD world flights" paragraph, replace the sentences "During the travel the world Avatar/HpPill mirrors hide (single shared copy; `ShopChrome.SetMirrorsActive`) and return on the shop landing. The full-screen HP compare bar is NOT a shared element — it stays suppressed until landing, combat-only as before." with:

  "During the travel the world Avatar/HpPill mirrors hide (single shared copy) and return at the handoff. **Handoff seam (2026-10-03)**: the swap is driven by the flight itself — on departure the presenter hides the mirror only after `flight.Begin` has world-locked the canvas copy at the mirror spot (no blank frame), and the canvas copy eases from the mirror's shop scale (`ShopMirrorScale.canvasShopScale`) to the combat scale over the flight's opening `HandoffScaleWindow` (0.35, `Ease.OutQuad`); on the return it eases back over the final window and the flight's `OnComplete` shows the mirror + hides the canvas copy in the same frame (`_handedOffToMirror` suppresses re-activation until the settle edge). The full-screen HP compare bar IS page content: a `HudWorldFlight.Pin` world-locks it at its combat-page home for both travels, so it slides in from the top edge with the page and slides up out on the return; `SuppressCombatCanvasUI` no longer gates the bar (it still gates the icon/pill enemy rules)."

- In the Shop → Combat orchestration bullet, replace "canvas UI unsuppressed" with "the compare bar arrives already pinned (it slides with the page)".
- In the Result → Shop bullet, replace "on landing the world mirrors return (`ShopChrome.SetMirrorsActive(true)`)" with "the world mirrors return inside the flight's `OnComplete` (driver's `SetMirrorsActive(true)` stays as an idempotent safety net)".
- Under "Deviations from the demo" add:

  "6. Handoff scale window (2026-10-03): the canvas HUD eases between the combat scale and the mirror's `canvasShopScale` over the flight's opening/final 35% (`HandoffScaleWindow`, `Ease.OutQuad`). The demo needs no such tween (one shared element, same size both homes); our two-copy emulation (canvas HUD vs world prefab mirror) does. Not demo-visible: the flight path itself still carries no scale tween per fix 7."

- [x] **Step 2: Regression rows**

Append to `docs/RegressionChecklist.md` (same column format as rows 132-138):

- Row 139 — "Visual fix (2026-10-03): player HP pill / avatar mirror swap was visible — 1-frame blank at departure, 1-frame double render at arrival, and a scale pop (combat 0.8 vs mirror `canvasShopScale` 0.41) because the swap ran at driver-coroutine time vs presenter-poll time with no scale matching" | `HPNumericDisplayHorizontal` / `CombatIconPresenter` (flight-driven handoff, `HandoffScaleWindow` ease, `_handedOffToMirror`), `PhaseTransitionDriver` (synchronous departure hide removed) | 2026-10-03 | ⚠️ | Step: 离开商店 then Result→shop, twice. Check: no blank frame at the band on departure; pill/avatar grow from mirror size over the first third of the ascent and shrink back over the last third of the descent; landing swap shows neither a size pop nor a doubled pill; settled shop shows the mirror only; bypass (`-odseed 5`) hard cut unchanged.
- Row 140 — "Visual fix (2026-10-03): HP compare bar popped in at the combat landing and out at the Result→Shop travel start — it is combat page content and must slide with the camera (demo v1.1 topo: red band)" | `CombatHPBarPresenter` (`HudWorldFlight.Pin` during both travels; visibility rule Combat ∪ traveling; `SuppressCombatCanvasUI` no longer gates the bar) | 2026-10-03 | ⚠️ | Step: 离开商店 — the bar slides IN from the top edge with the page (it may open on the previous combat's split and ease to the new one); finish into Result — bar hidden as before; continue to shop — the bar reappears at travel start and slides UP out; bypass hard cut unchanged.

- [x] **Step 3: Commit**

```bash
git add docs/PhaseTransition.md docs/RegressionChecklist.md
git commit -m "docs: phase-transition handoff seam + compare-bar pin (PhaseTransition.md, RegressionChecklist 139-140)"
```

### Task 6: User Play verification (manual — agent does NOT run Play Mode)

Hand the user this matrix after Task 5 merges:

- [ ] 1. 离开商店 (Overshoot default): no blank frame at the band; pill + avatar grow from mirror size over the opening third; compare bar slides in from the top edge (an old→new HP split re-ease is acceptable, Review Focus 4); enemy HUD slide-in unchanged.
- [ ] 2. Combat → Result: bar hidden at Result entry (unchanged); enemy HUD stays visible (fix 3 unchanged).
- [ ] 3. Result → Shop: bar reappears at travel start and slides up out; pill + avatar shrink into the band over the final third; landing swap has no size pop / double / blank; OptionsButton re-pins; mirrors are the only copies in the settled shop.
- [ ] 4. Two full loops: no stuck-hidden pill/avatar (`_handedOffToMirror` reset, Review Focus 1); no scale breathing at the band (Review Focus 2).
- [ ] 5. Bypass: `-odseed 5` — hard cut byte-identical to before (bar/pill/chrome).
- [ ] 6. Resize the window in a settled shop, then transition once: no drift (re-projection is per-frame).

---

## Self-Review Log

- **Spec coverage:** Q1 (natural pill appearance, mid-travel swap) → Tasks 1-3 (flight-driven same-frame swap + scale matching; the "mid-travel" request is realized as the final-window handoff — a literal mid-flight hard swap would teleport the pill, rejected in the 2026-10-03 review). Avatar included (same seam). Q2 scoped by the user to the compare bar → Task 4. Docs/checklist → Task 5. Crossfade (option C from the review) deliberately excluded (YAGNI; fallback if Play still shows a seam).
- **Placeholder scan:** every code step contains the full replacement code; no TBD/TODO.
- **Type consistency:** `HudWorldFlight.Begin(Vector3, Vector3, float, PhaseTransitionConfigSO, System.Action)` / `Pin(Vector3)` / `Kill()` / `Tick()` (Task 1) match the call sites in Tasks 2-4; `HandoffScaleWindow` is a per-presenter private const (Task 2 defines, Task 3 duplicates with a keep-in-sync note — no cross-file coupling); `_handedOffToMirror` is presenter-private; `ShopTopBarLayout.ShopScaleHpDisplay` / `ShopScaleAvatar` and `ShopChrome.SetMirrorsActive(bool)` verified to exist at the cited lines.
- **Review Focus:** 5 items listed; each has a pinning step (reset steps in Tasks 2/3, OutQuad in every scale tween, single-commit rule in Task 3, re-ease acceptance + zero-safe `EnterCombat` math in Task 4, ordering watch in Task 6 step 3).
