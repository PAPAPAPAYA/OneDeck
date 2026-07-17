using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for DeckCascadeLayout and the DeckPositionCalculator cascade branch.
/// PRD: plans/prd-card-cascade-layout-2026-07-17.md, section 6.1 (tests A1-A5).
/// Golden values generated from docs/CardArrangementDemo.html via
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
		arcSamples = 300
	};

	// Golden table, cascadeIndex 0 = front card. Demo canvas space (px, y-down), 20 cards, default params.
	private static readonly float[] DemoOffsetX = { 0.000000f, -26.976117f, -50.708670f, -71.437442f, -89.407633f, -104.864177f, -118.053650f, -129.221152f, -138.604595f, -146.433537f, -152.927459f, -158.290250f, -162.707804f, -166.347683f, -169.355862f, -171.861251f, -173.968229f, -175.768123f, -177.330278f, -178.706468f };
	private static readonly float[] DemoOffsetY = { 0.000000f, -32.329032f, -62.428867f, -90.346299f, -116.130639f, -139.838936f, -161.535366f, -181.294006f, -199.202259f, -215.360946f, -229.884581f, -242.902873f, -254.560130f, -265.013638f, -274.433029f, -282.997010f, -290.893967f, -298.317209f, -305.466257f, -312.544636f };
	private static readonly float[] DemoScale = { 1.000000f, 0.953878f, 0.910249f, 0.869114f, 0.830471f, 0.794321f, 0.760665f, 0.729501f, 0.700831f, 0.674654f, 0.650970f, 0.629778f, 0.611080f, 0.594875f, 0.581163f, 0.569945f, 0.561219f, 0.554986f, 0.551247f, 0.550000f };

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
}
