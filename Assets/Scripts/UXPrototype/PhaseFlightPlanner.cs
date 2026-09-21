using UnityEngine;

/// <summary>
/// Pure math for the phase transition (plan-phase-transition-world-camera-2026-09-21; demo:
/// docs/demo/PhaseTransitionDemo.html). All demo-px conversions and flight-schedule formulas
/// live here and only here so EditMode goldens can pin them 1:1 against the demo.
/// Demo page: 420x740 px, camera covers exactly one page; Unity page height = 2*orthoSize.
/// </summary>
public static class PhaseFlightPlanner
{
	/// <summary>Demo page height in px (PAGE_H, PhaseTransitionDemo.html:536).</summary>
	public const float DemoPageHeightPx = 740f;

	/// <summary>World height of one page: the orthographic camera's full vertical view.</summary>
	public static float PageHeightWorld(float orthographicSize)
	{
		return 2f * orthographicSize;
	}

	/// <summary>Demo px to world units: the demo page maps exactly onto one screen height.</summary>
	public static float PxToWorld(float demoPx, float pageHeightWorld)
	{
		return demoPx / DemoPageHeightPx * pageHeightWorld;
	}

	/// <summary>Demo px to canvas reference px (same page fraction; refHeight = Screen.height / canvas.scaleFactor).</summary>
	public static float DemoPxToCanvasPx(float demoPx, float canvasRefHeightPx)
	{
		return demoPx / DemoPageHeightPx * canvasRefHeightPx;
	}

	/// <summary>
	/// Flight arc apex (demo fly(): midpoint lifted by cardArc, offset 0.5, :676-682).
	/// World y-up: the apex is ABOVE the from/to midpoint.
	/// </summary>
	public static Vector3 ArcApex(Vector3 from, Vector3 to, float arcWorld)
	{
		Vector3 mid = (from + to) * 0.5f;
		mid.y += arcWorld;
		return mid;
	}

	/// <summary>Per-card stagger delay (demo: delay = i * cardStagger, :719).</summary>
	public static float FlightDelay(int index, float stagger)
	{
		return index * stagger;
	}

	/// <summary>Face-down flip time within one flight (demo flipAtMid: delay + transDur/2, :692-694).</summary>
	public static float FlipTime(float delay, float duration)
	{
		return delay + duration * 0.5f;
	}

	/// <summary>Total transition wait: camera duration plus the stagger tail (demo: transDur + 2*cardStagger, :739).</summary>
	public static float TotalDuration(float duration, float stagger)
	{
		return duration + 2f * stagger;
	}
}
