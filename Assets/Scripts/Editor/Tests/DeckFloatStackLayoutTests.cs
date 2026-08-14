using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for DeckFloatStackLayout and the DeckPositionCalculator float-stack branch.
/// Plan: plans/plan-float-stack-reveal-layout-2026-08-13.md, section 6.1.
/// Golden values generated from docs/demo/CardStackRevealDemo.html with DefaultParams
/// (8 cards) via browser evaluation of the demo's own roleTransform, then converted by
/// the documented rule: unityOffset = (canvasDx, -canvasDy) * pxToWorld.
/// </summary>
public class DeckFloatStackLayoutTests
{
	private const float PxToWorld = 0.01f;
	private const float PosEpsilon = 1e-4f;

	private static DeckFloatStackLayout.Params DefaultParams => new DeckFloatStackLayout.Params
	{
		stepY = 14f,
		revealScale = 1.07f,
		revealFloatX = 16f,
		revealUpY = -14f
	};

	// Golden stack offsets (unity index 0 = deck bottom, 7 = next to reveal), 8 cards, defaults.
	private static readonly float[] GoldenY = { 1.12f, 0.98f, 0.84f, 0.70f, 0.56f, 0.42f, 0.28f, 0.14f };

	// C1: slot offsets match the demo golden table (index mapping + y sign conversion).
	[Test]
	public void C1_SlotOffsets_MatchDemoGoldenValues()
	{
		for (int j = 0; j < 8; j++)
		{
			Vector2 offset = DeckFloatStackLayout.ComputeSlotOffset(j, 8, DefaultParams, PxToWorld);
			Assert.AreEqual(0f, offset.x, PosEpsilon, "x at unity index " + j);
			Assert.AreEqual(GoldenY[j], offset.y, PosEpsilon, "y at unity index " + j);
		}
	}

	// C2: reveal pose from the demo defaults; negative upY stays negative after conversion.
	[Test]
	public void C2_RevealPose_MatchesDemo()
	{
		Vector2 offset = DeckFloatStackLayout.ComputeRevealOffset(DefaultParams, PxToWorld);
		Assert.AreEqual(-0.16f, offset.x, PosEpsilon, "reveal offset x");
		Assert.AreEqual(-0.14f, offset.y, PosEpsilon, "reveal offset y (upY = -14 stays below the anchor)");
	}

	// C3: negative stepY flips the stack downward.
	[Test]
	public void C3_NegativeStepY_StacksDownward()
	{
		var down = DefaultParams;
		down.stepY = -14f;
		Vector2 next = DeckFloatStackLayout.ComputeSlotOffset(7, 8, down, PxToWorld);
		Vector2 bottom = DeckFloatStackLayout.ComputeSlotOffset(0, 8, down, PxToWorld);
		Assert.Less(next.y, 0f, "next-to-reveal card below the anchor with negative stepY");
		Assert.Less(bottom.y, next.y, "deck bottom is the lowest slot with negative stepY");
		Assert.AreEqual(-GoldenY[7], next.y, PosEpsilon, "magnitude matches the positive-step layout");
	}

	// C4: edge deck counts never throw and never produce NaN.
	[Test]
	public void C4_EdgeCounts_DoNotThrow()
	{
		Assert.AreEqual(Vector2.zero, DeckFloatStackLayout.ComputeSlotOffset(0, 0, DefaultParams, PxToWorld), "deckCount 0");

		Vector2 single = DeckFloatStackLayout.ComputeSlotOffset(0, 1, DefaultParams, PxToWorld);
		Assert.AreEqual(DefaultParams.stepY * PxToWorld, single.y, PosEpsilon, "deckCount 1 sits one step above the anchor");

		var many = DeckFloatStackLayout.ComputeSlotOffset(29, 30, DefaultParams, PxToWorld);
		Assert.IsFalse(float.IsNaN(many.x) || float.IsNaN(many.y), "deckCount 30 NaN");

		// index clamping beyond the range
		var clamped = DeckFloatStackLayout.ComputeSlotOffset(99, 8, DefaultParams, PxToWorld);
		Assert.AreEqual(GoldenY[7], clamped.y, PosEpsilon, "index clamps to the top slot");
	}

	// C5: calculator float-stack branch: x/y from slot offset, z passthrough by index;
	// disabled config falls through to the legacy linear formula byte-for-byte.
	[Test]
	public void C5_Calculator_FloatStackBranch_PositionAndZ()
	{
		var cfg = new DeckPositionCalculator.FloatStackConfig
		{
			enabled = true,
			layoutParams = DefaultParams,
			pxToWorld = PxToWorld
		};
		Vector3 basePos = new Vector3(10f, 20f, 5f);
		float zOffset = 0.1f;
		for (int j = 0; j < 8; j++)
		{
			Vector3 pos = DeckPositionCalculator.CalculatePositionAtIndex(j, 8, basePos, 0.5f, 0.5f, zOffset, null, null, cfg);
			Assert.AreEqual(basePos.x, pos.x, PosEpsilon, "x at unity index " + j);
			Assert.AreEqual(basePos.y + GoldenY[j], pos.y, PosEpsilon, "y at unity index " + j);
			Assert.AreEqual(basePos.z - zOffset * j, pos.z, 1e-5f, "z at unity index " + j);
		}

		cfg.enabled = false;
		Vector3 legacy = DeckPositionCalculator.CalculatePositionAtIndex(3, 8, basePos, 0.5f, 0.5f, zOffset, null, null, cfg);
		Assert.AreEqual(basePos.x + 0.5f * (8 - 1 - 3), legacy.x, 1e-6f, "disabled config falls back to legacy linear x");
		Assert.AreEqual(basePos.z - zOffset * 3, legacy.z, 1e-6f, "disabled config falls back to legacy linear z");
	}
}
