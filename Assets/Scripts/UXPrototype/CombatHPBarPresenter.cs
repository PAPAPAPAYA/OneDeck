using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat HP compare bar (top-center HUD). Pure presentation; no game-logic changes.
/// Polls CombatInfoDisplayer's displayed HP values — the same queue-frozen values the
/// HP text shows — so the bar changes exactly when a hit lands.
/// Motion design validated in docs/demo/CombatHPBarDemo.html.
/// Plan: plans/plan-hp-compare-bar-2026-07-18.md
/// </summary>
public class CombatHPBarPresenter : MonoBehaviour
{
	[Header("Wiring")]
	public RectTransform barRoot;
	public Image playerSeg;
	public Image enemySeg;
	public Image playerGhost;
	public Image enemyGhost;
	public Image playerFlash;
	public Image enemyFlash;
	public GamePhaseSO gamePhaseRef;
	public Canvas canvas;
	// NOT part of the missing-reference guard in Awake(): when unwired, Awake
	// creates a fallback shadow instead of disabling the component.
	public Image barShadow;

	[Header("Colors")]
	public Color playerColor = new Color(0.824f, 0.824f, 0.784f); // demo #d2d2c8
	public Color enemyColor = new Color(0.753f, 0.153f, 0.153f); // demo #c02727

	[Header("Tuning (defaults from CombatHPBarDemo.html)")]
	public float shareTweenDuration = 0.25f;
	public float ghostHoldDelay = 0.35f;
	public float ghostCollapseDuration = 0.43f;
	public float ghostBaseAlpha = 0.8f;
	public float flashPeakAlpha = 0.85f;
	public float flashInDuration = 0.10f;
	public float flashOutDuration = 0.12f;
	public float shakeDuration = 0.32f;
	public float shakeAmplitudeFactor = 0.12f;
	public float shakeMinPx = 2f;
	public float shakeMaxPx = 14f;
	public int shakeVibrato = 10;
	public float healFlashAlpha = 0.75f;
	public float healFlashDuration = 0.45f;
	public float lowHpThreshold = 0.25f;
	public float pulseTargetAlpha = 0.55f;
	public float pulseHalfDuration = 0.45f;
	public float ghostMinDelta = 0.004f;

	[Header("Shadow (single tuning entry point; Awake normalizes the scene Image)")]
	public Vector2 shadowOffset = new Vector2(4f, -4f);
	public float shadowPadding = 6f;
	public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);

	private int _displayedPlayerHp;
	private int _displayedEnemyHp;
	private bool _wasInCombat;
	private float _targetPlayerPct = 0.5f;
	private Vector2 _barRootBasePos;

	// Farthest old boundary of an unfinished ghost trail per side (playerPct
	// coordinates), so rapid successive hits accumulate into one continuous trail.
	private float? _ghostEdgePlayer;
	private float? _ghostEdgeEnemy;

	private Tween _playerFillTween;
	private Tween _enemyFillTween;
	private Tween _playerPulseTween;
	private Tween _enemyPulseTween;
	private bool _playerPulsing;
	private bool _enemyPulsing;

	private void Awake()
	{
		if (barRoot == null || playerSeg == null || enemySeg == null
			|| playerGhost == null || enemyGhost == null
			|| playerFlash == null || enemyFlash == null || gamePhaseRef == null)
		{
			Debug.LogError("[CombatHPBarPresenter] Missing serialized reference(s), disabling.");
			enabled = false;
			return;
		}
		if (canvas == null)
		{
			canvas = GetComponentInParent<Canvas>();
		}
		_barRootBasePos = barRoot.anchoredPosition;
		// VISUAL-FIX(2026-07-18): HP compare bar renders fully in the enemy color
		//   Cause:    Image.type=Filled with sprite == null silently ignores fillAmount
		//             and renders the full quad; the enemy segment (later sibling, on
		//             top) covered the player segment, so the whole bar looked red.
		//   Affects:  CombatHPBarPresenter segment/flash images (Filled type)
		//   Regress:  Enter combat at 20/20 HP: bar must show a left player-color half
		//             and a right enemy-color half; uneven HP must render proportional
		//             segment widths (e.g. 10/20 vs 20 -> 1/3 vs 2/3).
		EnsureFilledSprites();
		EnsureBarShadow();
		playerSeg.color = playerColor;
		enemySeg.color = enemyColor;
		SetAlpha(playerGhost, 0f);
		SetAlpha(enemyGhost, 0f);
		SetAlpha(playerFlash, 0f);
		SetAlpha(enemyFlash, 0f);
		barRoot.gameObject.SetActive(false);
	}

	private void Update()
	{
		bool inCombat = gamePhaseRef.Value() == EnumStorage.GamePhase.Combat;
		if (inCombat && !_wasInCombat)
		{
			EnterCombat();
		}
		else if (!inCombat && _wasInCombat)
		{
			ExitCombat();
		}
		_wasInCombat = inCombat;
		if (!inCombat)
		{
			return;
		}

		int playerHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedOwnerHp() : 0;
		int enemyHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedEnemyHp() : 0;

		int oldTotal = _displayedPlayerHp + _displayedEnemyHp;
		float oldPct = oldTotal > 0 ? (float)_displayedPlayerHp / oldTotal : 0.5f;
		int total = playerHp + enemyHp;
		float playerPct = total > 0 ? (float)playerHp / total : 0.5f;

		UpdateFills(playerPct);

		// Classify effects PER SIDE from each side's displayed-HP delta — never from
		// share diffs. Shares are complementary, so one side's change flips both
		// shares; share-diff classification would fire phantom damage on the side
		// that was not hit. The demo classifies by the changed side's HP delta sign.
		float shareDelta = Mathf.Abs(playerPct - oldPct);
		if (shareDelta >= ghostMinDelta)
		{
			int dPlayer = playerHp - _displayedPlayerHp;
			int dEnemy = enemyHp - _displayedEnemyHp;
			if (dPlayer < 0)
			{
				PlayDamage(true, oldPct, playerPct, shareDelta);
			}
			else if (dPlayer > 0)
			{
				PlayHeal(true, oldPct, playerPct);
			}
			if (dEnemy < 0)
			{
				PlayDamage(false, oldPct, playerPct, shareDelta);
			}
			else if (dEnemy > 0)
			{
				PlayHeal(false, oldPct, playerPct);
			}
		}

		_displayedPlayerHp = playerHp;
		_displayedEnemyHp = enemyHp;
		UpdatePulseState(true, playerPct);
		UpdatePulseState(false, 1f - playerPct);
	}

	private void LateUpdate()
	{
		if (!_wasInCombat)
		{
			return;
		}
		// Flash overlays are Filled mirrors of their segment.
		playerFlash.fillAmount = playerSeg.fillAmount;
		enemyFlash.fillAmount = enemySeg.fillAmount;
	}

	private void OnDisable()
	{
		CleanupVisuals();
	}

	// Silent sync to the current displayed values on combat entry: no tweens, no
	// effects, so the first frame never plays a phantom damage/heal from defaults.
	private void EnterCombat()
	{
		CleanupVisuals();
		barRoot.gameObject.SetActive(true);
		_displayedPlayerHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedOwnerHp() : 0;
		_displayedEnemyHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedEnemyHp() : 0;
		int total = _displayedPlayerHp + _displayedEnemyHp;
		float pct = total > 0 ? (float)_displayedPlayerHp / total : 0.5f;
		_targetPlayerPct = pct;
		playerSeg.fillAmount = pct;
		enemySeg.fillAmount = 1f - pct;
		playerFlash.fillAmount = pct;
		enemyFlash.fillAmount = 1f - pct;
		UpdatePulseState(true, pct);
		UpdatePulseState(false, 1f - pct);
	}

	private void ExitCombat()
	{
		CleanupVisuals();
		barRoot.gameObject.SetActive(false);
	}

	private void CleanupVisuals()
	{
		if (playerGhost == null)
		{
			return; // Awake disabled the component for missing references.
		}
		KillTween(ref _playerFillTween);
		KillTween(ref _enemyFillTween);
		KillPulse(true);
		KillPulse(false);
		_playerPulsing = false;
		_enemyPulsing = false;
		playerGhost.DOKill();
		playerGhost.rectTransform.DOKill();
		enemyGhost.DOKill();
		enemyGhost.rectTransform.DOKill();
		playerFlash.DOKill();
		enemyFlash.DOKill();
		barRoot.DOKill();
		barRoot.anchoredPosition = _barRootBasePos;
		SetAlpha(playerGhost, 0f);
		SetAlpha(enemyGhost, 0f);
		SetAlpha(playerFlash, 0f);
		SetAlpha(enemyFlash, 0f);
		_ghostEdgePlayer = null;
		_ghostEdgeEnemy = null;
	}

	private void UpdateFills(float playerPct)
	{
		if (Mathf.Approximately(playerPct, _targetPlayerPct))
		{
			return;
		}
		_targetPlayerPct = playerPct;
		// Restart-safe: hits can land mid-transition.
		KillTween(ref _playerFillTween);
		KillTween(ref _enemyFillTween);
		_playerFillTween = ApplySpeed(playerSeg.DOFillAmount(playerPct, shareTweenDuration).SetEase(Ease.OutQuad));
		_enemyFillTween = ApplySpeed(enemySeg.DOFillAmount(1f - playerPct, shareTweenDuration).SetEase(Ease.OutQuad));
	}

	private void PlayDamage(bool playerSide, float oldPct, float newPct, float shareDelta)
	{
		Image ghost = playerSide ? playerGhost : enemyGhost;
		RectTransform ghostRt = ghost.rectTransform;

		// ghostEdge accumulation: keep the farthest old boundary while a trail is active.
		float edge = oldPct;
		float? stored = playerSide ? _ghostEdgePlayer : _ghostEdgeEnemy;
		if (stored.HasValue)
		{
			edge = playerSide ? Mathf.Max(stored.Value, oldPct) : Mathf.Min(stored.Value, oldPct);
		}
		if (playerSide)
		{
			_ghostEdgePlayer = edge;
		}
		else
		{
			_ghostEdgeEnemy = edge;
		}

		float left = playerSide ? newPct : edge;
		float right = playerSide ? edge : newPct;
		PositionGhost(ghost, left, right, playerSide ? 0f : 1f);

		// A new hit kills the side's running ghost tweens and restarts hold + collapse.
		ghost.DOKill();
		ghostRt.DOKill();
		ApplySpeed(ghostRt.DOScaleX(0f, ghostCollapseDuration).SetEase(Ease.InQuad).SetDelay(ghostHoldDelay))
			.OnComplete(() => ClearGhostEdge(playerSide));
		ApplySpeed(ghost.DOFade(0f, ghostCollapseDuration).SetEase(Ease.InQuad).SetDelay(ghostHoldDelay));

		FlashSide(playerSide);
		ShakeBar(shareDelta);
	}

	private void PlayHeal(bool playerSide, float oldPct, float newPct)
	{
		Image ghost = playerSide ? playerGhost : enemyGhost;
		// The demo cancels the side's running animation on heal.
		ghost.DOKill();
		ghost.rectTransform.DOKill();
		ClearGhostEdge(playerSide);

		float left = Mathf.Min(oldPct, newPct);
		float right = Mathf.Max(oldPct, newPct);
		PositionGhost(ghost, left, right, 0.5f);
		SetAlpha(ghost, healFlashAlpha);
		ApplySpeed(ghost.DOFade(0f, healFlashDuration).SetEase(Ease.OutQuad));
	}

	private void PositionGhost(Image ghost, float leftPct, float rightPct, float pivotX)
	{
		RectTransform rt = ghost.rectTransform;
		rt.anchorMin = new Vector2(leftPct, 0f);
		rt.anchorMax = new Vector2(rightPct, 1f);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		rt.pivot = new Vector2(pivotX, 0.5f);
		rt.localScale = Vector3.one;
		SetAlpha(ghost, ghostBaseAlpha);
	}

	private void FlashSide(bool playerSide)
	{
		Image flash = playerSide ? playerFlash : enemyFlash;
		flash.DOKill();
		SetAlpha(flash, 0f);
		Sequence seq = DOTween.Sequence();
		seq.Append(flash.DOFade(flashPeakAlpha, flashInDuration));
		seq.Append(flash.DOFade(0f, flashOutDuration));
		ApplySpeed(seq);
	}

	// Shake strength uses the demo formula in screen pixels, converted to anchored
	// units via the canvas scale factor. Pure horizontal, no directional randomness.
	private void ShakeBar(float shareDelta)
	{
		float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
		if (scaleFactor <= 0.0001f)
		{
			scaleFactor = 1f;
		}
		float barWidthPx = barRoot.rect.width * scaleFactor;
		float strengthPx = Mathf.Clamp(shareDelta * barWidthPx * shakeAmplitudeFactor, shakeMinPx, shakeMaxPx);
		float strength = strengthPx / scaleFactor;
		barRoot.DOKill();
		barRoot.anchoredPosition = _barRootBasePos;
		ApplySpeed(barRoot.DOShakePosition(shakeDuration, new Vector3(strength, 0f, 0f), shakeVibrato, 0f, false, true));
	}

	private void UpdatePulseState(bool playerSide, float share)
	{
		bool shouldPulse = share > 0f && share < lowHpThreshold;
		bool pulsing = playerSide ? _playerPulsing : _enemyPulsing;
		if (shouldPulse == pulsing)
		{
			return;
		}
		if (shouldPulse)
		{
			Image seg = playerSide ? playerSeg : enemySeg;
			Tween tween = ApplySpeed(seg.DOFade(pulseTargetAlpha, pulseHalfDuration).SetLoops(-1, LoopType.Yoyo));
			if (playerSide)
			{
				_playerPulseTween = tween;
			}
			else
			{
				_enemyPulseTween = tween;
			}
		}
		else
		{
			KillPulse(playerSide);
		}
		if (playerSide)
		{
			_playerPulsing = shouldPulse;
		}
		else
		{
			_enemyPulsing = shouldPulse;
		}
	}

	private void KillPulse(bool playerSide)
	{
		if (playerSide)
		{
			KillTween(ref _playerPulseTween);
		}
		else
		{
			KillTween(ref _enemyPulseTween);
		}
		Image seg = playerSide ? playerSeg : enemySeg;
		SetAlpha(seg, 1f);
	}

	private void ClearGhostEdge(bool playerSide)
	{
		if (playerSide)
		{
			_ghostEdgePlayer = null;
		}
		else
		{
			_ghostEdgeEnemy = null;
		}
	}

	private static void KillTween(ref Tween tween)
	{
		if (tween != null && tween.IsActive())
		{
			tween.Kill();
		}
		tween = null;
	}

	private static void SetAlpha(Image image, float alpha)
	{
		Color color = image.color;
		color.a = alpha;
		image.color = color;
	}

	// Filled Images silently ignore fillAmount when sprite is null (see VISUAL-FIX
	// above); guarantee one. The scene wires Assets/Sprites/WhiteSquare.png — this is
	// only a fallback so a missing reference can never silently break the bar again.
	private static Sprite _fallbackWhiteSprite;

	private void EnsureFilledSprites()
	{
		if (playerSeg.sprite != null && enemySeg.sprite != null
			&& playerFlash.sprite != null && enemyFlash.sprite != null)
		{
			return;
		}
		if (_fallbackWhiteSprite == null)
		{
			var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
			var pixels = new Color[16];
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = Color.white;
			}
			tex.SetPixels(pixels);
			tex.Apply();
			_fallbackWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
		}
		if (playerSeg.sprite == null)
		{
			playerSeg.sprite = _fallbackWhiteSprite;
		}
		if (enemySeg.sprite == null)
		{
			enemySeg.sprite = _fallbackWhiteSprite;
		}
		if (playerFlash.sprite == null)
		{
			playerFlash.sprite = _fallbackWhiteSprite;
		}
		if (enemyFlash.sprite == null)
		{
			enemyFlash.sprite = _fallbackWhiteSprite;
		}
		Debug.LogWarning("[CombatHPBarPresenter] A Filled image had no sprite; assigned a generated white sprite (fillAmount is ignored without one). Wire Assets/Sprites/WhiteSquare.png in the scene.");
	}

	// One stretched shadow behind the whole bar silhouette (per-segment Shadow
	// components were rejected: seam lines + fragmented moving shadows during
	// ghost/flash animations). The presenter fields are the single tuning entry
	// point for offset/padding/color; the scene Image only owns the sprite.
	// Fallback is Awake-time only and does not heal mid-session deletion.
	private void EnsureBarShadow()
	{
		if (barShadow == null)
		{
			var go = new GameObject("BarShadow", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(barRoot, false);
			barShadow = go.GetComponent<Image>();
			barShadow.type = Image.Type.Simple;
			barShadow.raycastTarget = false;
			barShadow.sprite = playerSeg.sprite; // guaranteed non-null by EnsureFilledSprites()
			Debug.LogWarning("[CombatHPBarPresenter] barShadow not wired; created a fallback BarShadow child. Author one under HPBarRoot in the scene.");
		}
		RectTransform rt = barShadow.rectTransform;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(shadowOffset.x - shadowPadding, shadowOffset.y - shadowPadding);
		rt.offsetMax = new Vector2(shadowOffset.x + shadowPadding, shadowOffset.y + shadowPadding);
		barShadow.color = shadowColor;
		rt.SetSiblingIndex(0);
	}

	// timeScale (not ScaleDuration) so SetDelay-based holds scale with the global
	// combat animation speed together with the durations.
	private static T ApplySpeed<T>(T tween) where T : Tween
	{
		tween.timeScale = CombatAnimationSpeed.SpeedScale;
		return tween;
	}
}
