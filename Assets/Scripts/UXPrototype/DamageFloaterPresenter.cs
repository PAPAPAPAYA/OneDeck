using System.Collections.Generic;
using DefaultNamespace.Managers;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating damage numbers at each side's attack target position
/// (AttackAnimationManager.playerTargetPos / enemyTargetPos — the world position
/// the attacking card charges to). Pure presentation; no game-logic changes.
/// Primary path: CombatInfoDisplayer.onHpDisplayCommitted fires once per attack hit
/// carrying THAT hit's own actual HP loss, so each floater shows the correct number
/// no matter how reactive chains interleave logic order and animation playback
/// order (VISUAL-FIX(2026-08-04) below). Fallback path: frame polling of the
/// displayed HP, but ONLY while a side has no pending attack commits — this covers
/// HP drops that have no hit moment (status-effect damage, legacy no-recorder
/// path). Damage only; heals and full-shield absorbs produce no floater
/// (consistent with the HP bar and numeric display).
/// Diagnostic logs route through TestManager (LogCategory.DamageFloater).
/// Motion design validated in docs/demo/DamageFloaterDemo.html.
/// Plan: plans/plan-damage-floater-2026-07-26.md
/// </summary>
public class DamageFloaterPresenter : MonoBehaviour
{
	[Header("Wiring")]
	public RectTransform floaterLayer;
	public GamePhaseSO gamePhaseRef;
	public Canvas canvas;

	[Header("Optional")]
	[Tooltip("Null = TMP default font asset (same as CardTagTooltip / ResultStatsPanel runtime text).")]
	public TMP_FontAsset font;

	[Header("Colors")]
	[Tooltip("Floater color on the player side. Null = GameColorPalette damage.")]
	public ColorSO playerColor;
	[Tooltip("Floater color on the enemy side. Null = GameColorPalette damage.")]
	public ColorSO enemyColor;

	[Header("Tuning (defaults from DamageFloaterDemo.html)")]
	public float fontSize = 30f;
	[Tooltip("Bold floater text (TMP faux bold if the font asset has no bold face).")]
	public bool bold;
	[Tooltip("Italic floater text (TMP faux italic if the font asset has no italic face).")]
	public bool italic;
	public float punchScale = 2.2f;
	public float squashScale = 0.85f;
	public float overshootScale = 1.08f;
	public float finalScale = 0.95f;
	public float punchInTime = 0.38f;
	public float holdTime = 0.45f;
	public float fadeTime = 0.36f;
	public float floatUpDistPx = 62f;
	public float jitterPx = 40f;

	private class ActiveFloater
	{
		public GameObject go;
		public Sequence seq;
	}

	private readonly List<ActiveFloater> _active = new List<ActiveFloater>();
	private int _displayedPlayerHp;
	private int _displayedEnemyHp;
	private bool _wasInCombat;

	private void Awake()
	{
		if (floaterLayer == null || gamePhaseRef == null)
		{
			Debug.LogError("[DamageFloater] Missing serialized reference(s) (floaterLayer="
				+ (floaterLayer != null) + ", gamePhaseRef=" + (gamePhaseRef != null) + "), disabling.");
			enabled = false;
			return;
		}
		if (canvas == null)
		{
			canvas = GetComponentInParent<Canvas>();
		}
		TestManager.Log("[DamageFloater] Awake OK on '" + gameObject.name + "'"
			+ " | canvas=" + (canvas != null ? canvas.name : "NULL")
			+ " renderMode=" + (canvas != null ? canvas.renderMode.ToString() : "-")
			+ " | floaterLayer='" + floaterLayer.name + "' rect=" + floaterLayer.rect
			+ " | gamePhaseRef=" + gamePhaseRef.name
			+ " | font=" + (font != null ? font.name : "TMP default")
			+ " | AttackAnimationManager.me=" + (AttackAnimationManager.me != null)
			+ " | Camera.main=" + (Camera.main != null ? Camera.main.name : "NULL"));
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

		// Polling fallback only for sides with NO pending attack commits: while a side
		// is frozen, per-hit floaters arrive via onHpDisplayCommitted and a frame diff
		// here would double-count or misattribute them. The fallback covers HP drops
		// with no hit moment (status-effect damage, legacy no-recorder path).
		bool playerFrozen = CombatInfoDisplayer.me != null && CombatInfoDisplayer.me.HasPendingHpDisplay(true);
		bool enemyFrozen = CombatInfoDisplayer.me != null && CombatInfoDisplayer.me.HasPendingHpDisplay(false);

		// Classify PER SIDE from each side's own displayed-HP delta — same rule as
		// CombatHPBarPresenter, so one side's change can never spawn a phantom
		// floater on the side that was not hit. Positive deltas (heals) are ignored.
		if (!playerFrozen && playerHp < _displayedPlayerHp)
		{
			TestManager.Log("[DamageFloater] Player displayed HP drop (fallback poll): frame=" + Time.frameCount
				+ " " + _displayedPlayerHp + " -> " + playerHp
				+ " (dmg " + (_displayedPlayerHp - playerHp) + ")");
			SpawnFloater(true, _displayedPlayerHp - playerHp);
		}
		if (!enemyFrozen && enemyHp < _displayedEnemyHp)
		{
			TestManager.Log("[DamageFloater] Enemy displayed HP drop (fallback poll): frame=" + Time.frameCount
				+ " " + _displayedEnemyHp + " -> " + enemyHp
				+ " (dmg " + (_displayedEnemyHp - enemyHp) + ")");
			SpawnFloater(false, _displayedEnemyHp - enemyHp);
		}

		_displayedPlayerHp = playerHp;
		_displayedEnemyHp = enemyHp;
	}

	// VISUAL-FIX(2026-08-04): Floater numbers swapped between hits — Corpse
	//   Explosion (2x2, buries Eternal Ghost mid-effect) + Ghost (1 on bury) showed
	//   2/1/2 instead of 2/2/1.
	//   Cause:    The old design diffed the displayed HP between frames, and the
	//             display itself was driven by a FIFO of absolute post-hit values
	//             enqueued in logic order but popped in animation playback order.
	//             Reactive chains (bury -> onMeBuried damage) made those orders
	//             diverge, attaching each number to the wrong hit.
	//   Affects:  DamageFloaterPresenter (now driven by onHpDisplayCommitted),
	//             CombatInfoDisplayer.CommitHpDisplay (carries per-hit hpLoss)
	//   Regress:  Combat where a card buries Eternal Ghost between its own two
	//             hits: floaters must read 2 (reveal hit), 2 (parent attack anim),
	//             1 (ghost attack anim). Also: a hit fully absorbed by shield
	//             spawns no floater; status-effect damage still floats via the
	//             fallback poll once the display unfreezes.
	// Per-hit commit event: one floater per attack hit with THAT hit's own actual
	// HP loss. Also keeps the fallback cache in sync with the frozen display.
	private void OnHpDisplayCommitted(bool isOwner, int hpLoss, int newDisplayed)
	{
		if (isOwner)
		{
			_displayedPlayerHp = newDisplayed;
		}
		else
		{
			_displayedEnemyHp = newDisplayed;
		}
		if (hpLoss > 0)
		{
			TestManager.Log("[DamageFloater] Commit-driven floater: frame=" + Time.frameCount
				+ " side=" + (isOwner ? "player" : "enemy") + " hpLoss=" + hpLoss
				+ " displayed=" + newDisplayed);
			SpawnFloater(isOwner, hpLoss);
		}
	}

	// The pending locks were cancelled mid-flight (ClearHpDisplayLocks): the cached
	// values are stale, so silently reseed from the getters (now live-HP backed)
	// instead of letting the fallback poll show a huge phantom diff.
	private void OnHpDisplayLocksCleared()
	{
		ResyncDisplayedCache();
	}

	private void ResyncDisplayedCache()
	{
		_displayedPlayerHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedOwnerHp() : 0;
		_displayedEnemyHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedEnemyHp() : 0;
	}

	private bool _displayEventsSubscribed;

	private void SubscribeDisplayEvents()
	{
		if (_displayEventsSubscribed || CombatInfoDisplayer.me == null)
		{
			return;
		}
		CombatInfoDisplayer.me.onHpDisplayCommitted += OnHpDisplayCommitted;
		CombatInfoDisplayer.me.onHpDisplayLocksCleared += OnHpDisplayLocksCleared;
		_displayEventsSubscribed = true;
	}

	private void UnsubscribeDisplayEvents()
	{
		if (!_displayEventsSubscribed || CombatInfoDisplayer.me == null)
		{
			return;
		}
		CombatInfoDisplayer.me.onHpDisplayCommitted -= OnHpDisplayCommitted;
		CombatInfoDisplayer.me.onHpDisplayLocksCleared -= OnHpDisplayLocksCleared;
		_displayEventsSubscribed = false;
	}

	private void OnDisable()
	{
		UnsubscribeDisplayEvents();
		CleanupFloaters();
	}

	// Silent sync to the current displayed values on combat entry, so the first
	// frame never spawns a phantom floater from stale/default diffs.
	private void EnterCombat()
	{
		CleanupFloaters();
		SubscribeDisplayEvents();
		ResyncDisplayedCache();
		TestManager.Log("[DamageFloater] EnterCombat. Synced displayed HP player=" + _displayedPlayerHp
			+ " enemy=" + _displayedEnemyHp
			+ (CombatInfoDisplayer.me == null ? " | WARNING: CombatInfoDisplayer.me is null, HP reads as 0" : ""));
	}

	private void ExitCombat()
	{
		UnsubscribeDisplayEvents();
		TestManager.Log("[DamageFloater] ExitCombat. Cleaning up " + _active.Count + " live floater(s).");
		CleanupFloaters();
	}

	private void CleanupFloaters()
	{
		if (floaterLayer == null)
		{
			return; // Awake disabled the component for missing references.
		}
		for (int i = 0; i < _active.Count; i++)
		{
			if (_active[i].seq != null && _active[i].seq.IsActive())
			{
				_active[i].seq.Kill();
			}
			if (_active[i].go != null)
			{
				Destroy(_active[i].go);
			}
		}
		_active.Clear();
	}

	private void SpawnFloater(bool playerSide, int amount)
	{
		// Base position = the attack target the card charges to (world space).
		Transform target = AttackAnimationManager.me != null
			? (playerSide ? AttackAnimationManager.me.playerTargetPos : AttackAnimationManager.me.enemyTargetPos)
			: null;
		if (target == null)
		{
			TestManager.LogWarning("[DamageFloater] Skipping floater (side=" + (playerSide ? "player" : "enemy")
				+ ", amount=" + amount + "): AttackAnimationManager.me="
				+ (AttackAnimationManager.me != null) + ", target transform missing.");
			return;
		}
		if (Camera.main == null)
		{
			TestManager.LogWarning("[DamageFloater] Skipping floater (side=" + (playerSide ? "player" : "enemy")
				+ ", amount=" + amount + "): Camera.main is null, cannot convert world position.");
			return;
		}
		Vector3 worldPos = target.position;
		Vector2 local = WorldToLayerLocal(worldPos);
		float px = PxToLocal();
		local.x += Random.Range(-jitterPx * 0.5f, jitterPx * 0.5f) * px;
		local = ClampToLayer(local, px);

		var go = CreateFloaterObject(local, amount, playerSide);
		var rt = (RectTransform)go.transform;
		var tmp = go.GetComponent<TextMeshProUGUI>();
		var group = go.GetComponent<CanvasGroup>();

		TestManager.Log("[DamageFloater] Spawned '" + tmp.text + "' side=" + (playerSide ? "player" : "enemy")
			+ " | target='" + target.name + "' world=" + worldPos
			+ " | layerLocal=" + local + " (layer rect=" + floaterLayer.rect + ")"
			+ " | px->local=" + px + " | font=" + (tmp.font != null ? tmp.font.name : "NULL")
			+ " | color=" + tmp.color + " | scale=" + punchScale + " | duration="
			+ (punchInTime + holdTime + fadeTime) + "s / SpeedScale=" + CombatAnimationSpeed.SpeedScale);

		var entry = new ActiveFloater { go = go };
		entry.seq = PlayTimeline(rt, group, local.y, px);
		_active.Add(entry);
		entry.seq.OnComplete(() =>
		{
			_active.Remove(entry);
			TestManager.Log("[DamageFloater] Floater '" + tmp.text + "' finished, destroyed.");
			Destroy(go);
		});
	}

	// Builds the floater GameObject (TMP text + black shadow + CanvasGroup) at a
	// layer-local position. Shared by the gameplay spawn path above and the
	// edit-mode preview (DamageFloaterPresenterEditor).
	private GameObject CreateFloaterObject(Vector2 local, int amount, bool playerSide)
	{
		var go = new GameObject("DamageFloater", typeof(RectTransform));
		go.transform.SetParent(floaterLayer, false);
		var rt = (RectTransform)go.transform;
		rt.anchoredPosition = local;
		rt.localScale = Vector3.one * punchScale;

		var tmp = go.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			tmp.font = font;
		}
		tmp.text = "-" + amount;
		tmp.fontSize = fontSize;
		FontStyles style = FontStyles.Normal;
		if (bold)
		{
			style |= FontStyles.Bold;
		}
		if (italic)
		{
			style |= FontStyles.Italic;
		}
		tmp.fontStyle = style;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.color = DamageColor(playerSide);
		tmp.raycastTarget = false;

		// uGUI equivalent of the demo's black text-shadow.
		var shadow = go.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
		shadow.effectDistance = new Vector2(0f, -2f);

		var group = go.AddComponent<CanvasGroup>();
		group.alpha = 0f;
		return go;
	}

	// Edit-mode preview entry point (called by DamageFloaterPresenterEditor):
	// builds a floater at an explicit layer-local position with no gameplay
	// wiring (no HP polling, no attack-target lookup). The returned Sequence
	// uses manual update because DOTween does not tick outside Play Mode — the
	// caller ticks it via DOTween.ManualUpdate and destroys the object with
	// DestroyImmediate when done.
	public Sequence SpawnPreviewFloater(Vector2 localPos, int amount, bool playerSide, out GameObject floater)
	{
		float px = PxToLocal();
		floater = CreateFloaterObject(localPos, amount, playerSide);
		var rt = (RectTransform)floater.transform;
		var group = floater.GetComponent<CanvasGroup>();
		Sequence seq = PlayTimeline(rt, group, localPos.y, px);
		seq.SetUpdate(UpdateType.Manual);
		return seq;
	}

	// Demo px -> layer-local units via the canvas scale factor (the bar's shake
	// conversion pattern), so float distances track the reference resolution.
	private float PxToLocal()
	{
		float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
		if (scaleFactor <= 0.0001f)
		{
			scaleFactor = 1f;
		}
		return 1f / scaleFactor;
	}

	// World position (the attack target) -> layer-local units. WorldToScreenPoint
	// needs the world camera (Camera.main); the overlay-canvas local conversion
	// takes a null camera.
	private Vector2 WorldToLayerLocal(Vector3 worldPos)
	{
		Camera worldCam = Camera.main;
		if (worldCam == null)
		{
			return Vector2.zero;
		}
		Vector2 screen = worldCam.WorldToScreenPoint(worldPos);
		Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
		Vector2 local;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(floaterLayer, screen, uiCam, out local);
		return local;
	}

	// VISUAL-FIX(2026-07-26): Damage floaters spawn fully off-screen at the top corners
	//   Cause:    The spawn base position is the attack target (world (±3, 6)), which
	//             sits outside the camera frustum (visible half-extents ≈ ±2.79 x,
	//             ±6.06 y): x maps ~7.5% past the screen edge and y to 99% of the top
	//             edge, and the float-up drift carries the text even further out.
	//   Affects:  DamageFloaterPresenter.SpawnFloater
	//   Regress:  Deal damage to each side in combat: the floater must stay fully
	//             visible on screen for its whole lifetime (punch, hold, float-up).
	private Vector2 ClampToLayer(Vector2 local, float px)
	{
		Rect rect = floaterLayer.rect;
		// Horizontal: keep the whole text inside (1.5 em ≈ half of a "-123" string).
		// Vertical: reserve the float-up distance plus text height above the spawn
		// point so the END of the float stays visible too, not just the spawn frame.
		float xMargin = fontSize * 1.5f * px;
		float yMargin = fontSize * px + 10f * px;
		float yMin = rect.yMin + yMargin;
		float yMax = Mathf.Max(yMin, rect.yMax - floatUpDistPx * px - yMargin);
		local.x = Mathf.Clamp(local.x, rect.xMin + xMargin, rect.xMax - xMargin);
		local.y = Mathf.Clamp(local.y, yMin, yMax);
		return local;
	}

	// Demo keyframes (DamageFloaterDemo.html rebuildKeyframes) mapped to one DOTween
	// Sequence: punch scale segments + opacity fade-in, hold drift, float-up fade-out.
	// Accepted deviation from the demo's single global cubic-bezier(.2,.8,.3,1):
	// per-segment OutQuad for the punch phase and InQuad for the final fade.
	private Sequence PlayTimeline(RectTransform rt, CanvasGroup group, float baseY, float px)
	{
		DamageFloaterTimeline.Keyframes k = DamageFloaterTimeline.Compute(punchInTime, holdTime, fadeTime);
		float fade = k.totalTime - k.holdEndTime;

		Sequence seq = DOTween.Sequence();
		// Scale: punchScale (set at spawn) -> squash -> overshoot -> settle -> final.
		seq.Insert(0f, rt.DOScale(squashScale, k.squashTime).SetEase(Ease.OutQuad));
		seq.Insert(k.squashTime, rt.DOScale(overshootScale, k.overshootTime - k.squashTime).SetEase(Ease.OutQuad));
		seq.Insert(k.overshootTime, rt.DOScale(1f, k.punchEndTime - k.overshootTime).SetEase(Ease.OutQuad));
		seq.Insert(k.holdEndTime, rt.DOScale(finalScale, fade).SetEase(Ease.InQuad));
		// Opacity: fade in over the first punch sub-step; fade out over the fade phase.
		seq.Insert(0f, group.DOFade(1f, k.squashTime).SetEase(Ease.OutQuad));
		seq.Insert(k.holdEndTime, group.DOFade(0f, fade).SetEase(Ease.InQuad));
		// Y drift waypoints (demo px, up-positive): 8 / 14 / 18 / 22 / floatUpDist.
		seq.Insert(0f, rt.DOAnchorPosY(baseY + DamageFloaterTimeline.WaypointSquashPx * px, k.squashTime).SetEase(Ease.OutQuad));
		seq.Insert(k.squashTime, rt.DOAnchorPosY(baseY + DamageFloaterTimeline.WaypointOvershootPx * px, k.overshootTime - k.squashTime).SetEase(Ease.OutQuad));
		seq.Insert(k.overshootTime, rt.DOAnchorPosY(baseY + DamageFloaterTimeline.WaypointSettlePx * px, k.punchEndTime - k.overshootTime).SetEase(Ease.OutQuad));
		seq.Insert(k.punchEndTime, rt.DOAnchorPosY(baseY + DamageFloaterTimeline.WaypointHoldEndPx * px, k.holdEndTime - k.punchEndTime).SetEase(Ease.OutQuad));
		seq.Insert(k.holdEndTime, rt.DOAnchorPosY(baseY + floatUpDistPx * px, fade).SetEase(Ease.InQuad));
		return ApplySpeed(seq);
	}

	// Per-side floater color: the serialized ColorSO fields win; both fall back
	// to the palette damage color, then to the demo red.
	private Color DamageColor(bool playerSide)
	{
		ColorSO sideColor = playerSide ? playerColor : enemyColor;
		if (sideColor != null)
		{
			return sideColor.value;
		}
		if (GameColorPalette.Me != null && GameColorPalette.Me.damage != null)
		{
			return GameColorPalette.Me.damage.value;
		}
		return new Color(1f, 0.23f, 0.19f); // demo #ff3b30 fallback
	}

	// timeScale (not ScaleDuration) so the whole sequence scales with the global
	// combat animation speed (the bar's ApplySpeed pattern).
	private static T ApplySpeed<T>(T tween) where T : Tween
	{
		tween.timeScale = CombatAnimationSpeed.SpeedScale;
		return tween;
	}
}
