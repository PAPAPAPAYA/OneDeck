using UnityEngine;

/// <summary>
/// Tail bend direction for the cascade deck curve.
/// Mirror = bend back toward the opposite side of the front sweep (demo behavior),
/// Same = keep the front sweep direction.
/// </summary>
public enum CascadeTailBend { Mirror, Same }

/// <summary>
/// Pure static helper that computes the Smooth Curve Cascade deck layout.
/// Ported 1:1 from docs/CardArrangementDemo.html (computeSmoothPositions / getCascadeParams).
/// No scene dependencies; fully unit-testable (see Assets/Scripts/Editor/Tests/DeckCascadeLayoutTests.cs).
///
/// Coordinate convention:
/// - The demo runs in canvas space (y-down). ComputeOffsets converts to Unity world space
///   by negating BOTH axes and scaling by pxToWorld, so the canonical Unity curve reads
///   front up-RIGHT / tail hooking left. The caller then mirrors per-component by
///   sign(cascadeDirection); the default direction (-1, +1) reproduces the demo shape
///   (front up-left, tail hooking right).
/// - Index convention: cascadeIndex 0 = front card (deck top, first revealed) at the anchor;
///   cascadeIndex deckCount-1 = deepest card.
/// </summary>
public static class DeckCascadeLayout
{
	[System.Serializable]
	public struct Params
	{
		public int shrinkCount;       // front segment length (demo: 6)
		public float minScale;        // smallest card scale (demo: 0.55)
		public float scalePower;      // scale falloff steepness (demo: 2)
		public float startSpacingX;   // front spacing X (demo: 60)
		public float startSpacingY;   // front spacing Y (demo: 70)
		public float minSpacingX;     // tail spacing X (demo: 8)
		public float minSpacingY;     // tail spacing Y (demo: 12)
		public float spacingPower;    // spacing falloff steepness (demo: 2)
		public float tailReturn;      // demo "curveWidth": 0 = straight peak, 1 = strong return (demo: 0.55)
		public float tailBendSign;    // +1 = Mirror (demo), -1 = Same (keep front direction)
		public int arcSamples;        // Bezier sampling density (demo: 300)
	}

	// Last-result cache so per-index callers do not recompute the whole curve per card.
	private static int _cacheDeckCount = -1;
	private static float _cachePxToWorld;
	private static Params _cacheParams;
	private static Vector2[] _cacheOffsets;

	/// <summary>
	/// Compute per-card offsets from the anchor, in Unity world units (BEFORE the
	/// cascadeDirection mirror; the caller applies the per-component sign).
	/// Returns an array of length deckCount indexed by cascadeIndex (0 = front card).
	/// </summary>
	public static Vector2[] ComputeOffsets(int deckCount, Params p, float pxToWorld)
	{
		if (deckCount <= 0) return new Vector2[0];
		if (deckCount == _cacheDeckCount && pxToWorld == _cachePxToWorld && p.Equals(_cacheParams))
			return _cacheOffsets;

		var offsets = ComputeOffsetsUncached(deckCount, p, pxToWorld);
		_cacheDeckCount = deckCount;
		_cachePxToWorld = pxToWorld;
		_cacheParams = p;
		_cacheOffsets = offsets;
		return offsets;
	}

	/// <summary>
	/// Per-card scale multiplier. cascadeIndex 0 (front) = 1, deepest card approaches minScale.
	/// </summary>
	public static float ComputeScale(int cascadeIndex, int deckCount, Params p)
	{
		if (deckCount <= 1) return 1f;
		float t = cascadeIndex / (float)Mathf.Max(1, deckCount - 1);
		return 1f - (1f - p.minScale) * EaseOutPower(t, p.scalePower);
	}

	private static Vector2[] ComputeOffsetsUncached(int deckCount, Params p, float pxToWorld)
	{
		var offsets = new Vector2[deckCount];
		offsets[0] = Vector2.zero;
		if (deckCount == 1) return offsets;

		// Clamp shrinkCount so the peak stays inside the curve for tiny decks.
		int shrinkCount = Mathf.Min(p.shrinkCount, deckCount - 1);

		// Quadratic Bezier control points (demo canvas space, y-down).
		// P0 = (0,0), P1 = (-peakX, -peakY), P2 = (tailX, tailY).
		float peakX = p.startSpacingX * shrinkCount * 0.85f;
		float peakY = p.startSpacingY * shrinkCount * 0.85f;
		float tailX = -peakX * (1f - p.tailReturn)
			+ p.tailBendSign * p.minSpacingX * (deckCount - shrinkCount) * p.tailReturn * 0.6f;
		float tailY = -peakY - p.minSpacingY * (deckCount - shrinkCount);

		// Dense sampling for arc-length parameterization.
		int sampleCount = Mathf.Max(2, p.arcSamples);
		var sampleX = new float[sampleCount + 1];
		var sampleY = new float[sampleCount + 1];
		var sampleLen = new float[sampleCount + 1];
		float prevX = 0f, prevY = 0f, totalLength = 0f;
		for (int i = 1; i <= sampleCount; i++)
		{
			float t = i / (float)sampleCount;
			float inv = 1f - t;
			// P0 = (0,0) so its terms drop out of the quadratic Bezier evaluation.
			float x = 2f * inv * t * (-peakX) + t * t * tailX;
			float y = 2f * inv * t * (-peakY) + t * t * tailY;
			float dx = x - prevX;
			float dy = y - prevY;
			totalLength += Mathf.Sqrt(dx * dx + dy * dy);
			sampleX[i] = x;
			sampleY[i] = y;
			sampleLen[i] = totalLength;
			prevX = x;
			prevY = y;
		}

		// Walk cards along the curve with decreasing spacing (demo loop 1:1).
		float currentLen = 0f;
		for (int i = 1; i < deckCount; i++)
		{
			float t = i / (float)Mathf.Max(1, deckCount - 1);
			float spacingX = Mathf.Lerp(p.startSpacingX, p.minSpacingX, EaseOutPower(t, p.spacingPower));
			float spacingY = Mathf.Lerp(p.startSpacingY, p.minSpacingY, EaseOutPower(t, p.spacingPower));
			float stepSize = Mathf.Sqrt(spacingX * spacingX + spacingY * spacingY) * 0.5f;
			currentLen += stepSize;

			// Find the sample pair bracketing targetLen (demo linear scan 1:1).
			float targetLen = Mathf.Min(currentLen, totalLength);
			int s = 1;
			while (s < sampleLen.Length && sampleLen[s] < targetLen) s++;
			if (s >= sampleLen.Length)
			{
				offsets[i] = new Vector2(tailX, tailY);
				continue;
			}
			float aLen = sampleLen[s - 1];
			float segLen = sampleLen[s] - aLen;
			float localT = segLen > 0f ? (targetLen - aLen) / segLen : 0f;
			offsets[i] = new Vector2(
				Mathf.Lerp(sampleX[s - 1], sampleX[s], localT),
				Mathf.Lerp(sampleY[s - 1], sampleY[s], localT));
		}

		// Demo canvas (y-down) -> Unity (y-up): negate BOTH axes so the canonical Unity
		// curve reads front up-RIGHT; cascadeDirection (-1, +1) then reproduces the demo.
		for (int i = 0; i < deckCount; i++)
			offsets[i] = new Vector2(-offsets[i].x * pxToWorld, -offsets[i].y * pxToWorld);
		return offsets;
	}

	private static float EaseOutPower(float t, float power)
	{
		return 1f - Mathf.Pow(1f - t, power);
	}
}
