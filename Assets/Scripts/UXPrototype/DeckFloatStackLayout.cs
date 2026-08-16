using UnityEngine;

/// <summary>
/// Pure static helper that computes the Float Stack Reveal deck layout.
/// Ported 1:1 from docs/demo/CardStackRevealDemo.html (computeFrame + roleTransform).
/// No scene dependencies; fully unit-testable (see Assets/Scripts/Editor/Tests/DeckFloatStackLayoutTests.cs).
/// Plans: plans/plan-float-stack-reveal-layout-2026-08-13.md,
///        plans/plan-float-stack-center-scale-2026-08-15.md (center/compress/bottom-limit revision).
///
/// Coordinate convention:
/// - The demo runs in canvas space (y-down) with the anchor as the origin.
///   The anchor is the STACK CENTER (screen center); the deck is vertically symmetric around it.
///   Slot 0 (the shadow's home) sits one step below the lowest stack slot and follows the
///   centered/pinned stack bottom.
/// - Unity conversion: unityOffset = (canvasDx, -canvasDy) * pxToWorld.
/// - Index convention: unity deck index j (0 = deck bottom) maps to stack slot (deckCount - j),
///   so the next-to-reveal card (j = deckCount-1) is the lowest stack card on screen.
/// </summary>
public static class DeckFloatStackLayout
{
	[System.Serializable]
	public struct Params
	{
		public float stepY;             // fixed y step per stack slot, demo px (signed; negative = downward stack) (demo: 14)
		public float revealScale;       // revealed card scale multiplier (demo: 1.07)
		public float revealFloatX;      // revealed card left offset from the shadow home (slot 0), demo px (demo: 16)
		public float revealUpY;         // revealed card up offset from the shadow home, demo px (signed; negative = below = lowest element) (demo: -14)
		public float maxHeightPx;       // stack height (stepY·deckCount) that triggers global compression; <= 0 = off (demo: 250)
		public float minScale;          // floor for the global compress scale (demo: 0.55)
		public float bottomLimitPx;     // max px any card face (deck bottom edge or revealed card bottom edge) may sink below the anchor; <= 0 = off (demo: 270)
		public float cardHalfHeightPx;  // card-face half height in demo px (demo: 105)
	}

	/// <summary>
	/// Per-deck-count layout frame. Every consumer (slots, reveal, shadow, arc midpoint,
	/// card scale, jitter) derives from this single computation.
	/// </summary>
	public struct Frame
	{
		public float globalScale;      // compress-only scale, clamped to [minScale, 1]; 1 when disabled
		public float effectiveStepY;   // stepY * globalScale, px (step is bound to the scale)
		public float liftPx;           // >= 0 upward shift (Unity y-up) pinning the lowest card-face point
	}

	/// <summary>
	/// Layout frame for a deck count (deck count excludes the revealed card).
	/// - globalScale: compress-only (clamped to [minScale, 1]); small decks keep the natural step.
	/// - effectiveStepY: bound to the scale so compression actually shortens the stack.
	/// - liftPx: pins the LOWEST CARD-FACE POINT (max of the deck's visual bottom edge and
	///   the revealed card's bottom edge; the soft shadow may bleed) at bottomLimitPx once it
	///   would breach it; afterwards the deck only grows upward. Continuous in deck count:
	///   when the deck shrinks back, lift returns to 0 and the stack re-centers automatically.
	/// stepY &lt;= 0 (downward stack) skips scale/limit and runs pure centering.
	/// </summary>
	public static Frame ComputeFrame(int deckCount, Params p)
	{
		Frame f = new Frame { globalScale = 1f, effectiveStepY = p.stepY, liftPx = 0f };
		if (p.maxHeightPx > 0f && p.stepY > 0f && deckCount > 0)
			f.globalScale = Mathf.Clamp(p.maxHeightPx / (p.stepY * deckCount), p.minScale, 1f);
		f.effectiveStepY = p.stepY * f.globalScale;
		if (p.bottomLimitPx > 0f && f.effectiveStepY > 0f && deckCount > 0)
		{
			float slot0 = f.effectiveStepY * (deckCount + 1) * 0.5f; // shadow home, px below the anchor
			float deckBottom = f.effectiveStepY * (deckCount - 1) * 0.5f + p.cardHalfHeightPx * f.globalScale;
			float revealBottom = slot0 - p.revealUpY + p.cardHalfHeightPx * f.globalScale * p.revealScale;
			float drop = Mathf.Max(deckBottom, revealBottom); // lowest card-face point below the anchor
			if (drop > p.bottomLimitPx) f.liftPx = drop - p.bottomLimitPx;
		}
		return f;
	}

	/// <summary>
	/// Anchor-relative offset (Unity units, y-up) for a unity deck index in a deckCount-card stack.
	/// Centered: y = step·(count − 2j − 1)/2 + lift — symmetric around the anchor; count 1 = anchor.
	/// </summary>
	public static Vector2 ComputeSlotOffset(int unityIndex, int deckCount, Params p, float pxToWorld)
	{
		if (deckCount <= 0) return Vector2.zero;
		int clamped = Mathf.Clamp(unityIndex, 0, deckCount - 1);
		Frame f = ComputeFrame(deckCount, p);
		float yPx = f.effectiveStepY * (deckCount - 2 * clamped - 1) * 0.5f + f.liftPx;
		return new Vector2(0f, yPx * pxToWorld);
	}

	/// <summary>
	/// Anchor-relative float pose of the revealed card (Unity units, y-up).
	/// Derives from the shadow home (slot 0): offset = slot0 + (−floatX, +upY); with a
	/// negative upY the revealed card sinks below the stack base as the lowest element.
	/// </summary>
	public static Vector2 ComputeRevealOffset(int deckCount, Params p, float pxToWorld)
	{
		Frame f = ComputeFrame(deckCount, p);
		float yPx = p.revealUpY - f.effectiveStepY * (deckCount + 1) * 0.5f + f.liftPx;
		return new Vector2(-p.revealFloatX * pxToWorld, yPx * pxToWorld);
	}

	/// <summary>
	/// Shadow home y (Unity y-up, demo px, BEFORE pxToWorld and BEFORE the shadowOffset tweak):
	/// slot 0 = one step below the lowest stack slot, lifted with the stack.
	/// </summary>
	public static float ComputeShadowHomeYPx(int deckCount, Params p)
	{
		Frame f = ComputeFrame(deckCount, p);
		return f.liftPx - f.effectiveStepY * (deckCount + 1) * 0.5f;
	}

	/// <summary>
	/// Dynamic arc midpoint y (Unity y-up, demo px, BEFORE pxToWorld): one step beyond the
	/// stack's back/top slot, lifted with the stack.
	/// </summary>
	public static float ComputeArcMidYPx(int deckCount, Params p)
	{
		Frame f = ComputeFrame(deckCount, p);
		return f.liftPx + f.effectiveStepY * (deckCount + 1) * 0.5f;
	}
}
