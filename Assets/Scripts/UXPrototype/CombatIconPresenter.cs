using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the PlayerIcon / EnemyIcon HUD elements per game phase.
/// Player icon: Combat, Shop AND Result (2026-09-19: the shop top bar reused the combat
/// avatar + username block, repositioned via ShopTopBarLayout; 2026-09-21 phase transition:
/// avatar + HP stay visible through the Result overlay, demo PhaseTransitionDemo.html:27-28;
/// the combat anchor and scale are captured at Awake and restored on combat entry).
/// 2026-09-21 world scroll (plan-shop-topbar-world-scroll-2026-09-21 §3.3): in a SETTLED
/// shop the canvas icon hides — the world copy lives in the ShopHudPage chrome and scrolls
/// with the page; during a driver transition (IsTransitioning) the canvas icon stays visible
/// and glides between its shop/combat homes exactly as before (shared-element flight).
/// Enemy icon: Combat + Result (2026-10-02 re-audit fix 3 — the demo keeps the enemy HUD
/// behind the result overlay) + while a travel world-flies it.
/// 2026-10-02 re-audit fixes 2/3/7 (docs/PhaseTransition.md): while the driver travels, both
/// icons fly in WORLD space (HudWorldFlight) between the shop chrome mirror spot and their
/// combat anchors — no scale tween, and the camera's overshoot carries them like the demo.
/// The enemy icon enters with a full-transDur counter-direction slide from enemySlide above,
/// started together with the camera travel (demo flyShared, :704-727).
/// Same GamePhase-polled SetActive convention as CombatHPBarPresenter and
/// HPNumericDisplay. Pure presentation; no game-logic changes.
/// Name label under the icon shows PlayerIdentity.Username ("???" when unset).
/// The label is a child of the icon, so the phase toggles hide it with the parent;
/// the text re-polls per frame with a diff guard, matching the polling convention.
/// </summary>
public class CombatIconPresenter : MonoBehaviour
{
	[Header("Wiring")]
	public GameObject playerIcon;
	public GameObject enemyIcon;
	public GamePhaseSO gamePhaseRef;

	[Header("Name labels (optional)")]
	public TMP_Text playerNameLabel;
	public TMP_Text enemyNameLabel;

	private const string UnknownName = "???";

	private EnumStorage.GamePhase _lastPhase = EnumStorage.GamePhase.Result;
	private string _lastPlayerName;
	private string _lastEnemyName;
	// Init true (never the boot state) so the first Update always applies, mirroring the
	// _lastPhase = Result trick below.
	private bool _lastTransitioning = true;
	private PhaseTransitionDriver.TransitionTravel _lastTravel = PhaseTransitionDriver.TransitionTravel.None;

	private RectTransform _playerIconRt;
	private RectTransform _enemyIconRt;
	private Canvas _canvas;
	private Vector2 _combatAnchoredPos;
	private Vector3 _combatScale;
	private Vector2 _enemyCombatAnchoredPos;
	private bool _lastSuppressed;
	private Tween _glideTween;
	private Tween _scaleTween;
	// World flights (fixes 2/3/7): one per icon, created lazily at the first travel edge.
	private HudWorldFlight _playerFlight;
	private HudWorldFlight _enemyFlight;
	// 2026-10-03 handoff seam: same contract as HPNumericDisplayHorizontal — the flight
	// completes INTO the world mirror (scale-matched), then swaps copies in one frame.
	private bool _handedOffToMirror;
	private const float HandoffScaleWindow = 0.35f; // keep in sync with HPNumericDisplayHorizontal.HandoffScaleWindow

	private void Awake()
	{
		if (playerIcon == null || enemyIcon == null || gamePhaseRef == null)
		{
			Debug.LogError("[CombatIconPresenter] Missing serialized reference(s), disabling.");
			enabled = false;
			return;
		}
		// VISUAL-FIX(2026-07-22): Player/Enemy icons stay visible outside the Combat phase
		//   Cause:    PlayerIcon/EnemyIcon were scene-only objects with no script ever
		//             toggling them, so they rendered during Shop/Result phases too.
		//   Affects:  PlayerIcon/EnemyIcon under Combat Canvas
		//   Regress:  Enter Shop phase: ENEMY icon must be inactive; the PLAYER icon follows
		//             the phase rule as of 2026-09-21 (hidden in a settled shop — the world
		//             copy in ShopPageHud shows; visible again during driver transitions and
		//             in Combat/Result); re-enter Combat: both icons at their original
		//             anchored positions/scale.
		playerIcon.SetActive(false);
		enemyIcon.SetActive(false);
		_playerIconRt = playerIcon.transform as RectTransform;
		if (_playerIconRt != null)
		{
			_combatAnchoredPos = _playerIconRt.anchoredPosition;
			_combatScale = _playerIconRt.localScale;
		}
		_enemyIconRt = enemyIcon.transform as RectTransform;
		if (_enemyIconRt != null)
		{
			_enemyCombatAnchoredPos = _enemyIconRt.anchoredPosition;
		}
		_canvas = playerIcon.GetComponentInParent<Canvas>();
		// Combat/shop input is click-driven: no label graphic may intercept raycasts.
		if (playerNameLabel != null) playerNameLabel.raycastTarget = false;
		if (enemyNameLabel != null) enemyNameLabel.raycastTarget = false;
	}

	private void Update()
	{
		EnumStorage.GamePhase phase = gamePhaseRef.Value();
		bool suppressed = PhaseTransitionDriver.SuppressCombatCanvasUI;
		// VISUAL-FIX(2026-09-21): landing in the shop left the canvas avatar stacked over the
		//   new world copy
		//   Cause:    Visibility only re-applied on phase/suppress flips; the transition
		//             landing flips IsTransitioning false while the phase stays Shop, so the
		//             settled-shop handoff rule (plan-shop-topbar-world-scroll-2026-09-21
		//             §3.3) never re-ran and the canvas icon stayed visible over ShopPageHud.
		//   Affects:  CombatIconPresenter player side (shop phase).
		//   Regress:  Result -> Shop landing: canvas avatar hides on the landing poll, the
		//             world copy (ShopPageHud) is the only avatar visible; wheel scroll takes
		//             it away with the page. 离开商店 / Space from a settled shop: the canvas
		//             avatar re-activates next Update and glides shop anchor -> combat anchor
		//             as before. Driver bypass (config off / headless): same-frame hard cut.
		bool transitioning = PhaseTransitionDriver.IsTransitioning;
		var travel = PhaseTransitionDriver.Travel;
		bool travelEdge = travel != _lastTravel;
		if (phase != _lastPhase || suppressed != _lastSuppressed || transitioning != _lastTransitioning || travelEdge)
		{
			if (travelEdge) OnTravelEdge(travel);
			ApplyPhase(phase);
			_lastPhase = phase;
			_lastSuppressed = suppressed;
			_lastTransitioning = transitioning;
			_lastTravel = travel;
		}
		if (phase == EnumStorage.GamePhase.Combat)
		{
			RefreshPlayerNameLabel();
			RefreshEnemyNameLabel();
		}
		else if (phase == EnumStorage.GamePhase.Shop)
		{
			RefreshPlayerNameLabel();
		}
	}

	private void ApplyPhase(EnumStorage.GamePhase phase)
	{
		bool inCombat = phase == EnumStorage.GamePhase.Combat;
		bool inShop = phase == EnumStorage.GamePhase.Shop;
		bool inResult = phase == EnumStorage.GamePhase.Result;
		bool suppressed = PhaseTransitionDriver.SuppressCombatCanvasUI;
		// 2026-09-21 rule (demo :27-28): player avatar stays visible through the Result phase.
		// 2026-09-21 world-scroll handoff: in a SETTLED shop the canvas avatar hides (the
		// world copy in the chrome shows); during a driver transition it stays visible and
		// world-flies. Placement writes below keep running while hidden, so the hidden icon
		// parks at the shop anchor and the next shop -> combat flight starts from the right spot.
		playerIcon.SetActive(inCombat || inResult || (inShop && PhaseTransitionDriver.IsTransitioning && !_handedOffToMirror));
		// 2026-10-02 re-audit fix 3: the enemy HUD stays visible through the Result phase
		// (demo keeps it behind the overlay) and while a travel world-flies it (slide-in on
		// Shop->Combat, slide-up-out on Result->Shop); otherwise Combat-only as before.
		var travel = PhaseTransitionDriver.Travel;
		bool enemyVisible = travel != PhaseTransitionDriver.TransitionTravel.None
			|| inResult
			|| (inCombat && !suppressed);
		enemyIcon.SetActive(enemyVisible);
		if (_playerIconRt == null)
		{
			return;
		}
		// While a travel owns the icon (world flight, fixes 3/7) the anchored placement
		// stands down — the flight writes .position and anchoredPosition tracks it.
		if (travel != PhaseTransitionDriver.TransitionTravel.None
			&& _playerFlight != null && _playerFlight.Active)
		{
			return;
		}
		Vector2 targetPos = inShop
			? ShopTopBarLayout.ShopAnchorAvatar(_canvas)
			: _combatAnchoredPos;
		Vector3 targetScale = inShop ? Vector3.one * ShopTopBarLayout.ShopScaleAvatar : _combatScale;
		var cfg = PhaseTransitionConfigSO.Me;
		if (PhaseTransitionDriver.IsTransitioning && cfg != null)
		{
			// Fallback glide (no chrome mirrors -> no world flight, headless corner): the
			// pre-fix anchored glide between homes.
			KillTween(ref _glideTween);
			KillTween(ref _scaleTween);
			_glideTween = cfg.ApplyEase(_playerIconRt.DOAnchorPos(targetPos, cfg.transDur).SetUpdate(UpdateType.Normal, true));
			_scaleTween = cfg.ApplyEase(_playerIconRt.DOScale(targetScale, cfg.transDur).SetUpdate(UpdateType.Normal, true));
		}
		else
		{
			KillTween(ref _glideTween);
			KillTween(ref _scaleTween);
			_playerIconRt.anchoredPosition = targetPos;
			_playerIconRt.localScale = targetScale;
		}
	}

	/// <summary>
	/// Travel edges drive the world flights (fixes 2/3/7): the player avatar detaches from
	/// the shop top bar (world mirror spot) and flies to its combat anchor and back; the
	/// enemy icon slides in from / out to enemySlide above its combat home, full transDur,
	/// together with the camera travel. Without the chrome mirrors (headless corner) no
	/// flight starts and the legacy anchored placement/glide applies.
	/// </summary>
	// VISUAL-FIX(2026-10-03): the enemy icon (latently the player icon too) landed short of
	//   its combat anchor after the Shop->Combat flight — stranded mid-screen for the whole
	//   combat (user report; [CiP-DIAG] trace: edge frame rigY=2.66 vs canvasTopY=0.00).
	//   Cause:    The flight home was derived from the CAMERA RIG's Y (HudHomeAtPage(snap,
	//             CombatPageY, RigY)), but the Screen Space Camera canvas root lags the rig
	//             within the travel-start frame: the rig tween's first step had already
	//             moved (2.66) while the canvas still sat at the shop page (0.00). The
	//             formula credited the rig's in-flight offset to the canvas, landing the
	//             icon short by exactly that lag. The enemy HP pill escaped only by script
	//             execution order (its Update ran before the tween's first evaluation) —
	//             same latent bug in HPNumericDisplayHorizontal.
	//   Affects:  CombatIconPresenter + HPNumericDisplayHorizontal OnTravelEdge flight
	//             homes; PhaseFlightPlanner.HudHomeAtPage's third arg is now the carrying
	//             canvas plane's own Y (renamed rigY -> planeY).
	//   Regress:  Shop -> combat: enemy icon + HP pill land exactly on their authored
	//             combat anchors (icon next to the pill, top-right row) and the player
	//             icon + pill on theirs, regardless of when each Update processed the
	//             travel edge; Result -> shop flight start points unchanged.
	//   Related:  docs/PhaseTransition.md "HUD world flights", [CiP-DIAG]/[HPN-DIAG] probes
	private void OnTravelEdge(PhaseTransitionDriver.TransitionTravel travel)
	{
		var cfg = PhaseTransitionConfigSO.Me;
		if (cfg == null) return;
		switch (travel)
		{
			case PhaseTransitionDriver.TransitionTravel.ToCombat:
			{
				if (_playerIconRt != null && ShopChrome.TryGetAvatarWorldCenter(out Vector3 shopHome))
				{
					_handedOffToMirror = false;
					Vector3 to = PhaseFlightPlanner.HudHomeAtPage(SnapPlayerToCombatAnchor(), PhaseTransitionDriver.CombatPageY, CanvasPlaneY);
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
				if (_enemyIconRt != null)
				{
					Vector3 home = PhaseFlightPlanner.HudHomeAtPage(SnapEnemyToCombatAnchor(), PhaseTransitionDriver.CombatPageY, CanvasPlaneY);
					Vector3 from = home + Vector3.up * EnemySlideWorld(cfg);
					EnsureFlight(ref _enemyFlight, _enemyIconRt).Begin(from, home, cfg.transDur, cfg);
				}
				break;
			}
			case PhaseTransitionDriver.TransitionTravel.ToShop:
			{
				if (_playerIconRt != null && ShopChrome.TryGetAvatarWorldCenter(out Vector3 shopHome))
				{
					_handedOffToMirror = false;
					Vector3 from = PhaseFlightPlanner.HudHomeAtPage(_playerIconRt.position, PhaseTransitionDriver.CombatPageY, CanvasPlaneY);
					_playerIconRt.localScale = _combatScale;
					_scaleTween = _playerIconRt.DOScale(Vector3.one * ShopTopBarLayout.ShopScaleAvatar, cfg.transDur * HandoffScaleWindow)
						.SetDelay(cfg.transDur * (1f - HandoffScaleWindow)).SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					var capturedRt = _playerIconRt;
					EnsureFlight(ref _playerFlight, _playerIconRt).Begin(from, shopHome, cfg.transDur, cfg, () =>
					{
						// Same-frame swap at flight end — mirror shows, canvas copy hides
						// (see HPNumericDisplayHorizontal, 2026-10-03).
						ShopChrome.SetMirrorsActive(true);
						_handedOffToMirror = true;
						if (capturedRt != null) capturedRt.gameObject.SetActive(false);
					});
				}
				if (_enemyIconRt != null)
				{
					Vector3 home = PhaseFlightPlanner.HudHomeAtPage(_enemyIconRt.position, PhaseTransitionDriver.CombatPageY, CanvasPlaneY);
					EnsureFlight(ref _enemyFlight, _enemyIconRt).Begin(home, home + Vector3.up * EnemySlideWorld(cfg), cfg.transDur, cfg);
				}
				break;
			}
			default:
				_handedOffToMirror = false;
				if (_playerFlight != null) _playerFlight.Kill();
				if (_enemyFlight != null) _enemyFlight.Kill();
				break;
		}
	}

	private HudWorldFlight EnsureFlight(ref HudWorldFlight flight, RectTransform rt)
	{
		if (flight == null) flight = new HudWorldFlight(rt, _canvas);
		return flight;
	}

	/// <summary>
	/// The carrying canvas root's current world Y — the flight-home basis (HudHomeAtPage).
	/// NOT the camera rig: the canvas root lags the rig within the travel-start frame
	/// (VISUAL-FIX(2026-10-03)); the rig Y remains only as the no-canvas fallback.
	/// </summary>
	private float CanvasPlaneY => _canvas != null ? _canvas.transform.position.y : HudWorldFlight.RigY;

	/// <summary>Snaps the player icon to its combat anchor POSITION (kill glide tweens; the home math needs it) and returns its world position. Scale is no longer written here (2026-10-03 handoff seam): the player flight endpoints own scale.</summary>
	private Vector3 SnapPlayerToCombatAnchor()
	{
		KillTween(ref _glideTween);
		KillTween(ref _scaleTween);
		_playerIconRt.anchoredPosition = _combatAnchoredPos;
		return _playerIconRt.position;
	}

	/// <summary>Snaps the enemy icon back to its combat anchor (a shop-park may have left it at the shop anchor) and returns its world position.</summary>
	private Vector3 SnapEnemyToCombatAnchor()
	{
		_enemyIconRt.anchoredPosition = _enemyCombatAnchoredPos;
		return _enemyIconRt.position;
	}

	/// <summary>Enemy HUD slide distance in world units (demo enemySlide 140 px on the 740 px page).</summary>
	private static float EnemySlideWorld(PhaseTransitionConfigSO cfg)
	{
		return PhaseFlightPlanner.PxToWorld(cfg.enemySlideDemoPx, PhaseFlightPlanner.PageHeightWorld(ShopTopBarLayout.MainOrthoSize));
	}

	private void LateUpdate()
	{
		// World-flight re-projection runs after every Update placement write (and after the
		// camera's own motion), so the rendered pose is the world-locked one.
		if (_playerFlight != null) _playerFlight.Tick();
		if (_enemyFlight != null) _enemyFlight.Tick();
	}

	private static void KillTween(ref Tween tween)
	{
		if (tween != null && tween.IsActive()) tween.Kill();
		tween = null;
	}

	// Diff-guarded writes so the TMP mesh only rebuilds on an actual change; the
	// enemy name can arrive shortly after the phase switch (ghost deck injection),
	// so polling here instead of a one-shot entry write is the safe pattern.
	private void RefreshPlayerNameLabel()
	{
		string playerName = PlayerIdentity.Username;
		if (string.IsNullOrEmpty(playerName))
		{
			playerName = UnknownName;
		}
		if (_lastPlayerName != playerName)
		{
			_lastPlayerName = playerName;
			if (playerNameLabel != null)
			{
				playerNameLabel.text = playerName;
				playerNameLabel.color = GameColorPalette.IconNameLabelColor;
			}
		}
	}

	private void RefreshEnemyNameLabel()
	{
		string enemyName = OpponentDeckCache.Current != null ? OpponentDeckCache.Current.username : null;
		if (string.IsNullOrEmpty(enemyName))
		{
			enemyName = UnknownName;
		}
		if (_lastEnemyName != enemyName)
		{
			_lastEnemyName = enemyName;
			if (enemyNameLabel != null)
			{
				enemyNameLabel.text = enemyName;
				enemyNameLabel.color = GameColorPalette.IconNameLabelColor;
			}
		}
	}
}
