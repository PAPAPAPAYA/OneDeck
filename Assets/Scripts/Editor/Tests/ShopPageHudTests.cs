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
}
