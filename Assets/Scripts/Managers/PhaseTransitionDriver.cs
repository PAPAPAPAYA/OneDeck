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
	private bool _enemyEntrancePending;

	/// <summary>True while any transition coroutine runs. Gates shop scroll, phase-change snaps, chrome show.</summary>
	public static bool IsTransitioning => Me != null && Me._transitioning;
	/// <summary>True while the driver has claimed the shop's spawned cards (they must survive ClearSpawnedCards).</summary>
	public static bool OwnsDeckCards => Me != null && Me._ownsDeckCards;
	/// <summary>True while combat canvas UI (HP bar, enemy HUD) must stay hidden despite the Combat phase.</summary>
	public static bool SuppressCombatCanvasUI => Me != null && Me._suppressCombatCanvasUI;
	/// <summary>Consumed by the enemy HUD presenters on their next activation: play the slide-in entrance.</summary>
	public static bool EnemyEntrancePending => Me != null && Me._enemyEntrancePending;

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
		_ownsDeckCards = true;
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
		FlyDummiesToCombatStack(dummies, dur, stagger, cfg);

		yield return new WaitForSecondsRealtime(PhaseFlightPlanner.TotalDuration(dur, stagger) + 0.05f);

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
		foreach (var dummy in dummies)
		{
			if (dummy != null) Destroy(dummy);
		}

		// Canvas UI appears: HP bar activates; enemy HUD slides DOWN in (counter-direction, demo :724-727).
		_enemyEntrancePending = true;
		_suppressCombatCanvasUI = false;
		yield return null;
		yield return null;
		_enemyEntrancePending = false;

		_ownsDeckCards = false;
		_transitioning = false;
		ShopInputGate.Unblock();
	}

	private IEnumerator ResultToShopRoutine(PhaseManager pm)
	{
		var cfg = PhaseTransitionConfigSO.Me;
		_transitioning = true;
		ShopInputGate.Block();
		if (CombatManager.Me != null) CombatManager.Me.BlockInput(this);

		// Shop content spawns a page below (off-screen); avatar + HP pill glide back to the
		// shop top bar via their presenters (IsTransitioning = tween instead of snap).
		pm.AdvanceFromResultToShop();

		float dur = cfg != null ? Mathf.Max(0.05f, cfg.transDur) : 0.8f;
		Track(ApplyCfgEase(cfg, _rig.DOMoveY(_lastShopY, dur).SetUpdate(UpdateType.Normal, true)));
		yield return new WaitForSecondsRealtime(dur + 0.05f);

		if (CombatManager.Me != null) CombatManager.Me.UnblockInput(this);
		_transitioning = false;
		ShopInputGate.Unblock();
		// Chrome show was gated by IsTransitioning during the travel; reveal it on landing.
		ShopChrome.ShowIfActive();
	}

	/// <summary>
	/// Arc + stagger + mid-flight face-down flip of the borrowed shop deck cards (demo flyShared
	/// cards branch, :713-723). Dummy i lands on stack slot i above the deck anchor; the real
	/// combat physicals pop into the same slots at landing, so destroying the dummies is invisible.
	/// </summary>
	private void FlyDummiesToCombatStack(List<GameObject> dummies, float dur, float stagger, PhaseTransitionConfigSO cfg)
	{
		var ux = CombatUXManager.me;
		Vector3 anchor = ux != null && ux.physicalCardDeckPos != null
			? ux.physicalCardDeckPos.position
			: new Vector3(0f, _combatPageY, 0f);
		Vector3 deckScale = ux != null ? ux.physicalCardDeckSize : Vector3.one;
		float stepWorld = ux != null ? ux.floatStackStepY * ux.floatStackPxToWorld : 0.14f;
		float arcWorld = PhaseFlightPlanner.PxToWorld(cfg != null ? cfg.cardArcDemoPx : 90f, _pageH);

		for (int i = 0; i < dummies.Count; i++)
		{
			var dummy = dummies[i];
			if (dummy == null) continue;
			var phys = dummy.GetComponent<CardPhysObjScript>();
			if (phys != null) phys.KillTweens();
			float delay = PhaseFlightPlanner.FlightDelay(i, stagger);
			Vector3 from = dummy.transform.position;
			Vector3 to = anchor + new Vector3(0f, stepWorld * i, -0.01f * i);
			Vector3 apex = PhaseFlightPlanner.ArcApex(from, to, arcWorld);
			// Demo rot: (i - 1) * 5 degrees for 3 cards; generalize to a centered fan.
			float fanRot = (i - (dummies.Count - 1) * 0.5f) * 5f;

			var seq = DOTween.Sequence().SetUpdate(UpdateType.Normal, true);
			seq.AppendInterval(delay);
			seq.Append(dummy.transform.DOMove(apex, dur * 0.5f).SetEase(Ease.OutQuad));
			seq.Append(dummy.transform.DOMove(to, dur * 0.5f).SetEase(Ease.InQuad));
			seq.Insert(delay, dummy.transform.DOScale(deckScale, dur));
			seq.Insert(delay, dummy.transform.DORotate(new Vector3(0f, 0f, fanRot), dur * 0.5f));
			seq.Insert(delay + dur * 0.5f, dummy.transform.DORotate(Vector3.zero, dur * 0.5f));
			Track(seq);

			if (phys != null)
			{
				// flipAtMid (demo :692): cover at the arc apex. force=true: the combat-entry
				// shuffle is the never-cover rule's legal cover point (FaceDownFlipSystem).
				float flipAt = PhaseFlightPlanner.FlipTime(delay, dur);
				var captured = phys;
				Track(DOVirtual.DelayedCall(flipAt, () =>
				{
					if (captured != null) captured.SetFaceUp(false, true, true);
				}, true));
			}
		}
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
