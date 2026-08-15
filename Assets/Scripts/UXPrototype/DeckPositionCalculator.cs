using UnityEngine;

/// <summary>
/// Combat deck layout modes selectable on CombatUXManager.deckLayoutMode.
/// deckLayoutMode is the single source of truth for the active combat deck layout.
/// </summary>
public enum DeckLayoutMode { Linear, Cascade, ArcLoop, FloatStack }

/// <summary>
/// Pure static helper for calculating card positions in the deck layout.
/// </summary>
public static class DeckPositionCalculator
{
	/// <summary>
	/// Cascade layout configuration carrier, built by CombatUXManager from its serialized fields.
	/// When null or disabled, CalculatePositionAtIndex runs the legacy linear formula byte-for-byte.
	/// </summary>
	public sealed class CascadeConfig
	{
		public bool enabled;
		public DeckCascadeLayout.Params layoutParams;
		public float pxToWorld;
		public Vector2 direction = new Vector2(-1f, 1f); // consumed as per-component sign only
	}

	/// <summary>
	/// Arc Loop layout configuration carrier, built by CombatUXManager from its serialized fields.
	/// When null or disabled the arc branch is skipped (cascade/legacy paths unchanged).
	/// </summary>
	public sealed class ArcLoopConfig
	{
		public bool enabled;
		public DeckArcLoopLayout.Params layoutParams;
		public float pxToWorld;
	}

	/// <summary>
	/// Float Stack layout configuration carrier, built by CombatUXManager from its serialized fields.
	/// When null or disabled the float-stack branch is skipped (arc/cascade/legacy paths unchanged).
	/// </summary>
	public sealed class FloatStackConfig
	{
		public bool enabled;
		public DeckFloatStackLayout.Params layoutParams;
		public float pxToWorld;
	}

	/// <summary>
	/// Calculate position coordinates at specified index.
	/// </summary>
	/// <param name="index">Card index in deck (0 = bottom)</param>
	/// <param name="deckCount">Total number of cards in deck</param>
	/// <param name="basePos">Base deck position (including any focus offset)</param>
	/// <param name="xOffset">Horizontal offset per card (legacy linear path)</param>
	/// <param name="yOffset">Vertical offset per card (legacy linear path)</param>
	/// <param name="zOffset">Depth offset per card</param>
	/// <param name="cascade">Cascade layout config; null or disabled = legacy linear fan</param>
	/// <param name="arcLoop">Arc Loop layout config; null or disabled = cascade/legacy paths</param>
	/// <param name="floatStack">Float Stack layout config; null or disabled = arc/cascade/legacy paths</param>
	public static Vector3 CalculatePositionAtIndex(
		int index,
		int deckCount,
		Vector3 basePos,
		float xOffset,
		float yOffset,
		float zOffset,
		CascadeConfig cascade = null,
		ArcLoopConfig arcLoop = null,
		FloatStackConfig floatStack = null)
	{
		// Float Stack branch: direct stack offsets from DeckFloatStackLayout; z keeps the
		// legacy formula (next-to-reveal card = highest index = front-most of the stack).
		if (floatStack != null && floatStack.enabled && deckCount > 0)
		{
			int clampedStackIndex = Mathf.Clamp(index, 0, deckCount - 1);
			Vector2 stackOffset = DeckFloatStackLayout.ComputeSlotOffset(
				clampedStackIndex, deckCount, floatStack.layoutParams, floatStack.pxToWorld);
			return new Vector3(
				basePos.x + stackOffset.x,
				basePos.y + stackOffset.y,
				basePos.z - zOffset * clampedStackIndex);
		}

		// Arc Loop branch: slot offsets/depth from DeckArcLoopLayout. The deck top (index
		// deckCount-1) sits at the anchor; z uses the visual depth rank (height-based),
		// which keeps the analytic front-z in GetRevealZonePosition exact.
		if (arcLoop != null && arcLoop.enabled && deckCount > 0)
		{
			int clampedArcIndex = Mathf.Clamp(index, 0, deckCount - 1);
			DeckArcLoopLayout.Slot slot = DeckArcLoopLayout.ComputeSlots(
				deckCount, arcLoop.layoutParams, arcLoop.pxToWorld)[clampedArcIndex];
			return new Vector3(
				basePos.x + slot.offset.x,
				basePos.y + slot.offset.y,
				basePos.z - zOffset * slot.depthRank);
		}

		// VISUAL-FIX(2026-07-17): Cascade deck layout branch at the single layout seam.
		//   Cause:    Linear-fan layout replaced by the Smooth Curve Cascade Stack (PRD 2026-07-17);
		//             the branch lives here so every caller (wrappers, peel focus, reveal-to-bottom)
		//             inherits the curve. The legacy formula below is preserved byte-for-byte.
		//   Affects:  All CombatUXManager deck position callers (layout, popup peaks, slot-in, peel focus).
		//   Regress:  Set deckLayoutMode = Linear; the deck must render as the old linear fan.
		if (cascade != null && cascade.enabled && deckCount > 0)
		{
			Vector2[] offsets = DeckCascadeLayout.ComputeOffsets(deckCount, cascade.layoutParams, cascade.pxToWorld);
			int clampedIndex = Mathf.Clamp(index, 0, deckCount - 1);
			// cascadeIndex 0 = front card = unity deck top (index deckCount-1)
			Vector2 offset = offsets[deckCount - 1 - clampedIndex];
			float signX = cascade.direction.x >= 0f ? 1f : -1f;
			float signY = cascade.direction.y >= 0f ? 1f : -1f;
			return new Vector3(
				basePos.x + offset.x * signX,
				basePos.y + offset.y * signY,
				basePos.z - zOffset * clampedIndex);
		}

		// Legacy linear fan: index=0 (bottom of physical deck) has largest offset, index=count-1 (top) has smallest offset
		return new Vector3(
			basePos.x + xOffset * (deckCount - 1 - index),
			basePos.y + yOffset * (deckCount - 1 - index),
			basePos.z - zOffset * index
		);
	}
}
