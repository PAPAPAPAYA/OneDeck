using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for DeckCascadeLayout and the DeckPositionCalculator cascade branch.
/// PRD: plans/prd-card-cascade-layout-2026-07-17.md, section 6.1 (tests A1-A5).
/// Golden values generated from docs/demo/CardArrangementDemo.html via
/// tools/scripts/gen_cascade_golden.js (demo algorithm replicated verbatim).
/// </summary>
public class DeckCascadeLayoutTests
{
	private const float PxToWorld = 0.01f;
	private const float PosEpsilon = 1e-3f;   // world units; absorbs float(C#) vs double(JS) precision drift only
	private const float ScaleEpsilon = 1e-5f;

	private static DeckCascadeLayout.Params DefaultParams => new DeckCascadeLayout.Params
	{
		shrinkCount = 6,
		minScale = 0.55f,
		scalePower = 2f,
		startSpacingX = 60f,
		startSpacingY = 70f,
		minSpacingX = 8f,
		minSpacingY = 12f,
		spacingPower = 2f,
		tailReturn = 0.55f,
		tailBendSign = 1f,
		arcSamples = 300,
		coverageNormalize = true,
		coverageTarget = 0.62f,
		coverageCap = 2.5f
	};

	// Golden table, cascadeIndex 0 = front card. Demo canvas space (px, y-down), 20 cards, default params.
	// Regenerated with coverage normalization on: natural coverage at 20 cards is 60.4% < 62% target,
	// so stepFactor = 1.0269 (offsets shifted ~2.7% vs the pre-coverage table).
	private static readonly float[] DemoOffsetX = { 0.000000f, -27.688865f, -52.020592f, -73.241939f, -91.603136f, -107.357957f, -120.759201f, -132.061262f, -141.512913f, -149.352730f, -155.811779f, -161.101737f, -165.418197f, -168.935492f, -171.807315f, -174.162682f, -176.111604f, -177.742703f, -179.123840f, -180.300406f };
	private static readonly float[] DemoOffsetY = { 0.000000f, -33.208018f, -64.147127f, -92.862523f, -119.403541f, -143.824835f, -166.190375f, -186.572603f, -205.057250f, -221.745291f, -236.751033f, -250.206456f, -262.257953f, -273.066569f, -282.805762f, -291.660553f, -299.823957f, -307.496077f, -314.882447f, -322.193076f };
	private static readonly float[] DemoScale = { 1.000000f, 0.953878f, 0.910249f, 0.869114f, 0.830471f, 0.794321f, 0.760665f, 0.729501f, 0.700831f, 0.674654f, 0.650970f, 0.629778f, 0.611080f, 0.594875f, 0.581163f, 0.569945f, 0.561219f, 0.554986f, 0.551247f, 0.550000f };

	// Golden table for 6 cards, coverage normalization on: factor clamps to coverageCap (2.5).
	private static readonly float[] Demo6OffsetX = { 0.000000f, -51.123329f, -83.333573f, -102.783497f, -114.947334f, -124.554884f };
	private static readonly float[] Demo6OffsetY = { 0.000000f, -61.791394f, -103.826560f, -131.127175f, -149.325581f, -164.579252f };

	// A1: ComputeOffsets matches the demo golden table for the same (deckCount, Params).
	// Unity canonical = both axes negated (front up-right), scaled by pxToWorld.
	[Test]
	public void A1_Offsets_MatchDemoGoldenValues()
	{
		var offsets = DeckCascadeLayout.ComputeOffsets(20, DefaultParams, PxToWorld);
		Assert.AreEqual(20, offsets.Length, "offset count");
		for (int i = 0; i < 20; i++)
		{
			float expectedX = -DemoOffsetX[i] * PxToWorld;
			float expectedY = -DemoOffsetY[i] * PxToWorld;
			Assert.AreEqual(expectedX, offsets[i].x, PosEpsilon, "offset x at cascadeIndex " + i);
			Assert.AreEqual(expectedY, offsets[i].y, PosEpsilon, "offset y at cascadeIndex " + i);
		}
	}

	[Test]
	public void A1_Scales_MatchDemoGoldenValues()
	{
		for (int i = 0; i < 20; i++)
		{
			float scale = DeckCascadeLayout.ComputeScale(i, 20, DefaultParams);
			Assert.AreEqual(DemoScale[i], scale, ScaleEpsilon, "scale at cascadeIndex " + i);
		}
	}

	// A2: edge deck counts never throw and never produce NaN.
	[Test]
	public void A2_EdgeCounts_DoNotThrow()
	{
		Assert.AreEqual(0, DeckCascadeLayout.ComputeOffsets(0, DefaultParams, PxToWorld).Length, "deckCount 0");

		var single = DeckCascadeLayout.ComputeOffsets(1, DefaultParams, PxToWorld);
		Assert.AreEqual(1, single.Length, "deckCount 1 length");
		Assert.AreEqual(Vector2.zero, single[0], "deckCount 1 at anchor");
		Assert.AreEqual(1f, DeckCascadeLayout.ComputeScale(0, 1, DefaultParams), "deckCount 1 scale");

		var two = DeckCascadeLayout.ComputeOffsets(2, DefaultParams, PxToWorld);
		Assert.AreEqual(2, two.Length, "deckCount 2 length");
		AssertNoNaN(two, "deckCount 2");

		// deckCount < shrinkCount: shrinkCount clamps to deckCount-1
		var tiny = DeckCascadeLayout.ComputeOffsets(5, DefaultParams, PxToWorld);
		Assert.AreEqual(5, tiny.Length, "deckCount 5 length");
		AssertNoNaN(tiny, "deckCount 5 (clamped shrinkCount)");
	}

	private static void AssertNoNaN(Vector2[] offsets, string label)
	{
		foreach (var o in offsets)
		{
			Assert.IsFalse(float.IsNaN(o.x) || float.IsNaN(o.y), label + " produced NaN");
			Assert.IsFalse(float.IsInfinity(o.x) || float.IsInfinity(o.y), label + " produced Infinity");
		}
	}

	// A3: front-to-back scale is non-increasing, front = 1, tail >= minScale.
	[Test]
	public void A3_Scale_IsMonotonicNonIncreasing()
	{
		float prev = float.MaxValue;
		for (int i = 0; i < 20; i++)
		{
			float scale = DeckCascadeLayout.ComputeScale(i, 20, DefaultParams);
			Assert.LessOrEqual(scale, prev + ScaleEpsilon, "scale increases at cascadeIndex " + i);
			Assert.GreaterOrEqual(scale, DefaultParams.minScale - ScaleEpsilon, "scale below minScale at cascadeIndex " + i);
			prev = scale;
		}
		Assert.AreEqual(1f, DeckCascadeLayout.ComputeScale(0, 20, DefaultParams), "front card scale");
	}

	// A4: calculator applies the Y-flip + per-component direction mirror in a fixed order.
	// Default direction (-1, +1) reproduces the demo: front segment up-left (x <= 0, y >= 0).
	[Test]
	public void A4_Calculator_DirectionMirrorAndZ()
	{
		var basePos = new Vector3(10f, 20f, 30f);
		const int count = 20;
		var config = new DeckPositionCalculator.CascadeConfig
		{
			enabled = true,
			pxToWorld = PxToWorld,
			direction = new Vector2(-1f, 1f),
			layoutParams = DefaultParams
		};

		// Front card (unity index count-1, cascadeIndex 0) sits exactly at the anchor.
		Vector3 front = DeckPositionCalculator.CalculatePositionAtIndex(count - 1, count, basePos, 0.1f, 0.05f, 0.01f, config);
		Assert.AreEqual(basePos.x, front.x, PosEpsilon, "front x at anchor");
		Assert.AreEqual(basePos.y, front.y, PosEpsilon, "front y at anchor");

		// Deepest card (unity index 0, cascadeIndex 19): demo shape = up-left.
		Vector3 deep = DeckPositionCalculator.CalculatePositionAtIndex(0, count, basePos, 0.1f, 0.05f, 0.01f, config);
		Assert.Less(deep.x, basePos.x, "default direction sweeps left");
		Assert.Greater(deep.y, basePos.y, "default direction sweeps up (Y-flip applied, never upside-down)");

		// Flipping direction.x mirrors X exactly; Y unchanged.
		config.direction = new Vector2(1f, 1f);
		Vector3 mirrored = DeckPositionCalculator.CalculatePositionAtIndex(0, count, basePos, 0.1f, 0.05f, 0.01f, config);
		Assert.AreEqual(2f * basePos.x - deep.x, mirrored.x, PosEpsilon, "direction.x mirror");
		Assert.AreEqual(deep.y, mirrored.y, PosEpsilon, "direction.x mirror keeps y");
		Assert.Greater(mirrored.x, basePos.x, "mirrored direction sweeps right");

		// Z formula is unchanged: basePos.z - zOffset * index.
		Assert.AreEqual(basePos.z, deep.z, PosEpsilon, "z at index 0");
		Assert.AreEqual(basePos.z - 0.01f * (count - 1), front.z, PosEpsilon, "z at top index");
	}

	// A5: legacy branch (null or disabled config) returns the pre-change linear result byte-for-byte.
	[Test]
	public void A5_LegacyBranch_ByteForByte()
	{
		var basePos = new Vector3(1.5f, -2.5f, 3.5f);
		const float xOffset = 0.12f, yOffset = -0.07f, zOffset = 0.02f;
		const int count = 17;
		var disabled = new DeckPositionCalculator.CascadeConfig { enabled = false };

		for (int i = 0; i < count; i++)
		{
			var expected = new Vector3(
				basePos.x + xOffset * (count - 1 - i),
				basePos.y + yOffset * (count - 1 - i),
				basePos.z - zOffset * i);
			Assert.AreEqual(expected, DeckPositionCalculator.CalculatePositionAtIndex(i, count, basePos, xOffset, yOffset, zOffset), "null config at index " + i);
			Assert.AreEqual(expected, DeckPositionCalculator.CalculatePositionAtIndex(i, count, basePos, xOffset, yOffset, zOffset, disabled), "disabled config at index " + i);
			Assert.AreEqual(expected, DeckPositionCalculator.CalculatePositionAtIndex(i, count, basePos, xOffset, yOffset, zOffset, null), "explicit null at index " + i);
		}
	}

	// A6: coverage normalization (Plan B). 6 cards, default params: factor clamps to
	// coverageCap (2.5), so the small-deck walk still reaches the curve's hook region.
	[Test]
	public void A6_CoverageNormalize_SmallDeckMatchesGolden()
	{
		var offsets = DeckCascadeLayout.ComputeOffsets(6, DefaultParams, PxToWorld);
		Assert.AreEqual(6, offsets.Length, "offset count");
		for (int i = 0; i < 6; i++)
		{
			float expectedX = -Demo6OffsetX[i] * PxToWorld;
			float expectedY = -Demo6OffsetY[i] * PxToWorld;
			Assert.AreEqual(expectedX, offsets[i].x, PosEpsilon, "offset x at cascadeIndex " + i);
			Assert.AreEqual(expectedY, offsets[i].y, PosEpsilon, "offset y at cascadeIndex " + i);
		}
	}

	// A6b: disabling normalization keeps the short walk (pre-Plan-B behavior), and the
	// stretched walk moves the deepest card much further along the curve. Also proves
	// the result cache keys on the coverage fields (same deckCount, different Params).
	[Test]
	public void A6_CoverageNormalize_OffKeepsShortWalk()
	{
		var off = DefaultParams;
		off.coverageNormalize = false;
		var offsetsOn = DeckCascadeLayout.ComputeOffsets(6, DefaultParams, PxToWorld);
		var offsetsOff = DeckCascadeLayout.ComputeOffsets(6, off, PxToWorld);

		Assert.AreEqual(Vector2.zero, offsetsOn[0], "front card at anchor (on)");
		Assert.AreEqual(Vector2.zero, offsetsOff[0], "front card at anchor (off)");
		float magOn = offsetsOn[5].magnitude;
		float magOff = offsetsOff[5].magnitude;
		Assert.Greater(magOn, magOff * 1.5f, "normalization should stretch the small-deck walk");

		// Re-query with the original params: cache must not return the "off" result.
		var offsetsOnAgain = DeckCascadeLayout.ComputeOffsets(6, DefaultParams, PxToWorld);
		Assert.AreEqual(offsetsOn[5], offsetsOnAgain[5], "cache must key on coverage fields");
	}
}
