using System.Collections;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Phase transition driver (plan-phase-transition-world-camera-2026-09-21; interactive spec:
/// docs/demo/PhaseTransitionDemo.html). ONE continuous world: the combat page sits one screen
/// ABOVE the shop page; Shop->Combat / Result->Shop = the camera rig travels vertically while
/// shared elements (player avatar + HP pill, player deck cards) fly between their two layout
/// homes and the enemy HUD enters counter-direction. Presentation-only wrapper around the
/// existing PhaseManager enter/exit pipeline — combat logic, shop generation and the result
/// overlay are untouched.
///
/// World offset: at scene load the combat world markers on CombatUXManager are shifted +pageH
/// so the combat page lives above the shop page. When the driver is unavailable (config off,
/// headless/deterministic run) the offset is removed and everything behaves byte-for-byte like
/// before the port.
///
/// Camera ownership: writes go to the camera's PARENT rig ("Camera Man") — MilkShake Shaker
/// owns the camera child's localPosition (same arbitration as ShopUXManager wheel scroll).
/// ShopUXManager scroll is gated by IsTransitioning.
/// </summary>
public class PhaseTransitionDriver : MonoBehaviour
{
	public static PhaseTransitionDriver Me { get; private set; }

	private bool _transitioning;
	private bool _ownsDeckCards;
	private bool _suppressCombatCanvasUI;
	private TransitionTravel _travel;
	// Part D entrance v2: the total Shop→Combat flight wait computed by
	// ScheduleCombatEntranceFlights (shared stagger measured from the scheduling moment).
	private float _entranceFlightTotal;

	/// <summary>Travel direction while a transition coroutine runs (the HUD presenters run their world flights on its edges, fixes 2/3/7).</summary>
	public enum TransitionTravel { None, ToCombat, ToShop }

	/// <summary>True while any transition coroutine runs. Gates shop scroll, phase-change snaps, chrome show.</summary>
	public static bool IsTransitioning => Me != null && Me._transitioning;
	/// <summary>True while the driver has claimed the shop's spawned cards (they must survive ClearSpawnedCards).</summary>
	public static bool OwnsDeckCards => Me != null && Me._ownsDeckCards;
	/// <summary>True while combat canvas UI (HP compare bar) must stay hidden until the camera lands.</summary>
	public static bool SuppressCombatCanvasUI => Me != null && Me._suppressCombatCanvasUI;
	/// <summary>Current travel direction (None = settled). Static so the canvas HUD presenters can read it without a driver reference.</summary>
	public static TransitionTravel Travel => Me != null ? Me._travel : TransitionTravel.None;
	/// <summary>World Y of the two page origins (captured at Awake; 0 before init). Home basis for the HUD world flights.</summary>
	public static float ShopPageY => Me != null ? Me._shopPageY : 0f;
	public static float CombatPageY => Me != null ? Me._combatPageY : 0f;

	private Camera _cam;
	private Transform _rig;
	private PhaseManager _phaseManager;
	private float _pageH;
	private float _shopPageY;
	private float _combatPageY;
	private float _lastShopY;
	private bool _offsetApplied;
	private readonly List<Tween> _tweens = new List<Tween>();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void AutoCreate()
	{
		// Created after every scene Awake/OnEnable so CombatUXManager.me and the camera rig exist.
		if (!ConfigEnabled) return;
		EnsureCreated();
	}

	private static bool ConfigEnabled
	{
		get
		{
			var cfg = PhaseTransitionConfigSO.Me;
			return cfg != null && cfg.enabled;
		}
	}

	public static void EnsureCreated()
	{
		if (Me != null) return;
		var go = new GameObject(nameof(PhaseTransitionDriver));
		go.AddComponent<PhaseTransitionDriver>();
	}

	/// <summary>
	/// Headless/deterministic bypass (load-bearing): combat sims (infinity batch scan, seed
	/// repro, Strategy B) must pay zero transition time and see zero world offset.
	/// </summary>
	public static bool Available
	{
		get
		{
			var cfg = PhaseTransitionConfigSO.Me;
			if (cfg == null || !cfg.enabled) return false;
			if (!cfg.skipInHeadless) return true;
			if (Application.isBatchMode) return false;
			if (TestManager.Me != null && TestManager.Me.overrideCombatSeed != 0) return false;
			if (CombatManager.Me != null)
			{
				var visuals = CombatManager.Me.visuals;
				if (visuals is NullCombatVisuals || visuals is NullCombatVisualsBehaviour) return false;
			}
			return true;
		}
	}

	private void Awake()
	{
		if (Me != null && Me != this)
		{
			Destroy(gameObject);
			return;
		}
		Me = this;
		_cam = Camera.main;
		if (_cam == null) return;
		_rig = _cam.transform.parent != null ? _cam.transform.parent : _cam.transform;
		_pageH = PhaseFlightPlanner.PageHeightWorld(_cam.orthographicSize);
		_shopPageY = _rig.position.y;
		_combatPageY = _shopPageY + _pageH;
		_lastShopY = _shopPageY;
		_phaseManager = FindFirstObjectByType<PhaseManager>();
	}

	private void OnDestroy()
	{
		if (Me == this) Me = null;
		KillAllTweens();
	}

	private void Update()
	{
		bool available = Available;
		if (available != _offsetApplied)
		{
			ApplyWorldOffset(available);
		}
		if (!available || _transitioning || _rig == null || _phaseManager == null || _phaseManager.currentGamePhaseRef == null)
		{
			return;
		}
		// Combat or Result reached without a transition (tutorial path, direct phase flips):
		// keep the camera on the combat page so the offset world and the view stay aligned.
		var phase = _phaseManager.currentGamePhaseRef.Value();
		if (phase == EnumStorage.GamePhase.Combat || phase == EnumStorage.GamePhase.Result)
		{
			if (Mathf.Abs(_rig.position.y - _combatPageY) > 0.001f)
			{
				var pos = _rig.position;
				pos.y = _combatPageY;
				_rig.position = pos;
			}
		}
	}

	/// <summary>Shifts the combat world markers by +/- pageH. Symmetric and flag-idempotent.</summary>
	private void ApplyWorldOffset(bool on)
	{
		var ux = CombatUXManager.me;
		if (ux == null) return;
		float delta = on ? _pageH : -_pageH;
		OffsetMarker(ux.physicalCardDeckPos, delta);
		OffsetMarker(ux.physicalCardRevealPos, delta);
		OffsetMarker(ux.showPos, delta);
		OffsetMarker(ux.gravePosition, delta);
		OffsetMarker(ux.physicalCardNewTempCardPos, delta);
		OffsetMarker(ux.statusEffectConsumePos, delta);
		OffsetMarker(ux.deckFocusTargetPos, delta);
		_offsetApplied = on;
	}

	private static void OffsetMarker(Transform marker, float deltaY)
	{
		if (marker == null) return;
		marker.position += new Vector3(0f, deltaY, 0f);
	}

	/// <summary>
	/// Shop -> Combat entry point (离开商店 button in ShopChrome, Space shortcut in PhaseManager).
	/// Returns true when the driver took over; false = caller runs the legacy direct phase calls.
	/// </summary>
	public static bool RequestShopToCombat(PhaseManager pm)
	{
		if (!Available || pm == null || IsTransitioning) return false;
		EnsureCreated();
		if (Me._rig == null) return false;
		Me.StartCoroutine(Me.ShopToCombatRoutine(pm));
		return true;
	}

	/// <summary>Result -> Shop entry point (Space/click in the Result phase). Same takeover contract.</summary>
	public static bool RequestResultToShop(PhaseManager pm)
	{
		if (!Available || pm == null || IsTransitioning) return false;
		EnsureCreated();
		if (Me._rig == null) return false;
		Me.StartCoroutine(Me.ResultToShopRoutine(pm));
		return true;
	}

	private IEnumerator ShopToCombatRoutine(PhaseManager pm)
	{
		var cfg = PhaseTransitionConfigSO.Me;
		_transitioning = true;
		_travel = TransitionTravel.ToCombat;
		_ownsDeckCards = true;
		// F2 (plan-phase-transition-audit-fixes-2026-10-02): dismiss any enlarge preview BEFORE
		// the phase calls — RestoreCard retargets the card and releases its modal ShopInputGate
		// hold, so no enlarged card lingers on the abandoned page and the gate pair stays
		// balanced. The player-facing Space shortcut was removed (user ruling 2026-10-02) and
		// the button is gate-blocked, so the reachable modal+bypass path left is automation
		// (DeckTester.autoSpace); the dismiss keeps that path clean too.
		if (ShopUXManager.Instance != null) ShopUXManager.Instance.RestoreAllEnlargedCards();
		// 2026-10-02 re-audit fixes 4/7: the chrome band (chips/buttons) is page content and
		// stays visible to scroll away with the camera (fix 4a — ExitShop skips its hide while
		// IsTransitioning). 2026-10-03 handoff seam: the departure mirror hide now lives in the
		// HUD presenters (right after flight.Begin world-locks the canvas copy at the mirror
		// spot), so this coroutine no longer touches the mirrors on departure — hiding here
		// raced the presenters' Update and could blank the avatar/HP for one frame. The
		// Result->Shop hide below stays (mirrors are already off; defensive) and the landing
		// SetMirrorsActive(true) stays as an idempotent safety net behind the flight's
		// onComplete handoff.
		_suppressCombatCanvasUI = true;
		_lastShopY = _rig.position.y;
		ShopInputGate.Block();

		// Phase flips at travel start: shop content stays a page below, combat world content
		// instantiates a page above (off-screen), combat canvas UI stays suppressed until landing.
		pm.ExitingShopPhase();
		pm.EnteringCombatPhase();

		// EnterCombat force-clears the input block, so block AFTER the phase calls. The block
		// holds CombatManager.RevealCards (and with it InstantiateAllPhysicalCards + the Start
		// Card reveal) until the camera lands.
		if (CombatManager.Me != null) CombatManager.Me.BlockInput(this);

		// Borrow the shop player-deck cards as flight dummies (they survived ClearSpawnedCards
		// because OwnsDeckCards was set before the phase calls).
		List<GameObject> dummies = ShopUXManager.Instance != null
			? ShopUXManager.Instance.ReleasePlayerDeckCardsToDriver()
			: new List<GameObject>();

		float dur = cfg != null ? Mathf.Max(0.05f, cfg.transDur) : 0.8f;
		float stagger = cfg != null ? cfg.cardStagger : 0.07f;
		Track(ApplyCfgEase(cfg, _rig.DOMoveY(_combatPageY, dur).SetUpdate(UpdateType.Normal, true)));
		// Part C full-dummy coverage (user request 2026-10-04 "出现的时机和友方卡一样"): the
		// enemy cards + Start Card used to pop in at the landing — presentation clones fly
		// their slots on the SAME stagger schedule. Part D v2 (2026-10-04 plan): the WHOLE
		// schedule (player dummies + clones) is created only once combinedDeckZone exists —
		// the real-layout landing targets need the full future deck count, which GatherDecks
		// assembles 1-2 frames after the phase flip — and each clone spawns AT its flight
		// start, so no resting column ever hangs above the deck.
		_entranceFlightTotal = 0f;
		yield return ScheduleCombatEntranceFlights(dummies, dur, stagger, cfg);

		// VISUAL-FIX(2026-10-02): deck tails of 4+ cards popped into their stack slots mid-arc
		//   Cause:    The landing wait used the demo's fixed 3-card formula (transDur + 2*cardStagger,
		//             PhaseFlightPlanner.TotalDuration(float, float)) while dummy i lands at
		//             i*stagger + transDur; with the shipped config (0.8s / 0.07s) decks above 3
		//             cards destroyed their tail dummies up to 0.65s before landing (12-card deck),
		//             teleporting the real face-down cards into the stack slots (3-card decks hid it).
		//   Affects:  PhaseTransitionDriver.ShopToCombatRoutine (landing wait), PhaseFlightPlanner
		//             (new count-aware TotalDuration overload; the 3-card demo golden is unchanged)
		//   Regress:  Transition with an 8+ card player deck: every dummy visibly lands on its
		//             stack slot before the destroy/swap; a 3-card deck is visually identical to
		//             before. PhaseFlightPlannerTests count goldens (1/3/4/8) stay green.
		//   Related:  plan-phase-transition-audit-fixes-2026-10-02 F3, docs/demo/PhaseTransitionDemo.html:739
		// The flights are created at the end of ScheduleCombatEntranceFlights, so the shared
		// schedule starts exactly here — the count-aware total needs no wall-time subtraction.
		yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, _entranceFlightTotal));

		// Land: release combat progression; RevealCards now instantiates the real physicals
		// (face-down at the same stack slots the dummies landed on) and reveals the Start Card.
		if (CombatManager.Me != null) CombatManager.Me.UnblockInput(this);
		float guard = 0f;
		while (guard < 1.5f)
		{
			var ux = CombatUXManager.me;
			var cm = CombatManager.Me;
			if (ux != null && cm != null && cm.combinedDeckZone != null
				&& ux.physicalCardsInDeck.Count >= cm.combinedDeckZone.Count && cm.combinedDeckZone.Count > 0)
			{
				break;
			}
			guard += Time.unscaledDeltaTime;
			yield return null;
		}
		{
			// Landing-swap probe (entrance diagnostics): the moment the real physicals replace
			// the flying dummies — with the physicals/zone counts at the swap frame.
			var uxSwap = CombatUXManager.me;
			var cmSwap = CombatManager.Me;
			TestManager.Log(string.Format("[PhaseTransition] landing swap: physicals={0} zoneCount={1} t={2:F2}",
				uxSwap != null && uxSwap.physicalCardsInDeck != null ? uxSwap.physicalCardsInDeck.Count : -1,
				cmSwap != null && cmSwap.combinedDeckZone != null ? cmSwap.combinedDeckZone.Count : -1,
				Time.unscaledTime));
		}
		foreach (var dummy in dummies)
		{
			if (dummy != null) Destroy(dummy);
		}

		// Canvas UI release: the HP compare bar activates on the next poll; the enemy HUD has
		// been sliding in since travel start (fix 2 — world flight, demo :724-727).
		_suppressCombatCanvasUI = false;

		_ownsDeckCards = false;
		_travel = TransitionTravel.None;
		_transitioning = false;
		ShopInputGate.Unblock();
	}

	private IEnumerator ResultToShopRoutine(PhaseManager pm)
	{
		var cfg = PhaseTransitionConfigSO.Me;
		_transitioning = true;
		_travel = TransitionTravel.ToShop;
		ShopInputGate.Block();
		if (CombatManager.Me != null) CombatManager.Me.BlockInput(this);

		// Fixes 4/7: the world mirrors hide so the descending canvas HUD is the only
		// avatar/HP copy; chrome + panels are page content and scroll INTO view during the
		// descent (EnterShop -> ShowIfActive runs at travel start, gate removed — fix 4b).
		ShopChrome.SetMirrorsActive(false);

		// Shop content spawns a page below (off-screen); avatar + HP pill glide back to the
		// shop top bar via their presenters' world flights (Travel = ToShop).
		pm.AdvanceFromResultToShop();

		float dur = cfg != null ? Mathf.Max(0.05f, cfg.transDur) : 0.8f;
		Track(ApplyCfgEase(cfg, _rig.DOMoveY(_lastShopY, dur).SetUpdate(UpdateType.Normal, true)));
		yield return new WaitForSecondsRealtime(dur + 0.05f);

		if (CombatManager.Me != null) CombatManager.Me.UnblockInput(this);
		_travel = TransitionTravel.None;
		_transitioning = false;
		ShopInputGate.Unblock();
		// Landing swap: the flying canvas HUD handed back to the phase rules (and hides in a
		// settled shop), so the world Avatar/HpPill mirrors return (fixes 4/7).
		ShopChrome.SetMirrorsActive(true);
	}

	// Part D v2: the real flight targets come from CombatUXManager's future-count layout seam
	// (exact for every mode); Anchor/StepWorld/ZStep/DeckScale only feed the degenerate
	// fallback bake (combinedDeckZone never assembled within the guard). ArcWorld is shared.
	private struct FlightGeometry
	{
		public Vector3 Anchor;
		public Vector3 DeckScale;
		public float StepWorld;
		public float ArcWorld;
		public float ZStep;
	}

	private FlightGeometry BuildFlightGeometry(PhaseTransitionConfigSO cfg)
	{
		var ux = CombatUXManager.me;
		FlightGeometry geo;
		geo.Anchor = ux != null && ux.physicalCardDeckPos != null
			? ux.physicalCardDeckPos.position
			: new Vector3(0f, _combatPageY, 0f);
		geo.DeckScale = ux != null ? ux.physicalCardDeckSize : Vector3.one;
		geo.StepWorld = ux != null ? ux.floatStackStepY * ux.floatStackPxToWorld : 0.14f;
		geo.ArcWorld = PhaseFlightPlanner.PxToWorld(cfg != null ? cfg.cardArcDemoPx : 90f, _pageH);
		geo.ZStep = ux != null ? ux.zOffset : 0.01f;
		return geo;
	}

	/// <summary>
	/// Degenerate-path flight (combinedDeckZone never assembled within the guard window, so
	/// the real-layout targeting is impossible): the borrowed shop cards fly the LEGACY linear
	/// bake (anchor + step*i) so they never strand at their shop positions; no clones exist.
	/// Returns the count-aware flight total for the landing wait.
	/// </summary>
	private float FlyDummiesToCombatStack(List<GameObject> dummies, float dur, float stagger, PhaseTransitionConfigSO cfg)
	{
		var geo = BuildFlightGeometry(cfg);
		for (int i = 0; i < dummies.Count; i++)
		{
			FlyDummyToSlot(dummies[i], i, dummies.Count, dur, cfg, geo, flip: true,
				delay: PhaseFlightPlanner.FlightDelay(i, stagger),
				target: geo.Anchor + new Vector3(0f, geo.StepWorld * i, -geo.ZStep * i),
				finalScale: geo.DeckScale);
		}
		return PhaseFlightPlanner.TotalDuration(dur, stagger, Mathf.Max(1, dummies.Count)) + 0.05f;
	}

	/// <summary>
	/// One card's staggered arc onto its slot (shared by the borrowed shop dummies and the
	/// Part C enemy/Start-Card clones). `target` and `finalScale` are computed by the caller —
	/// real-layout path: CombatUXManager's future-count seam (GetLayoutSlotBasePosition +
	/// GetDeckScaleAtIndex with the full combinedDeckZone count); degenerate fallback: the
	/// legacy linear bake. flip=false skips the mid-arc cover: enemy clones spawn ALREADY
	/// face-down (hidden info — their faces must never show) and the Start Card clone stays
	/// face-up per its spawn rule.
	/// </summary>
	private void FlyDummyToSlot(GameObject dummy, int slotIndex, int fanCount, float dur, PhaseTransitionConfigSO cfg, FlightGeometry geo, bool flip, float delay, Vector3 target, Vector3 finalScale)
	{
		if (dummy == null) return;
		var phys = dummy.GetComponent<CardPhysObjScript>();
		if (phys != null) phys.KillTweens();
		Vector3 from = dummy.transform.position;
		// VISUAL-FIX(2026-10-04): flight dummy stack buried every rim shadow (no inter-card
		//   shadows during the transition; they popped in at the landing swap)
		//   Cause:    The landing z baked the demo's hardcoded -0.01/card step while the
		//             shipped deck layout steps zOffset (scene: 0.5) per card. A card's
		//             PhysicalCardShadow (local z +0.2) only renders in the inter-card gap
		//             when the stack step exceeds 0.2, so at 0.01 every rim landed behind
		//             ~20 neighbour faces and the landing swap teleported the stack onto
		//             the real 0.5-step z model (shadows appeared in one pop).
		//   Affects:  PhaseTransitionDriver.FlyDummyToSlot (landing z only;
		//             y/scale/arc untouched; user paused-scene finding,
		//             plan-transition-entrance-and-shadow-audit-2026-10-04 Part A)
		//   Regress:  Transition with any deck: the flying stack shows the same per-card
		//             rim shadows as the landed combat deck and the landing swap becomes
		//             z-invisible. Headless/bypass paths never reach this code.
		//   Related:  docs/demo/PhaseTransitionDemo.html flyShared branch (demo 3-card
		//             stack made the 0.01 step invisible), RegressionChecklist row 149
		//   (2026-10-05: the target source moved to the real-layout seam — block below —
		//   which carries the same zOffset-per-card step, so this property still holds.)
		// VISUAL-FIX(2026-10-05): entrance flights landed on a linear bake that never matched
		//   the shipped FloatStack layout (plan-transition-entrance-and-shadow-audit-2026-10-04
		//   Part D; latent since the 09-21 port, inherited by the Part C clones)
		//   Cause:    Dummy i flew to anchor + step*i while the real FloatStack slot is
		//             CENTERED (DeckFloatStackLayout.ComputeSlotOffset: y = step*(N-2j-1)/2 +
		//             lift) — for a 6-card deck the Start Card's real slot sat ~3.2 units BELOW
		//             the baked target with the vertical order inverted; the landing swap plus
		//             the opening Start-Card shuffle masked it.
		//   Affects:  PhaseTransitionDriver.FlyDummyToSlot (target/landing scale are now
		//             caller-computed via CombatUXManager.GetLayoutSlotBasePosition(i,
		//             futureCount) + GetDeckScaleAtIndex(i, futureCount) — exact for every
		//             layout mode)
		//   Regress:  Transition with any deck: every flying card lands on the slot the real
		//             layout computes for its index, so the landing swap is position-invisible
		//             and the row-149 z-step property above holds by construction.
		//   Related:  docs/DeckLayouts.md (FloatStack centering), RegressionChecklist row 151
		Vector3 to = target;
		Vector3 apex = PhaseFlightPlanner.ArcApex(from, to, geo.ArcWorld);
		// Demo rot: (i - 1) * 5 degrees for 3 cards; generalize to a centered fan.
		float fanRot = (slotIndex - (fanCount - 1) * 0.5f) * 5f;

		var seq = DOTween.Sequence().SetUpdate(UpdateType.Normal, true);
		seq.AppendInterval(delay);
		// VISUAL-FIX(2026-10-02): card flights ignored the shared ease — Overshoot mode did
		//   not bend the card paths
		//   Cause:    The two move segments hardcoded Ease.OutQuad (up) + Ease.InQuad (down)
		//             while the demo's fly() runs the WHOLE flight on the shared ease
		//             (PhaseTransitionDemo.html:684-691 — animation-level easing re-applies
		//             per keyframe interval, i.e. per segment).
		//   Affects:  PhaseTransitionDriver.FlyDummyToSlot (move segments, scale,
		//             fan rotation — all flight tweens now carry cfg.ApplyEase)
		//   Regress:  Transition with easeMode Overshoot (default): cards visibly overshoot
		//             past the apex and past their landing slot like the camera does; with
		//             Smooth/Linear the flight is a plain eased path. Camera + cards land
		//             together as before.
		//   Related:  docs/PhaseTransition.md open-item 1, PhaseTransitionConfigSO.ApplyEase
		seq.Append(ApplyCfgEase(cfg, dummy.transform.DOMove(apex, dur * 0.5f)));
		seq.Append(ApplyCfgEase(cfg, dummy.transform.DOMove(to, dur * 0.5f)));
		seq.Insert(delay, ApplyCfgEase(cfg, dummy.transform.DOScale(finalScale, dur)));
		seq.Insert(delay, ApplyCfgEase(cfg, dummy.transform.DORotate(new Vector3(0f, 0f, fanRot), dur * 0.5f)));
		seq.Insert(delay + dur * 0.5f, ApplyCfgEase(cfg, dummy.transform.DORotate(Vector3.zero, dur * 0.5f)));
		Track(seq);

		if (flip && phys != null)
		{
			// force=true: the combat-entry shuffle is the never-cover rule's legal cover
			// point (FaceDownFlipSystem); the dummies are destroyed after the landing swap,
			// so no ClearRevealedMemory is needed.
			// VISUAL-FIX(2026-10-02): mid-arc cover played the animated scaleX flip while the
			//   demo swaps to the card back INSTANTLY at the apex (is-back class toggle,
			//   PhaseTransitionDemo.html:692-694) — made instant (fix 6).
			// VISUAL-FIX(2026-10-04): animated squash flip restored (user ruling — the instant
			//   swap read as a hard pop; supersedes the 2026-10-02 fix 6 demo-fidelity ruling)
			//   Cause:    SetFaceUp(false, animated:false) at delay + transDur/2 replaced the
			//             face with the back in a single frame, mid-flight.
			//   Affects:  PhaseTransitionDriver.FlyDummyToSlot (flip call + schedule),
			//             PhaseFlightPlanner.FlipStart (new: apex-centered start + landing clamp)
			//   Regress:  Transition: each dummy plays the scaleX squash flip centered on its
			//             arc apex (pinch = back first appears at the demo cover point) and
			//             the back is fully open BEFORE the card lands. FlipRoot scaleX never
			//             fights the flight's root-transform tweens (BuildFlipRoot separation);
			//             the flip runs unscaled-time like every transition tween; the
			//             never-cover force path is untouched.
			//   Related:  docs/PhaseTransition.md deviations item 6, docs/FaceDownFlipSystem.md
			// Schedule with the combat-scaled duration (the flip's own GetCombatScaledDuration
			// applies the same scaler once the phase reads Combat, which it does from travel
			// start): the scheduled window is always >= the real flip, so the back can only
			// open EARLY relative to the clamp, never past the landing.
			float flipDur = CombatAnimationSpeed.ScaleDuration(phys.flipDuration);
			float flipStart = PhaseFlightPlanner.FlipStart(delay, dur, flipDur);
			var captured = phys;
			Track(DOVirtual.DelayedCall(flipStart, () =>
			{
				if (captured != null) captured.SetFaceUp(false, true, true);
			}, true));
		}
	}

	/// <summary>
	/// Part D entrance v2 (supersedes the Part C spawn-now cover; plan
	/// plan-transition-entrance-and-shadow-audit-2026-10-04.md): waits for CombatManager's
	/// GatherDecks (a state machine on Update — ready 1-2 frames after the phase flip), then
	/// schedules the WHOLE entrance on the shared stagger with REAL layout landing targets
	/// (CombatUXManager's future-count seam — the legacy linear bake mis-landed FloatStack by
	/// up to ~3 units with the vertical order inverted). Player section: the borrowed shop
	/// dummies fly to their future slots on the shared stagger. Uncovered tail (enemy cards +
	/// Start Card): one presentation clone per slot (same prefab selection as
	/// InstantiateAllPhysicalCards), spawned AT its flight start by a DOVirtual.DelayedCall —
	/// the clone instantiates at spawn height and its flight tween (zero extra delay) begins
	/// the same tick, so the motionless column above the deck never exists. Sets
	/// _entranceFlightTotal for the landing wait. Degenerate zone timeout: the dummies still
	/// fly the legacy linear bake (they must never strand at shop positions); no clones — the
	/// real cards land the legacy way. Presentation-only: the only combat read is the
	/// already-populated combinedDeckZone — RevealCards stays blocked until landing. Slot
	/// alignment: combinedDeckZone[i] IS the card InstantiateAllPhysicalCards places on slot i,
	/// so flight slots match the real landing slots by construction.
	/// </summary>
	private IEnumerator ScheduleCombatEntranceFlights(List<GameObject> dummies, float dur, float stagger, PhaseTransitionConfigSO cfg)
	{
		float guard = 0f;
		while (guard < 0.5f)
		{
			var cm = CombatManager.Me;
			if (cm != null && cm.combinedDeckZone != null && cm.combinedDeckZone.Count > 0) break;
			guard += Time.unscaledDeltaTime;
			yield return null;
		}
		var combat = CombatManager.Me;
		var ux = CombatUXManager.me;
		if (combat == null || combat.combinedDeckZone == null || combat.combinedDeckZone.Count == 0 || ux == null)
		{
			TestManager.Log("[PhaseTransition] combinedDeckZone not ready within guard — legacy linear bake fallback (no clones)");
			_entranceFlightTotal = FlyDummiesToCombatStack(dummies, dur, stagger, cfg);
			yield break; // no deck to target: legacy bake for the dummies, real cards land legacy
		}

		int deckCount = combat.combinedDeckZone.Count;
		var geo = BuildFlightGeometry(cfg);
		float pageH = PhaseFlightPlanner.PageHeightWorld(Camera.main != null ? Camera.main.orthographicSize : 6f);
		float enemySlide = PhaseFlightPlanner.PxToWorld(cfg != null ? cfg.enemyCardSlideDemoPx : 400f, pageH);
		float startSlide = PhaseFlightPlanner.PxToWorld(cfg != null ? cfg.startCardSlideDemoPx : 400f, pageH);

		// Entrance diagnostics (plan 10-04 Part D verification round 1): the user reported the
		// enemy/Start-Card fly-in as invisible ("appear too low") — these probes log each card's
		// spawn/target against the camera viewport at the spawn moment. Toggle: TestManager
		// logVisualSync ([PhaseTransition] routes there, TestManager.InferCategory).
		float camY = Camera.main != null ? Camera.main.transform.position.y : 0f;
		float ortho = Camera.main != null ? Camera.main.orthographicSize : 0f;
		TestManager.Log(string.Format(
			"[PhaseTransition] entrance schedule: playerDummies={0} deckCount={1} dur={2:F2} stagger={3:F3} combatPageY={4:F2} camY={5:F2} landingViewportY=[{6:F2},{7:F2}]",
			dummies.Count, deckCount, dur, stagger, _combatPageY, camY, _combatPageY - ortho, _combatPageY + ortho));

		// dummies.Count = the borrowed player-deck cards = the player section (deck order), so
		// list index == combinedDeckZone index for the player slots and the uncovered tail.
		for (int i = 0; i < dummies.Count; i++)
		{
			Vector3 dummyTarget = ux.GetLayoutSlotBasePosition(i, deckCount);
			float dummyDelay = PhaseFlightPlanner.FlightDelay(i, stagger);
			TestManager.Log(string.Format("[PhaseTransition] dummy {0} from={1} target={2} delay={3:F2}",
				i, dummies[i].transform.position, dummyTarget, dummyDelay));
			FlyDummyToSlot(dummies[i], i, dummies.Count, dur, cfg, geo, flip: true,
				delay: dummyDelay,
				target: dummyTarget,
				finalScale: ux.GetDeckScaleAtIndex(i, deckCount));
		}

		// VISUAL-FIX(2026-10-05): the Part C clones rested in a motionless column above the deck
		//   Cause:    Every clone spawned the moment combinedDeckZone appeared (~t=0.05s) but
		//             flew at delay = i * stagger — up to ~1s of stacked, stationary clones
		//             (15-card deck) hanging over the deck anchor until their own launch.
		//   Affects:  PhaseTransitionDriver.ScheduleCombatEntranceFlights (per-clone
		//             DOVirtual.DelayedCall spawn; the clone's flight tween runs at delay 0)
		//   Regress:  Transition with any enemy/start cards: each clone appears at its spawn
		//             height exactly when its flight starts and arcs in immediately — no
		//             resting column exists at any time. Player dummies unchanged (their
		//             stagger lives inside the tween as before). Headless/bypass never runs.
		//   Related:  plan-transition-entrance-and-shadow-audit-2026-10-04.md Part D item 2,
		//             RegressionChecklist row 151
		for (int i = dummies.Count; i < deckCount; i++)
		{
			var card = combat.combinedDeckZone[i];
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			bool isStart = cardScript != null && cardScript.isStartCard;
			GameObject prefab = ux.physicalCardPrefab;
			if (cardScript != null)
			{
				if (isStart) prefab = ux.startCardPhysicalPrefab != null ? ux.startCardPhysicalPrefab : prefab;
				else if (cardScript.isMinion) prefab = ux.minionPhysicalPrefab != null ? ux.minionPhysicalPrefab : prefab;
			}
			if (prefab == null) continue; // no prefab wired: this slot stays landing-pop (legacy)

			float slideY = isStart ? startSlide : enemySlide;
			int index = i;
			Track(DOVirtual.DelayedCall(PhaseFlightPlanner.FlightDelay(i, stagger), () =>
			{
				SpawnEntranceClone(card, index, prefab, slideY, deckCount, dummies, dur, cfg, geo, ux);
			}, true));
		}

		_entranceFlightTotal = PhaseFlightPlanner.TotalDuration(dur, stagger, Mathf.Max(deckCount, dummies.Count)) + 0.05f;
	}

	/// <summary>
	/// One entrance clone (delayed-spawn body): instantiates at spawn height above the slot's
	/// real layout position, wires cardImRepresenting (enemy orange back + opponent art come
	/// free per frame), applies the face rules (enemy face-down already, Start Card keeps its
	/// face) and flies onto the shared schedule with zero extra delay.
	/// </summary>
	private void SpawnEntranceClone(GameObject card, int slotIndex, GameObject prefab, float slideY, int deckCount, List<GameObject> dummies, float dur, PhaseTransitionConfigSO cfg, FlightGeometry geo, CombatUXManager ux)
	{
		Vector3 target = ux.GetLayoutSlotBasePosition(slotIndex, deckCount);
		Vector3 finalScale = ux.GetDeckScaleAtIndex(slotIndex, deckCount);
		Vector3 spawnPos = target + new Vector3(0f, slideY, 0f);
		// Spawn-moment probe (see the entrance diagnostics note in ScheduleCombatEntranceFlights).
		float camY = Camera.main != null ? Camera.main.transform.position.y : 0f;
		float ortho = Camera.main != null ? Camera.main.orthographicSize : 0f;
		TestManager.Log(string.Format(
			"[PhaseTransition] clone {0} '{1}' spawn={2} target={3} slideY={4:F2} t={5:F2} camY={6:F2} viewportY=[{7:F2},{8:F2}] spawnVisible={9} targetVisible={10}",
			slotIndex, card.name, spawnPos, target, slideY, Time.unscaledTime, camY, camY - ortho, camY + ortho,
			spawnPos.y >= camY - ortho && spawnPos.y <= camY + ortho,
			target.y >= camY - ortho && target.y <= camY + ortho));
		GameObject clone = Instantiate(prefab, spawnPos, Quaternion.identity);
		clone.name = "[entrance dummy] " + card.name;
		var phys = clone.GetComponent<CardPhysObjScript>();
		if (phys == null)
		{
			Destroy(clone);
			return;
		}
		var cardScript = card.GetComponent<CardScript>();
		phys.cardImRepresenting = cardScript; // drives ApplyBackColor (enemy orange) / ApplyColor per frame
		phys.SetScaleImmediate(finalScale);
		if (cardScript == null || !cardScript.isStartCard) phys.SetFaceUp(false, false);
		dummies.Add(clone);
		FlyDummyToSlot(clone, slotIndex, deckCount, dur, cfg, geo, flip: false,
			delay: 0f, target: target, finalScale: finalScale);
	}

	private void Track(Tween tween)
	{
		if (tween != null) _tweens.Add(tween);
	}

	private void KillAllTweens()
	{
		foreach (var tween in _tweens)
		{
			if (tween != null && tween.IsActive()) tween.Kill();
		}
		_tweens.Clear();
	}

	private static Tween ApplyCfgEase(PhaseTransitionConfigSO cfg, Tween tween)
	{
		return cfg != null ? cfg.ApplyEase(tween) : tween.SetEase(Ease.OutBack);
	}
}
