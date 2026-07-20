using UnityEngine;

/// <summary>
/// Adaptive count-up/down tick math for HPNumericDisplay, ported 1:1 from
/// docs/demo/HPNumericDisplayDemo.html (stepSizeFor / stepDelay) so EditMode tests
/// can pin golden values. Pure static, unit-testable (same pattern as
/// DeckCascadeLayout).
/// Plan: plans/plan-hp-numeric-display-2026-07-19.md
/// </summary>
public static class HPNumericCounter
{
	public const int DefaultStepMs = 50;
	public const int DefaultTargetCountMs = 500;
	public const int DefaultEaseOutPoints = 5;
	public const int DefaultEaseOutExtraMs = 35;

	/// <summary>
	/// Points to advance per tick. 1 while inside the ease-out tail; otherwise sized
	/// so the bulk of the count finishes inside targetCountMs (fastSteps ticks).
	/// </summary>
	public static int StepSizeFor(int remaining, int stepMs = DefaultStepMs, int targetCountMs = DefaultTargetCountMs, int easeOutPoints = DefaultEaseOutPoints)
	{
		if (remaining <= easeOutPoints)
		{
			return 1;
		}
		int fastSteps = Mathf.Max(1, Mathf.FloorToInt(targetCountMs / (float)stepMs));
		return Mathf.Max(1, Mathf.CeilToInt(remaining / (float)fastSteps));
	}

	/// <summary>
	/// Delay (ms) before the next tick. Flat stepMs outside the tail; stretched by
	/// easeOutExtraMs per point inside the last easeOutPoints points.
	/// </summary>
	public static int StepDelay(int remaining, bool easeOut, int stepMs = DefaultStepMs, int easeOutPoints = DefaultEaseOutPoints, int easeOutExtraMs = DefaultEaseOutExtraMs)
	{
		if (!easeOut || remaining > easeOutPoints)
		{
			return stepMs;
		}
		return stepMs + (easeOutPoints + 1 - remaining) * easeOutExtraMs;
	}
}
