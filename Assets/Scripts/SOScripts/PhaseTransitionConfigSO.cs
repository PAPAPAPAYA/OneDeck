using DG.Tweening;
using UnityEngine;

/// <summary>
/// Phase transition tuning (plan-phase-transition-world-camera-2026-09-21; demo:
/// docs/demo/PhaseTransitionDemo.html params bar :514-521). Lazy singleton loaded from
/// Resources; a MISSING asset is legal and simply disables transitions (Available=false),
/// so headless/test environments never hard-fail on a missing config.
/// Demo px values convert at use: world via PhaseFlightPlanner.PxToWorld (740px page),
/// canvas via PhaseFlightPlanner.DemoPxToCanvasPx.
/// </summary>
public class PhaseTransitionConfigSO : ScriptableObject
{
	public enum EaseMode { Overshoot, Smooth, Linear }

	[Header("Master")]
	[Tooltip("Master switch. Off = today's hard-cut phase changes, zero transition behavior.")]
	public bool enabled = true;
	[Tooltip("Skip transitions for headless/deterministic runs (batch mode, overrideCombatSeed, NullCombatVisuals) so combat sims pay zero tween time.")]
	public bool skipInHeadless = true;

	[Header("Timing (demo: transDur 800ms, cardStagger 70ms)")]
	[Tooltip("Camera travel + all shared flights duration, seconds (demo: 0.8)")]
	[Range(0.3f, 1.6f)] public float transDur = 0.8f;
	[Tooltip("Per-card flight delay, seconds (demo: 0.07)")]
	[Range(0f, 0.22f)] public float cardStagger = 0.07f;

	[Header("Ease (demo default: overshoot 1.7; smooth = cubic-bezier(.2,.7,.3,1) ~ OutQuad)")]
	public EaseMode easeMode = EaseMode.Overshoot;
	[Tooltip("Back-ease overshoot strength, Overshoot mode only (demo: 1.7 = DOTween OutBack default)")]
	[Range(1f, 3f)] public float overshoot = 1.7f;

	[Header("Distances (demo px on the 740px page; demo: cardArc 90, enemySlide 140, PAD 200)")]
	[Tooltip("Deck-card flight arc height, demo px (demo: 90)")]
	public float cardArcDemoPx = 90f;
	[Tooltip("Enemy HUD counter-direction slide-in distance, demo px (demo: 140)")]
	public float enemySlideDemoPx = 140f;
	[Tooltip("Background padding beyond each page edge for overshoot peek, demo px (demo: 200). v1.1 world topo only.")]
	public float pagePadDemoPx = 200f;

	public Ease GetEase()
	{
		switch (easeMode)
		{
			case EaseMode.Smooth: return Ease.OutQuad;
			case EaseMode.Linear: return Ease.Linear;
			default: return Ease.OutBack;
		}
	}

	/// <summary>Applies the configured ease to a tween; Overshoot mode also carries the strength.</summary>
	public Tween ApplyEase(Tween tween)
	{
		if (tween == null) return null;
		if (easeMode == EaseMode.Overshoot) return tween.SetEase(Ease.OutBack, overshoot);
		return tween.SetEase(GetEase());
	}

	private static PhaseTransitionConfigSO _me;
	private static bool _loadAttempted;

	/// <summary>Lazy singleton. Null (no error) when the asset does not exist: transitions simply stay off.</summary>
	public static PhaseTransitionConfigSO Me
	{
		get
		{
			if (_me == null && !_loadAttempted)
			{
				_loadAttempted = true;
				_me = Resources.Load<PhaseTransitionConfigSO>("PhaseTransitionConfig");
			}
			return _me;
		}
	}
}
