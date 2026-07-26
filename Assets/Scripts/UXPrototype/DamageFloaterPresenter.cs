using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating damage numbers above each side's HP numeric display. Pure presentation;
/// no game-logic changes. Polls CombatInfoDisplayer's displayed HP values — the same
/// queue-frozen values the HP text, compare bar, and numeric display show — so a
/// floater spawns on the exact frame a hit lands. Damage only; heals and full-shield
/// absorbs produce no floater (consistent with the HP bar and numeric display).
/// Motion design validated in docs/demo/DamageFloaterDemo.html.
/// Plan: plans/plan-damage-floater-2026-07-26.md
/// </summary>
public class DamageFloaterPresenter : MonoBehaviour
{
	[Header("Wiring")]
	public RectTransform floaterLayer;
	public RectTransform playerAnchor;
	public RectTransform enemyAnchor;
	public GamePhaseSO gamePhaseRef;
	public Canvas canvas;

	[Header("Optional")]
	[Tooltip("Null = TMP default font asset (same as CardTagTooltip / ResultStatsPanel runtime text).")]
	public TMP_FontAsset font;

	[Header("Tuning (defaults from DamageFloaterDemo.html)")]
	public float fontSize = 30f;
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
		if (floaterLayer == null || playerAnchor == null || enemyAnchor == null || gamePhaseRef == null)
		{
			Debug.LogError("[DamageFloaterPresenter] Missing serialized reference(s), disabling.");
			enabled = false;
			return;
		}
		if (canvas == null)
		{
			canvas = GetComponentInParent<Canvas>();
		}
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

		// Classify PER SIDE from each side's own displayed-HP delta — same rule as
		// CombatHPBarPresenter, so one side's change can never spawn a phantom
		// floater on the side that was not hit. Positive deltas (heals) are ignored.
		if (playerHp < _displayedPlayerHp)
		{
			SpawnFloater(true, _displayedPlayerHp - playerHp);
		}
		if (enemyHp < _displayedEnemyHp)
		{
			SpawnFloater(false, _displayedEnemyHp - enemyHp);
		}

		_displayedPlayerHp = playerHp;
		_displayedEnemyHp = enemyHp;
	}

	private void OnDisable()
	{
		CleanupFloaters();
	}

	// Silent sync to the current displayed values on combat entry, so the first
	// frame never spawns a phantom floater from stale/default diffs.
	private void EnterCombat()
	{
		CleanupFloaters();
		_displayedPlayerHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedOwnerHp() : 0;
		_displayedEnemyHp = CombatInfoDisplayer.me != null ? CombatInfoDisplayer.me.GetDisplayedEnemyHp() : 0;
	}

	private void ExitCombat()
	{
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
		RectTransform anchor = playerSide ? playerAnchor : enemyAnchor;
		Vector2 local = AnchorToLayerLocal(anchor);
		float px = PxToLocal();
		local.x += Random.Range(-jitterPx * 0.5f, jitterPx * 0.5f) * px;

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
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.color = DamageColor();
		tmp.raycastTarget = false;

		// uGUI equivalent of the demo's black text-shadow.
		var shadow = go.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
		shadow.effectDistance = new Vector2(0f, -2f);

		var group = go.AddComponent<CanvasGroup>();
		group.alpha = 0f;

		var entry = new ActiveFloater { go = go };
		entry.seq = PlayTimeline(rt, group, local.y, px);
		_active.Add(entry);
		entry.seq.OnComplete(() =>
		{
			_active.Remove(entry);
			Destroy(go);
		});
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

	private Vector2 AnchorToLayerLocal(RectTransform anchor)
	{
		Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
		Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);
		Vector2 local;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(floaterLayer, screen, cam, out local);
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

	private static Color DamageColor()
	{
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
