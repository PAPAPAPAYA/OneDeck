using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for HPNumericCounter. Golden values ported 1:1 from
/// docs/demo/HPNumericDisplayDemo.html (stepSizeFor / stepDelay with the demo
/// constants STEP_MS=50, TARGET_COUNT_MS=500, EASE_OUT_POINTS=5,
/// EASE_OUT_EXTRA_MS=35).
/// Plan: plans/plan-hp-numeric-display-2026-07-19.md, verification item 1.
/// </summary>
public class HPNumericCounterTests
{
	// Golden: step size is always 1 inside the ease-out tail (remaining <= 5).
	[Test]
	public void StepSize_One_InsideEaseOutTail()
	{
		for (int remaining = 1; remaining <= 5; remaining++)
		{
			Assert.AreEqual(1, HPNumericCounter.StepSizeFor(remaining), "remaining " + remaining);
		}
	}

	// Golden: multi-point fast steps above the tail (fastSteps = floor(500/50) = 10).
	[Test]
	public void StepSize_GoldenValues_FromDemo()
	{
		Assert.AreEqual(10, HPNumericCounter.StepSizeFor(100));
		Assert.AreEqual(9, HPNumericCounter.StepSizeFor(90));
		Assert.AreEqual(9, HPNumericCounter.StepSizeFor(81));
		Assert.AreEqual(8, HPNumericCounter.StepSizeFor(72));
		Assert.AreEqual(7, HPNumericCounter.StepSizeFor(64));
		Assert.AreEqual(6, HPNumericCounter.StepSizeFor(57));
		Assert.AreEqual(6, HPNumericCounter.StepSizeFor(51));
		Assert.AreEqual(5, HPNumericCounter.StepSizeFor(45));
		Assert.AreEqual(4, HPNumericCounter.StepSizeFor(40));
		Assert.AreEqual(4, HPNumericCounter.StepSizeFor(36));
		Assert.AreEqual(2, HPNumericCounter.StepSizeFor(11));
		Assert.AreEqual(1, HPNumericCounter.StepSizeFor(10));
		Assert.AreEqual(1, HPNumericCounter.StepSizeFor(6));
	}

	// Golden: a full 100 -> 0 count follows this exact step-size sequence in the demo
	// (multi-point fast phase, then per-point tail).
	[Test]
	public void FullCount_From100_MatchesDemoStepSequence()
	{
		int[] expectedSteps = { 10, 9, 9, 8, 7, 6, 6, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
		int displayed = 100;
		int steps = 0;
		while (displayed != 0)
		{
			int remaining = Mathf.Abs(0 - displayed);
			int step = Mathf.Min(remaining, HPNumericCounter.StepSizeFor(remaining));
			Assert.Less(steps, expectedSteps.Length, "more steps than the golden sequence");
			Assert.AreEqual(expectedSteps[steps], step, "step " + steps);
			displayed -= step;
			steps++;
		}
		Assert.AreEqual(expectedSteps.Length, steps, "total step count");
		Assert.AreEqual(0, displayed);
	}

	// Golden: stretched tail delays = 50 + (5 + 1 - remaining) * 35.
	[Test]
	public void StepDelay_GoldenValues_FromDemo()
	{
		Assert.AreEqual(85, HPNumericCounter.StepDelay(5, true));
		Assert.AreEqual(120, HPNumericCounter.StepDelay(4, true));
		Assert.AreEqual(155, HPNumericCounter.StepDelay(3, true));
		Assert.AreEqual(190, HPNumericCounter.StepDelay(2, true));
		Assert.AreEqual(225, HPNumericCounter.StepDelay(1, true));
	}

	[Test]
	public void StepDelay_FlatAboveTail_AndWhenEaseOutOff()
	{
		Assert.AreEqual(50, HPNumericCounter.StepDelay(6, true));
		Assert.AreEqual(50, HPNumericCounter.StepDelay(100, true));
		Assert.AreEqual(50, HPNumericCounter.StepDelay(5, false));
		Assert.AreEqual(50, HPNumericCounter.StepDelay(1, false));
	}
}
