using UnityEngine;

/// <summary>
/// Shared top-bar placement for the Shop phase (2026-09-19 layout: UIKitDemo §08 order).
/// One constant source (viewport space, (0,1) = top-left) consumed by both render
/// systems that draw the shop top bar:
///   - the world-space chips of ShopChrome (via ViewportToChromeLocal), and
///   - the reused combat canvas pieces — player icon (CombatIconPresenter) and the
///     horizontal HP display (HPNumericDisplayHorizontal, player side) — via
///     ViewportToCanvasAnchored.
///
/// §08 order: row 0 = 离开商店 | avatar+username | HP | money ... ❚❚ (all level);
/// row 1 = rarity odds; row 2 = wins / hearts / income. Rows 1-2 are left-aligned on
/// HudColumnViewportX, i.e. the column the avatar block starts.
///
/// Positions are resolution-independent; sizes stay per-system (world units for
/// chips, canvas reference pixels for the reused components). Caveat: the chrome's
/// exit/options buttons are laid out in WORLD units, so their viewport footprint
/// shrinks as the camera half-width grows — these X constants are tuned against the
/// shipping Game view and a markedly different aspect will shift the relationship.
/// </summary>
public static class ShopTopBarLayout
{
	// Row 0 (avatar -> HP -> money, left to right after 离开商店), anchored on each
	// piece's CENTER; the canvas pieces already anchor bottom-left, so the anchored
	// position computed below is their center in canvas reference pixels.
	// The avatar's Y sits a touch below the HP/money row so its oversized decoration
	// (see PlayerIconShopScale) clears the viewport top.
	public static readonly Vector2 PlayerIconViewport = new Vector2(0.292f, 0.956f);
	public static readonly Vector2 HpDisplayViewport = new Vector2(0.437f, 0.959f);
	public static readonly Vector2 MoneyChipViewport = new Vector2(0.654f, 0.959f);

	// Rows 1-2: rarity odds, then wins / hearts / income — left-aligned on the HUD column.
	// The column X is set just left of the avatar block so the odds row clears the reroll
	// button in the Shop panel header (which sits at world Y 4.33, between the two rows).
	public const float OddsRowViewportY = 0.883f;
	public const float StatsRowViewportY = 0.810f;
	public const float HudColumnViewportX = 0.252f;

	// Scale overrides applied to the reused combat components while in the Shop phase
	// (their combat scale is captured at Awake and restored on combat entry).
	// PlayerIconShopScale is bounded by the icon's DECORATION, not its 100x100 rect: the
	// `small shadow` child is 222x306, so the visible block is ~3.06x the rect height. At
	// 0.35 the block top (icon center + 0.60u) crosses the viewport top and clips; 0.28
	// keeps the whole block inside with the anchor on the HUD row.
	public const float PlayerIconShopScale = 0.28f;
	public const float HpDisplayShopScale = 0.5f;

	/// <summary>
	/// Viewport position to a canvas RectTransform anchoredPosition. Requires the
	/// target's anchors to sit at the canvas bottom-left ((0,0), as the combat HUD
	/// pieces do), so the anchored position is simply the reference-pixel offset.
	/// </summary>
	public static Vector2 ViewportToCanvasAnchored(Vector2 viewport, Canvas canvas)
	{
		float scale = canvas != null ? canvas.scaleFactor : 1f;
		if (scale <= 0.0001f)
		{
			scale = 1f;
		}
		return new Vector2(viewport.x * Screen.width / scale, viewport.y * Screen.height / scale);
	}

	/// <summary>
	/// Viewport position to a ShopChrome-local offset (the chrome root is placed ONCE at
	/// Build: camera XY, BandInsetFromTop below the viewport top — page content since the
	/// 2026-09-21 world-scroll port, no per-frame anchor).
	/// </summary>
	public static Vector2 ViewportToChromeLocal(Vector2 viewport, Camera cam)
	{
		if (cam == null)
		{
			return Vector2.zero;
		}
		float halfW = cam.orthographicSize * cam.aspect;
		float localX = (viewport.x - 0.5f) * 2f * halfW;
		float localY = (viewport.y - 1f) * 2f * cam.orthographicSize + ShopChrome.BandInsetFromTop;
		return new Vector2(localX, localY);
	}

	/// <summary>Chrome-local Y of a viewport Y (same origin as ViewportToChromeLocal's y).</summary>
	public static float ViewportToChromeLocalY(float viewportY, Camera cam)
	{
		if (cam == null)
		{
			return 0f;
		}
		return (viewportY - 1f) * 2f * cam.orthographicSize + ShopChrome.BandInsetFromTop;
	}
}
