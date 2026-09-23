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
/// shop the canvas icon hides — the world copy lives in ShopPageHud and scrolls with the
/// page; during a driver transition (IsTransitioning) the canvas icon stays visible and
/// glides between its shop/combat homes exactly as before (shared-element flight).
/// Enemy icon: Combat only (suppressed while the transition driver keeps canvas UI hidden).
/// During a driver transition the per-phase re-anchor becomes a glide tween instead of a snap,
/// and the enemy icon enters with a counter-direction slide (demo flyShared, :704-727).
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

	private RectTransform _playerIconRt;
	private RectTransform _enemyIconRt;
	private Canvas _canvas;
	private Vector2 _combatAnchoredPos;
	private Vector3 _combatScale;
	private Vector2 _enemyCombatAnchoredPos;
	private bool _lastSuppressed;
	private Tween _glideTween;
	private Tween _scaleTween;
	private Tween _enemySlideTween;

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
		if (phase != _lastPhase || suppressed != _lastSuppressed || transitioning != _lastTransitioning)
		{
			ApplyPhase(phase);
			_lastPhase = phase;
			_lastSuppressed = suppressed;
			_lastTransitioning = transitioning;
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
		// world copy in ShopPageHud shows); during a driver transition it stays visible and
		// glides. Placement writes below keep running while hidden, so the hidden icon parks
		// at the shop anchor and the next shop -> combat flight starts from the right spot.
		playerIcon.SetActive(inCombat || inResult || (inShop && PhaseTransitionDriver.IsTransitioning));
		bool enemyWasActive = enemyIcon.activeSelf;
		bool enemyVisible = inCombat && !suppressed;
		enemyIcon.SetActive(enemyVisible);
		if (enemyVisible && !enemyWasActive && PhaseTransitionDriver.EnemyEntrancePending)
		{
			PlayEnemyEntrance();
		}
		if (_playerIconRt == null)
		{
			return;
		}
		Vector2 targetPos = inShop
			? ShopTopBarLayout.LeftOffsetToCanvasAnchored(ShopTopBarLayout.PlayerIconXFromLeftEdge, ShopTopBarLayout.PlayerIconViewportY, _canvas, ShopTopBarLayout.MainOrthoSize)
			: _combatAnchoredPos;
		Vector3 targetScale = inShop ? Vector3.one * ShopTopBarLayout.PlayerIconShopScale : _combatScale;
		var cfg = PhaseTransitionConfigSO.Me;
		if (PhaseTransitionDriver.IsTransitioning && cfg != null)
		{
			// Shared-element flight (demo flyShared HUD branch, :704-709): glide between homes.
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
	/// Enemy HUD counter-direction entrance (demo :724-727): starts above its combat anchor and
	/// slides DOWN in against the camera's upward travel.
	/// </summary>
	private void PlayEnemyEntrance()
	{
		if (_enemyIconRt == null) return;
		var cfg = PhaseTransitionConfigSO.Me;
		if (cfg == null) return;
		float refHeight = _canvas != null && _canvas.scaleFactor > 0.0001f
			? Screen.height / _canvas.scaleFactor
			: Screen.height;
		float slidePx = PhaseFlightPlanner.DemoPxToCanvasPx(cfg.enemySlideDemoPx, refHeight);
		KillTween(ref _enemySlideTween);
		_enemyIconRt.anchoredPosition = _enemyCombatAnchoredPos + new Vector2(0f, slidePx);
		_enemySlideTween = cfg.ApplyEase(_enemyIconRt.DOAnchorPos(_enemyCombatAnchoredPos, Mathf.Max(0.25f, cfg.transDur * 0.5f)).SetUpdate(UpdateType.Normal, true));
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
