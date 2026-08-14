using UnityEngine;

/// <summary>
/// Pure static helper that computes the Elliptical Arc Loop deck layout.
/// Ported 1:1 from docs/demo/CardArcArrangementDemo.html (loopBasePoint / tiltPoint /
/// buildArcTable / loopBottomDensityCoord / pointAtDensity / applyLayout slot loop).
/// No scene dependencies; fully unit-testable (see Assets/Scripts/Editor/Tests/DeckArcLoopLayoutTests.cs).
/// Plan: plans/plan-arc-loop-deck-layout-2026-08-12.md.
///
/// Coordinate convention:
/// - The demo runs in canvas space (y-down); all table/slot math stays in that space 1:1.
///   Only the final per-slot offset is converted: unityOffset = (canvasDx, -canvasDy) * pxToWorld,
///   relative to the deck-top slot, so the deck top sits at the anchor and the rest of the
///   ring rises above it.
/// - Index convention: Slot array is indexed by UNITY deck index (0 = deck bottom,
///   deckCount-1 = deck top). Deck top is pinned at the tilted loop's visual lowest point;
///   deck bottom is one weighted arc step up the right side.
/// </summary>
public static class DeckArcLoopLayout
{
	[System.Serializable]
	public struct Params
	{
		public float radiusX;       // loop horizontal radius in demo px (demo: 195)
		public float radiusY;       // loop vertical radius in demo px (demo: 155)
		public float exponent;      // superellipse exponent (demo: 2 = true ellipse, higher = squarer)
		public float tiltDeg;       // rigid in-plane tilt; positive = left half lower, right half higher (demo: 45)
		public float curveDensity;  // curvature weight w (demo: 3; 0 = uniform arc length)
		public float minScale;      // scale of the highest/back-most card (demo: 0.70)
		public float scalePower;    // height-scale falloff steepness (demo: 1)
		public bool mirror;         // true = deck bottom slot left of the deck top (mirrored loop)
		public int arcSamples;      // arc-length/curvature table resolution (demo: 720)
	}

	/// <summary>
	/// One deck slot's layout result, indexed by unity deck index (0 = deck bottom).
	/// </summary>
	public sealed class Slot
	{
		public Vector2 offset;   // Unity units, relative to the deck-top slot (deck top = Vector2.zero)
		public float scale;      // height-normalized depth scale (deck top = 1, highest card = minScale)
		public int depthRank;    // 0 = back-most (highest on screen), deckCount-1 = front-most (deck top)
	}

	// Loop table cache: the untilted loop's arc-length/curvature table. Count- and
	// pxToWorld-independent (tilt preserves arc length; density ratios are scale-invariant).
	private sealed class LoopTable
	{
		public Vector2[] pts;        // arcSamples+1 untilted base points (canvas y-down)
		public float[] cumDensity;   // arcSamples+1 cumulative weighted arc length
		public float totalDensity;
	}
	private static bool _tableValid;
	private static Params _tableParams;
	private static LoopTable _table;

	// Last-result slot cache so per-index callers do not recompute the whole layout per card.
	private static int _cacheDeckCount = -1;
	private static float _cachePxToWorld;
	private static Params _cacheParams;
	private static Slot[] _cacheSlots;

	/// <summary>
	/// Per-slot layout for the whole deck. Returns an array of length deckCount indexed by
	/// unity deck index (0 = deck bottom, deckCount-1 = deck top).
	/// </summary>
	public static Slot[] ComputeSlots(int deckCount, Params p, float pxToWorld)
	{
		if (deckCount <= 0) return new Slot[0];
		if (deckCount == _cacheDeckCount && pxToWorld == _cachePxToWorld && p.Equals(_cacheParams))
			return _cacheSlots;

		var slots = ComputeSlotsUncached(deckCount, p, pxToWorld);
		_cacheDeckCount = deckCount;
		_cachePxToWorld = pxToWorld;
		_cacheParams = p;
		_cacheSlots = slots;
		return slots;
	}

	/// <summary>
	/// Per-slot scale multiplier for a unity deck index. Deck top (deckCount-1) = 1,
	/// the highest/back-most slot approaches minScale.
	/// </summary>
	public static float ComputeScale(int unityIndex, int deckCount, Params p, float pxToWorld)
	{
		if (deckCount <= 1) return 1f;
		var slots = ComputeSlots(deckCount, p, pxToWorld);
		int clamped = Mathf.Clamp(unityIndex, 0, deckCount - 1);
		return slots[clamped].scale;
	}

	/// <summary>
	/// Offset at a fractional position along the loop walk, for the dynamic arc-midpoint seam.
	/// t in [0,1]: 0 = deck top (front), 1 = deck bottom. Interpolates between the two
	/// bracketing slot offsets, mirroring DeckCascadeLayout.ComputeOffsetAtCurveT's contract.
	/// </summary>
	public static Vector2 ComputeOffsetAtCurveT(int deckCount, float t, Params p, float pxToWorld)
	{
		var slots = ComputeSlots(deckCount, p, pxToWorld);
		if (slots.Length == 0) return Vector2.zero;
		float ci = (1f - Mathf.Clamp01(t)) * (slots.Length - 1);
		int lo = Mathf.FloorToInt(ci);
		int hi = Mathf.CeilToInt(ci);
		if (lo == hi) return slots[lo].offset;
		return Vector2.Lerp(slots[lo].offset, slots[hi].offset, ci - lo);
	}

	private static Slot[] ComputeSlotsUncached(int deckCount, Params p, float pxToWorld)
	{
		LoopTable table = EnsureTable(p);
		float phi = p.tiltDeg * Mathf.Deg2Rad;
		float cos = Mathf.Cos(phi);
		float sin = Mathf.Sin(phi);

		// Deck-top anchor: the tilted loop's visual lowest point (max canvas y).
		int best = 0;
		float bestY = float.NegativeInfinity;
		int m = table.pts.Length - 1;
		for (int i = 0; i < m; i++)
		{
			float y = TiltY(table.pts[i], cos, sin);
			if (y > bestY)
			{
				bestY = y;
				best = i;
			}
		}
		float bottomD = table.cumDensity[best];

		// Slot canvas positions along the weighted arc walk.
		var xs = new float[deckCount];
		var ys = new float[deckCount];
		for (int k = 0; k < deckCount; k++)
		{
			float d = bottomD + (k + 1f) / deckCount * table.totalDensity;
			Vector2 pos = PointAtDensity(table, d, cos, sin);
			xs[k] = pos.x;
			ys[k] = pos.y;
		}

		// Height-normalized depth scale: lowest slot (front, deck top) = 1, highest = minScale.
		float yMin = float.MaxValue, yMax = float.MinValue;
		for (int k = 0; k < deckCount; k++)
		{
			if (ys[k] < yMin) yMin = ys[k];
			if (ys[k] > yMax) yMax = ys[k];
		}
		var slots = new Slot[deckCount];
		for (int k = 0; k < deckCount; k++)
		{
			float t = yMax > yMin ? (yMax - ys[k]) / (yMax - yMin) : 0f;
			float falloff = 1f - Mathf.Pow(1f - t, p.scalePower);
			slots[k] = new Slot { scale = 1f - (1f - p.minScale) * falloff };
		}

		// Depth rank: canvas y ascending (back-most first); ties resolve to the lower index.
		var order = new int[deckCount];
		for (int k = 0; k < deckCount; k++) order[k] = k;
		System.Array.Sort(order, (a, b) => ys[a] != ys[b] ? ys[a].CompareTo(ys[b]) : a.CompareTo(b));
		for (int rank = 0; rank < deckCount; rank++)
			slots[order[rank]].depthRank = rank;

		// Offsets relative to the deck-top slot; canvas (y-down) -> Unity (y-up): negate y.
		float topX = xs[deckCount - 1];
		float topY = ys[deckCount - 1];
		for (int k = 0; k < deckCount; k++)
		{
			slots[k].offset = new Vector2(
				(xs[k] - topX) * pxToWorld,
				-(ys[k] - topY) * pxToWorld);
		}
		return slots;
	}

	private static LoopTable EnsureTable(Params p)
	{
		if (_tableValid && p.Equals(_tableParams)) return _table;
		_table = BuildTable(p);
		_tableParams = p;
		_tableValid = true;
		return _table;
	}

	private static LoopTable BuildTable(Params p)
	{
		int m = Mathf.Max(8, p.arcSamples);
		var pts = new Vector2[m + 1];
		for (int i = 0; i <= m; i++)
			pts[i] = LoopBasePoint(p, i / (float)m * Mathf.PI * 2f);

		var segLen = new float[m];
		float totalLen = 0f;
		for (int i = 0; i < m; i++)
		{
			float dx = pts[i + 1].x - pts[i].x;
			float dy = pts[i + 1].y - pts[i].y;
			float len = Mathf.Sqrt(dx * dx + dy * dy);
			segLen[i] = len;
			totalLen += len;
		}

		// Menger curvature through each sample (wraparound neighbors).
		var kappa = new float[m];
		for (int i = 0; i < m; i++)
		{
			Vector2 a = pts[(i - 1 + m) % m];
			Vector2 b = pts[i];
			Vector2 c = pts[(i + 1) % m];
			float abx = b.x - a.x, aby = b.y - a.y;
			float bcx = c.x - b.x, bcy = c.y - b.y;
			float cax = c.x - a.x, cay = c.y - a.y;
			float cross = abx * bcy - aby * bcx;
			float denom = Mathf.Sqrt((abx * abx + aby * aby) * (bcx * bcx + bcy * bcy) * (cax * cax + cay * cay));
			kappa[i] = denom > 0f ? 2f * Mathf.Abs(cross) / denom : 0f;
		}

		float kappaSum = 0f;
		for (int i = 0; i < m; i++) kappaSum += kappa[i] * segLen[i];
		float kappaMean = totalLen > 0f ? kappaSum / totalLen : 0f;

		// Density rho = 1 + w * kappa / kappaMean, trapezoid-integrated over segments.
		var cumDensity = new float[m + 1];
		cumDensity[0] = 0f;
		float totalDensity = 0f;
		for (int i = 0; i < m; i++)
		{
			float rhoA = kappaMean > 0f ? 1f + p.curveDensity * kappa[i] / kappaMean : 1f;
			float rhoB = kappaMean > 0f ? 1f + p.curveDensity * kappa[(i + 1) % m] / kappaMean : 1f;
			totalDensity += (rhoA + rhoB) * 0.5f * segLen[i];
			cumDensity[i + 1] = totalDensity;
		}

		return new LoopTable { pts = pts, cumDensity = cumDensity, totalDensity = totalDensity };
	}

	// Untilted superellipse point in demo canvas space (y-down). alpha = 0 is the loop
	// bottom; positive alpha climbs the right side first.
	private static Vector2 LoopBasePoint(Params p, float alpha)
	{
		float shape = 2f / p.exponent;
		float side = p.mirror ? -1f : 1f;
		return new Vector2(
			side * p.radiusX * SPow(Mathf.Sin(alpha), shape),
			p.radiusY * SPow(Mathf.Cos(alpha), shape));
	}

	// Rigid in-plane tilt; positive phi = left half lower, right half higher (canvas y-down).
	private static Vector2 Tilt(Vector2 p, float cos, float sin)
	{
		return new Vector2(p.x * cos + p.y * sin, -p.x * sin + p.y * cos);
	}

	private static float TiltY(Vector2 p, float cos, float sin)
	{
		return -p.x * sin + p.y * cos;
	}

	// Tilted loop point at weighted arc coordinate d (wraps around the loop).
	private static Vector2 PointAtDensity(LoopTable t, float d, float cos, float sin)
	{
		int m = t.pts.Length - 1;
		float dd = d % t.totalDensity;
		if (dd < 0f) dd += t.totalDensity;
		// First segment whose cumulative end exceeds dd.
		int lo = 0, hi = m;
		while (lo < hi)
		{
			int mid = (lo + hi) >> 1;
			if (t.cumDensity[mid + 1] <= dd) lo = mid + 1; else hi = mid;
		}
		float segStart = t.cumDensity[lo];
		float segEnd = t.cumDensity[lo + 1];
		float f = segEnd > segStart ? (dd - segStart) / (segEnd - segStart) : 0f;
		Vector2 a = t.pts[lo];
		Vector2 b = t.pts[lo + 1];
		return Tilt(new Vector2(a.x + (b.x - a.x) * f, a.y + (b.y - a.y) * f), cos, sin);
	}

	// Signed power helper for the superellipse shaping. Matches the demo's spow:
	// Pow(0, positive) = 0, so Mathf.Sign(0) = 1 does not diverge from Math.sign.
	private static float SPow(float v, float power)
	{
		return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), power);
	}
}
