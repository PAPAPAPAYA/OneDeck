using UnityEngine;

/// <summary>
/// Pure static helper that computes the Float Stack Reveal deck layout.
/// Ported 1:1 from docs/demo/CardStackRevealDemo.html (roleTransform).
/// No scene dependencies; fully unit-testable (see Assets/Scripts/Editor/Tests/DeckFloatStackLayoutTests.cs).
/// Plan: plans/plan-float-stack-reveal-layout-2026-08-13.md.
///
/// Coordinate convention:
/// - The demo runs in canvas space (y-down) with the shadow anchor as the origin.
///   Slot 0 is the anchor itself (the shadow's home) and is never occupied by a deck card;
///   stack slot i (1 = next to reveal) sits stepY·i above the anchor.
/// - Unity conversion: unityOffset = (canvasDx, -canvasDy) * pxToWorld.
/// - Index convention: unity deck index j (0 = deck bottom) maps to stack slot (deckCount - j),
///   so the next-to-reveal card (j = deckCount-1) is one step above the anchor.
/// </summary>
public static class DeckFloatStackLayout
{
	[System.Serializable]
	public struct Params
	{
		public float stepY;         // fixed y step per stack slot, demo px (signed; negative = downward stack) (demo: 14)
		public float revealScale;   // revealed card scale multiplier (demo: 1.07)
		public float revealFloatX;  // revealed card left offset from the shadow anchor, demo px (demo: 16)
		public float revealUpY;     // revealed card up offset from the shadow anchor, demo px (signed) (demo: -14)
	}

	/// <summary>
	/// Anchor-relative offset (Unity units) for a unity deck index in a deckCount-card stack.
	/// </summary>
	public static Vector2 ComputeSlotOffset(int unityIndex, int deckCount, Params p, float pxToWorld)
	{
		if (deckCount <= 0) return Vector2.zero;
		int clamped = Mathf.Clamp(unityIndex, 0, deckCount - 1);
		// Slot (deckCount - clamped): demo canvas (0, -stepY*slot) -> Unity (0, +stepY*slot).
		return new Vector2(0f, p.stepY * (deckCount - clamped) * pxToWorld);
	}

	/// <summary>
	/// Anchor-relative float pose of the revealed card (Unity units).
	/// Demo canvas (-floatX, -upY) -> Unity (-floatX, +upY): a negative upY stays negative.
	/// </summary>
	public static Vector2 ComputeRevealOffset(Params p, float pxToWorld)
	{
		return new Vector2(-p.revealFloatX * pxToWorld, p.revealUpY * pxToWorld);
	}
}
