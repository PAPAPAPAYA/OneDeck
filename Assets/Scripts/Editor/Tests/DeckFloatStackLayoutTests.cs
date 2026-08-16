using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for DeckFloatStackLayout and the DeckPositionCalculator float-stack branch.
/// Plans: plans/plan-float-stack-reveal-layout-2026-08-13.md,
///        plans/plan-float-stack-center-scale-2026-08-15.md (center/compress/bottom-limit revision).
/// Golden values generated from docs/demo/CardStackRevealDemo.html with DefaultParams/FramedParams
/// via the demo's own computeFrame + roleTransform, converted by the documented rule:
/// unityOffset = (canvasDx, -canvasDy) * pxToWorld.
/// </summary>
public class DeckFloatStackLayoutTests
{
	private const float PxToWorld = 0.01f;
	private const float PosEpsilon = 1e-4f;

	// Centering-only baseline: compression and bottom limit disabled.
	private static DeckFloatStackLayout.Params DefaultParams => new DeckFloatStackLayout.Params
	{
		stepY = 14f,
		revealScale = 1.07f,
		revealFloatX = 16f,
		revealUpY = -14f,
		maxHeightPx = 0f,
		minScale = 0.55f,
		bottomLimitPx = 0f,
		cardHalfHeightPx = 105f
	};

	// Full feature set (demo defaults): compression + bottom limit active.
	private static DeckFloatStackLayout.Params FramedParams => new DeckFloatStackLayout.Params
	{
		stepY = 14f,
		revealScale = 1.07f,
		revealFloatX = 16f,
		revealUpY = -14f,
		maxHeightPx = 250f,
		minScale = 0.55f,
		bottomLimitPx = 270f,
		cardHalfHeightPx = 105f
	};

	// Golden centered stack offsets (unity index 0 = deck bottom, 7 = next to reveal),
	// 8 cards, compression/limit disabled: y = 14·(8 − 2j − 1)/2 px, symmetric around the anchor.
	private static readonly float[] GoldenY = { 0.49f, 0.35f, 0.21f, 0.07f, -0.07f, -0.21f, -0.35f, -0.49f };

	// C1: slot offsets match the centered golden table (index mapping + y sign conversion).
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

	// C2: reveal pose derives from the shadow home (slot 0): y = upY − step·(count+1)/2.
	[Test]
	public void C2_RevealPose_DerivesFromSlot0()
	{
		Vector2 offset = DeckFloatStackLayout.ComputeRevealOffset(8, DefaultParams, PxToWorld);
		Assert.AreEqual(-0.16f, offset.x, PosEpsilon, "reveal offset x");
		Assert.AreEqual(-0.77f, offset.y, PosEpsilon, "reveal offset y = upY (−14) − step·4.5 (63) below the anchor");
		// Same value via the shadow home helper: reveal = slot0 + upY.
		float slot0 = DeckFloatStackLayout.ComputeShadowHomeYPx(8, DefaultParams);
		Assert.AreEqual((DefaultParams.revealUpY + slot0) * PxToWorld, offset.y, PosEpsilon, "reveal = slot0 + upY");
	}

	// C3: negative stepY flips the centered stack downward (pure centering, no scale/limit).
	[Test]
	public void C3_NegativeStepY_StacksDownward()
	{
		var down = DefaultParams;
		down.stepY = -14f;
		Vector2 next = DeckFloatStackLayout.ComputeSlotOffset(7, 8, down, PxToWorld);
		Vector2 bottom = DeckFloatStackLayout.ComputeSlotOffset(0, 8, down, PxToWorld);
		Assert.Greater(next.y, 0f, "next-to-reveal card above the anchor with negative stepY");
		Assert.Less(bottom.y, next.y, "deck bottom is the lowest slot with negative stepY");
		Assert.AreEqual(-GoldenY[7], next.y, PosEpsilon, "magnitude matches the positive-step layout");
	}

	// C4: edge deck counts never throw and never produce NaN.
	[Test]
	public void C4_EdgeCounts_DoNotThrow()
	{
		Assert.AreEqual(Vector2.zero, DeckFloatStackLayout.ComputeSlotOffset(0, 0, DefaultParams, PxToWorld), "deckCount 0");

		Vector2 single = DeckFloatStackLayout.ComputeSlotOffset(0, 1, DefaultParams, PxToWorld);
		Assert.AreEqual(0f, single.y, PosEpsilon, "deckCount 1 sits exactly at the anchor (centered)");

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

	// F1: disabled knobs keep the frame neutral; disabling compression alone does NOT
	// disable the pin (the uncompressed drop is even larger, so the pin engages).
	[Test]
	public void F1_Frame_DisabledKnobs_Neutral()
	{
		var p = FramedParams;
		p.maxHeightPx = 0f;
		var f = DeckFloatStackLayout.ComputeFrame(24, p);
		Assert.AreEqual(1f, f.globalScale, PosEpsilon, "maxHeightPx 0 disables compression");
		// scale 1, step 14: deckBottom = 161+105 = 266; revealBottom = 175+14+112.35 = 301.35.
		Assert.AreEqual(31.35f, f.liftPx, 1e-3f, "pin still active with compression off: lift = 301.35 − 270");

		p = FramedParams;
		p.bottomLimitPx = 0f;
		f = DeckFloatStackLayout.ComputeFrame(59, p);
		Assert.AreEqual(0.55f, f.globalScale, PosEpsilon, "compression still applies");
		Assert.AreEqual(0f, f.liftPx, PosEpsilon, "bottomLimitPx 0 disables the pin");
	}

	// F2: compression onset — small decks keep scale 1; count 24 compresses but does not lift.
	[Test]
	public void F2_Frame_CompressionOnset()
	{
		var small = DeckFloatStackLayout.ComputeFrame(7, FramedParams);
		Assert.AreEqual(1f, small.globalScale, PosEpsilon, "7 cards: raw height 98 < 250 keeps scale 1");

		var f = DeckFloatStackLayout.ComputeFrame(24, FramedParams);
		Assert.AreEqual(250f / 336f, f.globalScale, 1e-4f, "24 cards: scale = 250/(14·24)");
		Assert.AreEqual(14f * (250f / 336f), f.effectiveStepY, 1e-3f, "step is bound to the scale");
		Assert.AreEqual(0f, f.liftPx, PosEpsilon, "lowest card-face point 227.8 < 270 stays unpinned");
	}

	// F3: minScale floor.
	[Test]
	public void F3_Frame_ScaleFloor()
	{
		var f = DeckFloatStackLayout.ComputeFrame(59, FramedParams);
		Assert.AreEqual(0.55f, f.globalScale, PosEpsilon, "59 cards: 250/826 = 0.30 floors at 0.55");
		Assert.AreEqual(7.7f, f.effectiveStepY, 1e-4f, "floored step = 14·0.55");
	}

	// F4: bottom pin with the REVEALED card's bottom edge as the binding constraint (upY = −14).
	[Test]
	public void F4_Frame_Lift_RevealEdgeBinds()
	{
		// count 59, scale 0.55, step 7.7: deckBottom = 223.3+57.75 = 281.05;
		// revealBottom = 231+14+61.7925 = 306.7925 -> lift = 306.7925 − 270.
		var f = DeckFloatStackLayout.ComputeFrame(59, FramedParams);
		Assert.AreEqual(36.7925f, f.liftPx, 1e-3f, "lift pins the revealed card bottom edge at 270");

		Vector2 top = DeckFloatStackLayout.ComputeSlotOffset(58, 59, FramedParams, PxToWorld);
		Assert.AreEqual(-1.865075f, top.y, 1e-4f, "lowest stack slot y = −223.3 + 36.7925 px");
		Vector2 reveal = DeckFloatStackLayout.ComputeRevealOffset(59, FramedParams, PxToWorld);
		Assert.AreEqual(-2.082075f, reveal.y, 1e-4f, "reveal y = −14 − 231 + 36.7925 px");
	}

	// F5: bottom pin with the DECK bottom edge as the binding constraint (upY lifted high).
	[Test]
	public void F5_Frame_Lift_DeckEdgeBinds()
	{
		var p = FramedParams;
		p.revealUpY = 70f; // reveal card floats high; deck edge becomes the lowest card face
		// revealBottom = 231−70+61.7925 = 222.7925 < deckBottom 281.05 -> lift = 281.05 − 270.
		var f = DeckFloatStackLayout.ComputeFrame(59, p);
		Assert.AreEqual(11.05f, f.liftPx, 1e-3f, "lift pins the deck bottom edge at 270");
	}

	// F6: negative stepY skips scale/limit (pure centering).
	[Test]
	public void F6_Frame_NegativeStepY_Neutral()
	{
		var p = FramedParams;
		p.stepY = -14f;
		var f = DeckFloatStackLayout.ComputeFrame(59, p);
		Assert.AreEqual(1f, f.globalScale, PosEpsilon, "negative stepY disables compression");
		Assert.AreEqual(0f, f.liftPx, PosEpsilon, "negative stepY disables the pin");
	}

	// F7: centered slots are symmetric; lift shifts every slot by the same amount.
	[Test]
	public void F7_Slots_SymmetryAndUniformLift()
	{
		for (int j = 0; j < 8; j++)
		{
			float a = DeckFloatStackLayout.ComputeSlotOffset(j, 8, DefaultParams, PxToWorld).y;
			float b = DeckFloatStackLayout.ComputeSlotOffset(7 - j, 8, DefaultParams, PxToWorld).y;
			Assert.AreEqual(-b, a, PosEpsilon, "symmetry at unity index " + j);
		}

		var noLimit = FramedParams;
		noLimit.bottomLimitPx = 0f;
		float lift = DeckFloatStackLayout.ComputeFrame(59, FramedParams).liftPx;
		foreach (int j in new[] { 0, 30, 58 })
		{
			float lifted = DeckFloatStackLayout.ComputeSlotOffset(j, 59, FramedParams, PxToWorld).y;
			float plain = DeckFloatStackLayout.ComputeSlotOffset(j, 59, noLimit, PxToWorld).y;
			Assert.AreEqual(lift * PxToWorld, lifted - plain, 1e-4f, "lift is uniform at unity index " + j);
		}
	}
}
