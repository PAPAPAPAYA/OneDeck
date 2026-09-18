using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the pure helpers of ShopSectionPanels (shop page panels port,
/// plans/plan-shop-panels-port-2026-09-18.md Task 4): content-bounds math and the
/// zero-padded deck slot counter format.
/// </summary>
public class ShopSectionPanelsTests
{
	[Test]
	public void ComputeContentBounds_SingleCenter_ReturnsCenteredBounds()
	{
		var centers = new System.Collections.Generic.List<Vector3> { new Vector3(1f, 2f, 0f) };
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual(new Vector3(1f, 2f, 0f), b.center);
		Assert.AreEqual(2.8f, b.size.x, 0.001f);
		Assert.AreEqual(4.5f, b.size.y, 0.001f);
	}

	[Test]
	public void ComputeContentBounds_MultiRow_UsesExtremes()
	{
		var centers = new System.Collections.Generic.List<Vector3>
		{
			new Vector3(-3.2f, 0f, 0f), new Vector3(3.2f, 0f, 0f), new Vector3(0f, -4.5f, 0f)
		};
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual(-3.2f - 1.4f, b.min.x, 0.001f);
		Assert.AreEqual(3.2f + 1.4f, b.max.x, 0.001f);
		Assert.AreEqual(0f + 2.0f, b.max.y, 0.001f);
		Assert.AreEqual(-4.5f - 2.5f, b.min.y, 0.001f);
	}

	[Test]
	public void ComputeContentBounds_EmptyList_ReturnsZeroBounds()
	{
		var centers = new System.Collections.Generic.List<Vector3>();
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual(Vector3.zero, b.center);
		Assert.AreEqual(Vector3.zero, b.size);
	}

	[Test]
	public void FormatSlotCount_PadsToTwoDigits()
	{
		Assert.AreEqual("03/05", ShopSectionPanels.FormatSlotCount(3, 5));
		Assert.AreEqual("12/12", ShopSectionPanels.FormatSlotCount(12, 12));
	}
}
