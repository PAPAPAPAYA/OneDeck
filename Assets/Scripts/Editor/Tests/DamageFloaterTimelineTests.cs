using NUnit.Framework;

/// <summary>
/// EditMode tests for DamageFloaterTimeline. Golden values ported 1:1 from
/// docs/demo/DamageFloaterDemo.html (rebuildKeyframes with the demo defaults
/// punchInTime=0.38, holdTime=0.45, fadeTime=0.36).
/// Plan: plans/plan-damage-floater-2026-07-26.md, verification item 1.
/// </summary>
public class DamageFloaterTimelineTests
{
	private const float Delta = 0.0001f;

	// Golden: demo defaults -> s1=0.1216, s2=0.2204, s3=0.38, holdEnd=0.83, total=1.19.
	[Test]
	public void Compute_GoldenKeyframeTimes_FromDemoDefaults()
	{
		DamageFloaterTimeline.Keyframes k = DamageFloaterTimeline.Compute(0.38f, 0.45f, 0.36f);
		Assert.AreEqual(0.1216f, k.squashTime, Delta, "s1 = 0.38 * 0.32");
		Assert.AreEqual(0.2204f, k.overshootTime, Delta, "s2 = 0.38 * 0.58");
		Assert.AreEqual(0.38f, k.punchEndTime, Delta, "s3 = punchInTime");
		Assert.AreEqual(0.83f, k.holdEndTime, Delta, "holdEnd = 0.38 + 0.45");
		Assert.AreEqual(1.19f, k.totalTime, Delta, "total = 0.38 + 0.45 + 0.36");
	}

	// Golden: waypoint magnitudes match the demo keyframes (CSS y -8/-14/-18/-22,
	// up-positive in Unity).
	[Test]
	public void Waypoints_MatchDemoKeyframes()
	{
		Assert.AreEqual(8f, DamageFloaterTimeline.WaypointSquashPx);
		Assert.AreEqual(14f, DamageFloaterTimeline.WaypointOvershootPx);
		Assert.AreEqual(18f, DamageFloaterTimeline.WaypointSettlePx);
		Assert.AreEqual(22f, DamageFloaterTimeline.WaypointHoldEndPx);
		Assert.Less(DamageFloaterTimeline.WaypointSquashPx, DamageFloaterTimeline.WaypointOvershootPx);
		Assert.Less(DamageFloaterTimeline.WaypointOvershootPx, DamageFloaterTimeline.WaypointSettlePx);
		Assert.Less(DamageFloaterTimeline.WaypointSettlePx, DamageFloaterTimeline.WaypointHoldEndPx);
	}

	// Keyframe times must be monotonically non-decreasing for any parameter combo,
	// including zeros (DOTween Insert ordering relies on it).
	[Test]
	public void Compute_TimesAreMonotonic_ForVariousParams()
	{
		float[,] combos =
		{
			{ 0.38f, 0.45f, 0.36f }, // demo defaults
			{ 0.1f, 0f, 0.1f },      // slider minimums
			{ 1f, 2f, 2f },          // slider maximums
			{ 0f, 0f, 0f },          // degenerate
			{ 0.5f, 0f, 0.25f },     // no hold
		};
		for (int i = 0; i < combos.GetLength(0); i++)
		{
			DamageFloaterTimeline.Keyframes k = DamageFloaterTimeline.Compute(combos[i, 0], combos[i, 1], combos[i, 2]);
			string label = "combo " + i;
			Assert.LessOrEqual(0f, k.squashTime, label);
			Assert.LessOrEqual(k.squashTime, k.overshootTime, label);
			Assert.LessOrEqual(k.overshootTime, k.punchEndTime, label);
			Assert.LessOrEqual(k.punchEndTime, k.holdEndTime, label);
			Assert.LessOrEqual(k.holdEndTime, k.totalTime, label);
		}
	}

	// Negative inputs are clamped to 0 instead of producing negative times.
	[Test]
	public void Compute_NegativeDurations_ClampedToZero()
	{
		DamageFloaterTimeline.Keyframes k = DamageFloaterTimeline.Compute(-0.38f, -0.45f, -0.36f);
		Assert.AreEqual(0f, k.squashTime, Delta);
		Assert.AreEqual(0f, k.overshootTime, Delta);
		Assert.AreEqual(0f, k.punchEndTime, Delta);
		Assert.AreEqual(0f, k.holdEndTime, Delta);
		Assert.AreEqual(0f, k.totalTime, Delta);
	}
}
