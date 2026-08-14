using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for DeckArcLoopLayout and the DeckPositionCalculator arc-loop branch.
/// Plan: plans/plan-arc-loop-deck-layout-2026-08-12.md, section 8.1.
/// Golden values generated from docs/demo/CardArcArrangementDemo.html with DefaultParams
/// (8 cards) via browser evaluation of the demo's own slotPosition/computeZOrder, then
/// converted by the documented rule: unityOffset = (canvasDx, -canvasDy) * pxToWorld,
/// relative to the deck-top slot.
/// </summary>
public class DeckArcLoopLayoutTests
{
	private const float PxToWorld = 0.01f;
	private const float PosEpsilon = 1e-3f;   // world units; absorbs float(C#) vs double(JS) precision drift only
	private const float ScaleEpsilon = 1e-4f;

	private static DeckArcLoopLayout.Params DefaultParams => new DeckArcLoopLayout.Params
	{
		radiusX = 195f,
		radiusY = 155f,
		exponent = 2f,
		tiltDeg = 45f,
		curveDensity = 3f,
		minScale = 0.7f,
		scalePower = 1f,
		mirror = false,
		arcSamples = 720
	};

	// Golden table, indexed by unity deck index (0 = deck bottom, 7 = deck top), 8 cards, default params.
	private static readonly float[] GoldenX = { 1.4252611f, 2.1519443f, 1.8143455f, 0.7936400f, -0.6316211f, -1.3583043f, -1.0207055f, 0f };
	private static readonly float[] GoldenY = { 0.6003575f, 2.0129470f, 3.0997717f, 3.5227828f, 2.9224253f, 1.5098358f, 0.4230111f, 0f };
	private static readonly float[] GoldenScale = { 0.9488736f, 0.8285775f, 0.7360236f, 0.7000000f, 0.7511264f, 0.8714225f, 0.9639764f, 1f };
	private static readonly int[] GoldenRank = { 5, 3, 1, 0, 2, 4, 6, 7 };

	// B1: ComputeSlots matches the demo golden table for the same (deckCount, Params).
	[Test]
	public void B1_Offsets_MatchDemoGoldenValues()
	{
		var slots = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		Assert.AreEqual(8, slots.Length, "slot count");
		for (int k = 0; k < 8; k++)
		{
			Assert.AreEqual(GoldenX[k], slots[k].offset.x, PosEpsilon, "offset x at unity index " + k);
			Assert.AreEqual(GoldenY[k], slots[k].offset.y, PosEpsilon, "offset y at unity index " + k);
		}
	}

	[Test]
	public void B1_Scales_MatchDemoGoldenValues()
	{
		var slots = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		for (int k = 0; k < 8; k++)
		{
			Assert.AreEqual(GoldenScale[k], slots[k].scale, ScaleEpsilon, "scale at unity index " + k);
			Assert.AreEqual(GoldenScale[k], DeckArcLoopLayout.ComputeScale(k, 8, DefaultParams, PxToWorld), ScaleEpsilon, "ComputeScale at unity index " + k);
		}
	}

	[Test]
	public void B1_DepthRanks_MatchDemoGoldenValues()
	{
		var slots = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		for (int k = 0; k < 8; k++)
			Assert.AreEqual(GoldenRank[k], slots[k].depthRank, "depthRank at unity index " + k);
	}

	// B2: edge deck counts never throw and never produce NaN.
	[Test]
	public void B2_EdgeCounts_DoNotThrow()
	{
		Assert.AreEqual(0, DeckArcLoopLayout.ComputeSlots(0, DefaultParams, PxToWorld).Length, "deckCount 0");

		var single = DeckArcLoopLayout.ComputeSlots(1, DefaultParams, PxToWorld);
		Assert.AreEqual(1, single.Length, "deckCount 1 length");
		Assert.AreEqual(Vector2.zero, single[0].offset, "deckCount 1 at anchor");
		Assert.AreEqual(1f, single[0].scale, "deckCount 1 scale");
		Assert.AreEqual(0, single[0].depthRank, "deckCount 1 rank");
		Assert.AreEqual(1f, DeckArcLoopLayout.ComputeScale(0, 1, DefaultParams, PxToWorld), "deckCount 1 ComputeScale");

		var two = DeckArcLoopLayout.ComputeSlots(2, DefaultParams, PxToWorld);
		Assert.AreEqual(2, two.Length, "deckCount 2 length");
		AssertNoNaN(two, "deckCount 2");

		var many = DeckArcLoopLayout.ComputeSlots(30, DefaultParams, PxToWorld);
		Assert.AreEqual(30, many.Length, "deckCount 30 length");
		AssertNoNaN(many, "deckCount 30");
	}

	private static void AssertNoNaN(DeckArcLoopLayout.Slot[] slots, string label)
	{
		foreach (var s in slots)
		{
			Assert.IsFalse(float.IsNaN(s.offset.x) || float.IsNaN(s.offset.y), label + " produced NaN");
			Assert.IsFalse(float.IsInfinity(s.offset.x) || float.IsInfinity(s.offset.y), label + " produced Infinity");
			Assert.IsFalse(float.IsNaN(s.scale), label + " produced NaN scale");
		}
	}

	// B3: deck top sits at the anchor (zero offset); deck bottom (slot 0) is right of it
	// when mirror is off, left when mirrored.
	[Test]
	public void B3_DeckTopAtAnchor_DeckBottomRightOfTop()
	{
		var slots = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		Assert.AreEqual(Vector2.zero, slots[7].offset, "deck top at anchor");
		Assert.Greater(slots[0].offset.x, 0f, "deck bottom right of deck top (mirror off)");
		Assert.AreEqual(7, slots[7].depthRank, "deck top is front-most");
		Assert.AreEqual(1f, slots[7].scale, "deck top scale");

		var mirrored = DefaultParams;
		mirrored.mirror = true;
		var slotsM = DeckArcLoopLayout.ComputeSlots(8, mirrored, PxToWorld);
		Assert.AreEqual(Vector2.zero, slotsM[7].offset, "deck top at anchor (mirrored)");
		Assert.Less(slotsM[0].offset.x, 0f, "deck bottom left of deck top (mirrored)");
	}

	// B4: curveDensity = 0 spreads slots evenly (chord distances roughly equal); w = 3
	// packs the bends tighter (min consecutive chord shrinks).
	[Test]
	public void B4_CurveDensity_ZeroIsUniform_WeightPacksBends()
	{
		var uniform = DefaultParams;
		uniform.curveDensity = 0f;
		var slotsU = DeckArcLoopLayout.ComputeSlots(8, uniform, PxToWorld);
		float[] chordU = ConsecutiveChords(slotsU);
		float meanU = Average(chordU);
		foreach (float c in chordU)
			Assert.Less(Mathf.Abs(c - meanU) / meanU, 0.1f, "uniform arc length: chord deviates >10%");

		var slotsW = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		float[] chordW = ConsecutiveChords(slotsW);
		Assert.Less(Min(chordW), Min(chordU), "w=3 should pack the bends tighter than w=0");
		Assert.Greater(Max(chordW), Max(chordU), "w=3 should spread the straights wider than w=0");
	}

	private static float[] ConsecutiveChords(DeckArcLoopLayout.Slot[] slots)
	{
		int n = slots.Length;
		var chords = new float[n];
		for (int k = 0; k < n; k++)
		{
			Vector2 d = slots[(k + 1) % n].offset - slots[k].offset;
			chords[k] = d.magnitude;
		}
		return chords;
	}

	private static float Average(float[] v) { float s = 0f; foreach (float x in v) s += x; return s / v.Length; }
	private static float Min(float[] v) { float m = float.MaxValue; foreach (float x in v) if (x < m) m = x; return m; }
	private static float Max(float[] v) { float m = float.MinValue; foreach (float x in v) if (x > m) m = x; return m; }

	// B5: scale is height-driven like the z rank: deck top = 1, back-most (rank 0) = minScale,
	// and scale is non-decreasing as depthRank increases.
	[Test]
	public void B5_Scale_FollowsDepthRank()
	{
		var slots = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		var scaleByRank = new float[8];
		foreach (var s in slots)
			scaleByRank[s.depthRank] = s.scale;
		float prev = -1f;
		for (int rank = 0; rank < 8; rank++)
		{
			Assert.GreaterOrEqual(scaleByRank[rank], prev - ScaleEpsilon, "scale decreases toward the front at rank " + rank);
			prev = scaleByRank[rank];
		}
		Assert.AreEqual(DefaultParams.minScale, scaleByRank[0], ScaleEpsilon, "back-most scale == minScale");
		Assert.AreEqual(1f, scaleByRank[7], ScaleEpsilon, "front-most scale == 1");
	}

	// B6: ComputeOffsetAtCurveT walks deck top (t=0) around the loop to deck bottom (t=1), clamps.
	[Test]
	public void B6_ComputeOffsetAtCurveT()
	{
		var slots = DeckArcLoopLayout.ComputeSlots(8, DefaultParams, PxToWorld);
		Assert.AreEqual(slots[7].offset, DeckArcLoopLayout.ComputeOffsetAtCurveT(8, 0f, DefaultParams, PxToWorld), "t=0 = deck top");
		Assert.AreEqual(slots[0].offset, DeckArcLoopLayout.ComputeOffsetAtCurveT(8, 1f, DefaultParams, PxToWorld), "t=1 = deck bottom");
		Assert.AreEqual(slots[7].offset, DeckArcLoopLayout.ComputeOffsetAtCurveT(8, -0.5f, DefaultParams, PxToWorld), "t<0 clamps");
		Assert.AreEqual(slots[0].offset, DeckArcLoopLayout.ComputeOffsetAtCurveT(8, 1.5f, DefaultParams, PxToWorld), "t>1 clamps");
		Assert.AreEqual(Vector2.zero, DeckArcLoopLayout.ComputeOffsetAtCurveT(0, 0.5f, DefaultParams, PxToWorld), "deckCount 0");
	}

	// B7: calculator arc branch: x/y from slot offset, z from depthRank; disabled config
	// falls through to the legacy linear formula byte-for-byte.
	[Test]
	public void B7_Calculator_ArcBranch_PositionAndZ()
	{
		var cfg = new DeckPositionCalculator.ArcLoopConfig
		{
			enabled = true,
			layoutParams = DefaultParams,
			pxToWorld = PxToWorld
		};
		Vector3 basePos = new Vector3(10f, 20f, 5f);
		float zOffset = 0.1f;
		for (int k = 0; k < 8; k++)
		{
			Vector3 pos = DeckPositionCalculator.CalculatePositionAtIndex(k, 8, basePos, 0.5f, 0.5f, zOffset, null, cfg);
			Assert.AreEqual(basePos.x + GoldenX[k], pos.x, PosEpsilon, "x at unity index " + k);
			Assert.AreEqual(basePos.y + GoldenY[k], pos.y, PosEpsilon, "y at unity index " + k);
			Assert.AreEqual(basePos.z - zOffset * GoldenRank[k], pos.z, 1e-5f, "z at unity index " + k);
		}

		cfg.enabled = false;
		Vector3 legacy = DeckPositionCalculator.CalculatePositionAtIndex(3, 8, basePos, 0.5f, 0.5f, zOffset, null, cfg);
		Assert.AreEqual(basePos.x + 0.5f * (8 - 1 - 3), legacy.x, 1e-6f, "disabled config falls back to legacy linear x");
		Assert.AreEqual(basePos.z - zOffset * 3, legacy.z, 1e-6f, "disabled config falls back to legacy linear z");
	}
}
