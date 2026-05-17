using UnityEngine;

/// <summary>
/// Pure static helper for calculating card positions in the deck layout.
/// </summary>
public static class DeckPositionCalculator
{
	/// <summary>
	/// Calculate position coordinates at specified index.
	/// </summary>
	/// <param name="index">Card index in deck (0 = bottom)</param>
	/// <param name="deckCount">Total number of cards in deck</param>
	/// <param name="basePos">Base deck position (including any focus offset)</param>
	/// <param name="xOffset">Horizontal offset per card</param>
	/// <param name="yOffset">Vertical offset per card</param>
	/// <param name="zOffset">Depth offset per card</param>
	public static Vector3 CalculatePositionAtIndex(
		int index,
		int deckCount,
		Vector3 basePos,
		float xOffset,
		float yOffset,
		float zOffset)
	{
		// index=0 (bottom of physical deck) has largest offset, index=count-1 (top) has smallest offset
		Vector3 result = new Vector3(
			basePos.x + xOffset * (deckCount - 1 - index),
			basePos.y + yOffset * (deckCount - 1 - index),
			basePos.z - zOffset * index
		);
		return result;
	}
}
