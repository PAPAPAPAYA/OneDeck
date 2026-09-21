using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode goldens for PhaseFlightPlanner (plan-phase-transition-world-camera-2026-09-21).
/// Numbers are pinned 1:1 against docs/demo/PhaseTransitionDemo.html: PAGE_H = 740 px (:536),
/// transDur 800 ms, cardStagger 70 ms, cardArc 90 px, enemySlide 140 px (:514-521). The Unity
/// page height is 2*orthoSize = 12.12 world units for the shipping camera (ortho 6.06).
/// </summary>
public class PhaseFlightPlannerTests
{
	private const float Ortho = 6.06f;
	private const float PageH = 12.12f; // 2 * Ortho, shipping camera
	private const float Tol = 1e-4f;

	[Test]
	public void PageHeightWorld_IsTwiceOrthographicSize()
	{
		Assert.AreEqual(12.12f, PhaseFlightPlanner.PageHeightWorld(Ortho), Tol);
	}

	[Test]
	public void PxToWorld_MapsDemoPageToScreenHeight()
	{
		// The full demo page (740 px) maps exactly onto one screen height.
		Assert.AreEqual(PageH, PhaseFlightPlanner.PxToWorld(740f, PageH), Tol);
		// cardArc 90 px (demo def) -> 1.4741 world units.
		Assert.AreEqual(1.4741f, PhaseFlightPlanner.PxToWorld(90f, PageH), 1e-3f);
		// enemySlide 140 px -> 2.2930 world units.
		Assert.AreEqual(2.2930f, PhaseFlightPlanner.PxToWorld(140f, PageH), 1e-3f);
		// PAD 200 px -> 3.2757 world units.
		Assert.AreEqual(3.2757f, PhaseFlightPlanner.PxToWorld(200f, PageH), 1e-3f);
	}

	[Test]
	public void DemoPxToCanvasPx_KeepsPageFraction()
	{
		// 140 px on the 740 px page over a 1080-ref canvas = 204.32 canvas px.
		Assert.AreEqual(140f / 740f * 1080f, PhaseFlightPlanner.DemoPxToCanvasPx(140f, 1080f), Tol);
		Assert.AreEqual(1080f, PhaseFlightPlanner.DemoPxToCanvasPx(740f, 1080f), Tol);
	}

	[Test]
	public void ArcApex_LiftsMidpointByArc()
	{
		var from = new Vector3(0f, 0f, 0f);
		var to = new Vector3(2f, 4f, -1f);
		var apex = PhaseFlightPlanner.ArcApex(from, to, 1.5f);
		Assert.AreEqual(1f, apex.x, Tol, "apex x = midpoint x");
		Assert.AreEqual(3.5f, apex.y, Tol, "apex y = midpoint y + arc");
		Assert.AreEqual(-0.5f, apex.z, Tol, "apex z = midpoint z");
	}

	[Test]
	public void FlightDelay_IsStaggerTimesIndex()
	{
		Assert.AreEqual(0f, PhaseFlightPlanner.FlightDelay(0, 0.07f), Tol);
		Assert.AreEqual(0.07f, PhaseFlightPlanner.FlightDelay(1, 0.07f), Tol);
		Assert.AreEqual(0.14f, PhaseFlightPlanner.FlightDelay(2, 0.07f), 1e-4f);
	}

	[Test]
	public void FlipTime_IsMidFlight()
	{
		// Demo flipAtMid (:692): delay + transDur/2. Card 1: 0.07 + 0.4 = 0.47 s.
		Assert.AreEqual(0.47f, PhaseFlightPlanner.FlipTime(0.07f, 0.8f), 1e-4f);
		Assert.AreEqual(0.4f, PhaseFlightPlanner.FlipTime(0f, 0.8f), 1e-4f);
	}

	[Test]
	public void TotalDuration_AddsStaggerTail()
	{
		// Demo wait (:739): transDur + 2*cardStagger = 0.8 + 0.14 = 0.94 s.
		Assert.AreEqual(0.94f, PhaseFlightPlanner.TotalDuration(0.8f, 0.07f), 1e-4f);
	}
}
