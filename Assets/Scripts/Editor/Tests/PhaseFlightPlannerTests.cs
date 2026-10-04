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
		// Since the 2026-10-04 animated flip this value is the PINCH moment (FlipRoot
		// scaleX = 0, the back first appears), not an instant swap.
		Assert.AreEqual(0.47f, PhaseFlightPlanner.FlipTime(0.07f, 0.8f), 1e-4f);
		Assert.AreEqual(0.4f, PhaseFlightPlanner.FlipTime(0f, 0.8f), 1e-4f);
	}

	[Test]
	public void FlipStart_CentersFlipOnApex()
	{
		// 2026-10-04 animated flip: start = pinch point (FlipTime) - half the flip, so the
		// scaleX pinch stays at the demo cover point and the back opens during the descent.
		// flipDuration 0.3 s is the CardPhysObjScript shipping default.
		Assert.AreEqual(0.25f, PhaseFlightPlanner.FlipStart(0f, 0.8f, 0.3f), 1e-4f);
		Assert.AreEqual(0.32f, PhaseFlightPlanner.FlipStart(0.07f, 0.8f, 0.3f), 1e-4f);
	}

	[Test]
	public void FlipStart_ClampsToFinishBeforeLanding()
	{
		// Short flight: the start is pulled earlier so the back is fully open by landing
		// (start 0.05 + flip 0.3 = 0.35 < landing 0.4).
		Assert.AreEqual(0.05f, PhaseFlightPlanner.FlipStart(0f, 0.4f, 0.3f), 1e-4f);
		// Flight exactly one flip long: the flip ends exactly at landing.
		Assert.AreEqual(0f, PhaseFlightPlanner.FlipStart(0f, 0.3f, 0.3f), 1e-4f);
		// Degenerate (flight shorter than the flip): never start before the card's own
		// launch — the flip runs into the landing swap, which lands later anyway.
		Assert.AreEqual(0f, PhaseFlightPlanner.FlipStart(0f, 0.2f, 0.3f), 1e-4f);
	}

	[Test]
	public void TotalDuration_AddsStaggerTail()
	{
		// Demo wait (:739): transDur + 2*cardStagger = 0.8 + 0.14 = 0.94 s.
		Assert.AreEqual(0.94f, PhaseFlightPlanner.TotalDuration(0.8f, 0.07f), 1e-4f);
	}

	[Test]
	public void TotalDuration_CountAware_CoversEveryStaggerStep()
	{
		// 2026-10-02 audit F3: dummy i lands at i*stagger + duration, so the count-aware
		// wait covers (count - 1) stagger steps. count = 3 is byte-identical to the demo
		// golden above; 1/4/8 pin the shipped-config values the driver now waits out.
		Assert.AreEqual(0.94f, PhaseFlightPlanner.TotalDuration(0.8f, 0.07f, 3), 1e-4f);
		Assert.AreEqual(0.80f, PhaseFlightPlanner.TotalDuration(0.8f, 0.07f, 1), 1e-4f);
		Assert.AreEqual(1.01f, PhaseFlightPlanner.TotalDuration(0.8f, 0.07f, 4), 1e-4f);
		Assert.AreEqual(1.29f, PhaseFlightPlanner.TotalDuration(0.8f, 0.07f, 8), 1e-4f);
	}

	[Test]
	public void HudHomeAtPage_ShiftsByPlaneDistanceToPage()
	{
		// 2026-10-02 re-audit fixes 2/3/7: the canvas plane rides the camera, so a HUD
		// element's world home at a page's camera height = current world pos + (pageY - planeY),
		// where planeY is the carrying canvas root's own Y (NOT the rig — VISUAL-FIX(2026-10-03):
		// the canvas lags the rig within the travel-start frame). Combat page one pageH above
		// the synced plane: shift is exactly +12.12.
		var atCombat = PhaseFlightPlanner.HudHomeAtPage(new Vector3(1f, 2f, -3f), 12.12f, 0f);
		Assert.AreEqual(1f, atCombat.x, Tol, "x is never written (Y-only travel)");
		Assert.AreEqual(14.12f, atCombat.y, Tol);
		Assert.AreEqual(-3f, atCombat.z, Tol, "z is never written");
		// Back down: combat anchor world -> shop page home (plane 12.12 above shop origin).
		var atShop = PhaseFlightPlanner.HudHomeAtPage(new Vector3(0f, 24.24f, 0f), 0f, 12.12f);
		Assert.AreEqual(12.12f, atShop.y, Tol);
		// Plane already at the page: identity.
		var identity = PhaseFlightPlanner.HudHomeAtPage(new Vector3(5f, 6f, 7f), 12.12f, 12.12f);
		Assert.AreEqual(6f, identity.y, Tol);
	}
}
