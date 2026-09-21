using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode goldens for ShopPageHud.CanvasPxToWorld (plan-shop-topbar-world-scroll-2026-09-21
/// §3.2/§6): the pure canvas-px -> world-units calibration used by the world avatar + HP pill
/// mirror. Reference numbers from the plan §6 (1080 px screen, orthoSize 6.06, scaleFactor 1).
/// Pure static math — no scene objects, nothing to dirty.
/// </summary>
public class ShopPageHudTests
{
	private const float Epsilon = 1e-4f;

	[Test]
	public void A1_HundredPxAtReferenceResolution()
	{
		// 100 px -> 100/1080 x 12.12 = 1.1222 u (plan §6 golden).
		Assert.AreEqual(100f / 1080f * 12.12f, ShopPageHud.CanvasPxToWorld(100f, 1f, 1f, 1080f, 6.06f), Epsilon);
	}

	[Test]
	public void A2_IconBlockAtShopScale()
	{
		// small shadow 222x306 px at PlayerIconShopScale 0.28 -> 0.697 x 0.962 u (plan §6 golden).
		Assert.AreEqual(222f * 0.28f / 1080f * 12.12f, ShopPageHud.CanvasPxToWorld(222f, 0.28f, 1f, 1080f, 6.06f), Epsilon);
		Assert.AreEqual(306f * 0.28f / 1080f * 12.12f, ShopPageHud.CanvasPxToWorld(306f, 0.28f, 1f, 1080f, 6.06f), Epsilon);
	}

	[Test]
	public void A3_CanvasScaleFactorStacksLinearly()
	{
		// The scale chain (localScale x scaleFactor) multiplies in before the px->world divide.
		Assert.AreEqual(50f * 0.5f * 1.7777f / 1080f * 12.12f,
			ShopPageHud.CanvasPxToWorld(50f, 0.5f, 1.7777f, 1080f, 6.06f), Epsilon);
	}

	[Test]
	public void A4_NonPositiveScreenHeightDegeneratesToZero()
	{
		Assert.AreEqual(0f, ShopPageHud.CanvasPxToWorld(100f, 1f, 1f, 0f, 6.06f), Epsilon);
		Assert.AreEqual(0f, ShopPageHud.CanvasPxToWorld(100f, 1f, 1f, -100f, 6.06f), Epsilon);
	}

	// Sliced-mirror goldens (VISUAL-FIX 2026-09-21): SlicedWorldSize x SlicedWorldScale must
	// reproduce the canvas target size, and the world border fraction must equal the canvas
	// border fraction (canvas border local px = border/spritePPU x refPPU; world border =
	// border/spritePPU x localScale).

	[Test]
	public void B1_SlicedSplitPreservesFinalWorldSize()
	{
		Vector2 size = ShopPageHud.SlicedWorldSize(new Vector2(192f, 276f), new Vector2(0.9f, 0.9f), 100f);
		float scale = ShopPageHud.SlicedWorldScale(100f, 0.005f);
		Assert.AreEqual(192f * 0.9f * 0.005f, size.x * scale, Epsilon);
		Assert.AreEqual(276f * 0.9f * 0.005f, size.y * scale, Epsilon);
	}

	[Test]
	public void B2_SlicedSplitPreservesBorderProportion()
	{
		// RoundedCorner: 64 px border at 256 PPU; canvas refPPU 100 -> canvas border 25 local px.
		// World border = 64/256 x localScale; over a 192 px x unit wide frame both must give 13%.
		float unit = 0.005f;
		float scale = ShopPageHud.SlicedWorldScale(100f, unit);
		float worldBorderFraction = 64f / 256f * scale / (192f * 1f * unit);
		float canvasBorderFraction = 64f / 256f * 100f / 192f;
		Assert.AreEqual(canvasBorderFraction, worldBorderFraction, Epsilon);
	}

	[Test]
	public void B3_InvalidRefPpuFallsBackTo100()
	{
		Assert.AreEqual(ShopPageHud.SlicedWorldSize(new Vector2(10f, 10f), Vector2.one, 100f),
			ShopPageHud.SlicedWorldSize(new Vector2(10f, 10f), Vector2.one, 0f));
		Assert.AreEqual(ShopPageHud.SlicedWorldScale(100f, 0.5f), ShopPageHud.SlicedWorldScale(0f, 0.5f), Epsilon);
	}
}
