using UnityEngine;

/// <summary>
/// Keyframe timing math for DamageFloaterPresenter, ported 1:1 from
/// docs/demo/DamageFloaterDemo.html (rebuildKeyframes) so EditMode tests can pin
/// golden values. Pure static, unit-testable (same pattern as DeckCascadeLayout /
/// HPNumericCounter).
/// Plan: plans/plan-damage-floater-2026-07-26.md
/// </summary>
public static class DamageFloaterTimeline
{
	// Punch sub-keyframe ratios within the punch phase (demo: s1 = pIn * 0.32,
	// s2 = pIn * 0.58, s3 = pIn).
	public const float PunchSquashRatio = 0.32f;
	public const float PunchOvershootRatio = 0.58f;

	// Y drift waypoints in demo px (up-positive in Unity; CSS y is down-positive,
	// so the demo's -8 / -14 / -18 / -22 / -floatUpDist map to these magnitudes).
	public const float WaypointSquashPx = 8f;
	public const float WaypointOvershootPx = 14f;
	public const float WaypointSettlePx = 18f;
	public const float WaypointHoldEndPx = 22f;

	/// <summary>
	/// Absolute keyframe times (seconds from spawn) for one floater animation.
	/// </summary>
	public readonly struct Keyframes
	{
		/// <summary>s1 — punch squash keyframe (scale reaches squashScale, opacity 1).</summary>
		public readonly float squashTime;
		/// <summary>s2 — punch overshoot keyframe (scale reaches overshootScale).</summary>
		public readonly float overshootTime;
		/// <summary>s3 — punch settle keyframe (scale reaches 1), end of the punch phase.</summary>
		public readonly float punchEndTime;
		/// <summary>End of the hold phase; the float-up + fade-out starts here.</summary>
		public readonly float holdEndTime;
		/// <summary>End of the whole animation; the floater is fully faded out.</summary>
		public readonly float totalTime;

		public Keyframes(float squashTime, float overshootTime, float punchEndTime, float holdEndTime, float totalTime)
		{
			this.squashTime = squashTime;
			this.overshootTime = overshootTime;
			this.punchEndTime = punchEndTime;
			this.holdEndTime = holdEndTime;
			this.totalTime = totalTime;
		}
	}

	/// <summary>
	/// Computes the keyframe times from the three phase durations. Negative inputs
	/// are clamped to 0 so the times stay monotonically non-decreasing.
	/// </summary>
	public static Keyframes Compute(float punchInTime, float holdTime, float fadeTime)
	{
		float punchEnd = Mathf.Max(0f, punchInTime);
		float holdEnd = punchEnd + Mathf.Max(0f, holdTime);
		float total = holdEnd + Mathf.Max(0f, fadeTime);
		return new Keyframes(punchEnd * PunchSquashRatio, punchEnd * PunchOvershootRatio, punchEnd, holdEnd, total);
	}
}
