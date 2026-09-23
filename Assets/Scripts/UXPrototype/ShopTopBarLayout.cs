using UnityEngine;

/// <summary>
/// Shared top-bar placement for the Shop phase — LEFT-ALIGNED band model (2026-09-23
/// step 2, plan-shop-per-element-placement; supersedes the viewport-X model of
/// cf8c52c). Consumed by both render systems that draw the shop top bar:
///   - the world-space chrome (ShopChrome chips/buttons + ShopPageHud mirrors) via
///     ChromeLocalXFromLeftOffset + ViewportToChromeLocalY, and
///   - the reused combat canvas pieces — player icon (CombatIconPresenter) and the
///     horizontal HP display (HPNumericDisplayHorizontal, player side) — via
///     LeftOffsetToCanvasAnchored.
///
/// Coordinates: X = the element center's world-unit offset from the view's LEFT edge
/// (the band rides the edge, so gaps between elements stay FIXED in world units
/// across aspect changes — the whole reason for this semantics); Y = viewport
/// fraction, which is aspect-safe (the vertical span is locked by orthoSize).
/// Demo §08 order: 离开商店 | avatar+username | HP | money | wins/hearts | income,
/// with the rarity odds row above; exact arrangement is data (ShopLayoutConfigSO).
///
/// Values resolve through ShopLayoutConfigSO (Resources/ShopLayoutConfig) with the
/// ...Default constants here as fallbacks; the resolving properties keep stable API
/// names for the four consumers.
/// </summary>
public static class ShopTopBarLayout
{
	// Fallback defaults — the single default source for the mirror anchors (the
	// ShopLayoutConfigSO field initializers reference these too). Baked at ortho 6.06 /
	// aspect 16:9 (2x halfW = 21.5467) from the former viewport constants 0.292 / 0.437.
	public const float PlayerIconXFromLeftEdgeDefault = 6.29f;
	public const float PlayerIconViewportYDefault = 0.956f;
	public const float HpDisplayXFromLeftEdgeDefault = 9.42f;
	public const float HpDisplayViewportYDefault = 0.959f;

	// Scale overrides applied to the reused combat components while in the Shop phase
	// (their combat scale is captured at Awake and restored on combat entry).
	// PlayerIconShopScale is bounded by the icon's DECORATION, not its 100x100 rect: the
	// `small shadow` child is 222x306, so the visible block is ~3.06x the rect height. At
	// 0.35 the block top (icon center + 0.60u) crosses the viewport top and clips; 0.28
	// keeps the whole block inside with the anchor on the HUD row.
	public const float PlayerIconShopScaleDefault = 0.28f;
	public const float HpDisplayShopScaleDefault = 0.5f;

	// Shipping Main Camera ortho size — the fallback when Camera.main is unavailable at
	// a canvas placement read (GameScene Main Camera value; see plan §3.2).
	public const float FallbackOrthoSize = 6.06f;

	// Config-resolving read points — stable names for the consumers.
	public static float PlayerIconXFromLeftEdge => ShopLayoutConfigSO.V(c => c.playerIconXFromLeftEdge, PlayerIconXFromLeftEdgeDefault);
	public static float PlayerIconViewportY => ShopLayoutConfigSO.V(c => c.playerIconViewportY, PlayerIconViewportYDefault);
	public static float HpDisplayXFromLeftEdge => ShopLayoutConfigSO.V(c => c.hpDisplayXFromLeftEdge, HpDisplayXFromLeftEdgeDefault);
	public static float HpDisplayViewportY => ShopLayoutConfigSO.V(c => c.hpDisplayViewportY, HpDisplayViewportYDefault);
	public static float PlayerIconShopScale => ShopLayoutConfigSO.V(c => c.playerIconShopScale, PlayerIconShopScaleDefault);
	public static float HpDisplayShopScale => ShopLayoutConfigSO.V(c => c.hpDisplayShopScale, HpDisplayShopScaleDefault);

	/// <summary>Camera.main's ortho size with the shipping fallback (canvas-side reads).</summary>
	public static float MainOrthoSize
	{
		get
		{
			Camera cam = Camera.main;
			return cam != null ? cam.orthographicSize : FallbackOrthoSize;
		}
	}

	/// <summary>
	/// World-unit offset from the view's left edge to a chrome-LOCAL x (the chrome root
	/// sits at the camera center: view-left is -halfW). The whole band rides the edge,
	/// which is what keeps inter-element gaps fixed across aspect changes.
	/// </summary>
	public static float ChromeLocalXFromLeftOffset(float xFromLeftEdge, float halfW)
	{
		return xFromLeftEdge - halfW;
	}

	/// <summary>Chrome-local Y of a viewport Y (chrome root is BandInsetFromTop below the
	/// viewport top — page content since the 2026-09-21 world-scroll port).</summary>
	public static float ViewportToChromeLocalY(float viewportY, Camera cam)
	{
		return cam != null ? ViewportToChromeLocalY(viewportY, cam.orthographicSize) : 0f;
	}

	/// <summary>
	/// Same conversion from a build-time captured ortho size — no camera needed, so callers
	/// re-placing chrome children (ShopChrome.ApplyLayout) stay scroll-safe:
	/// the chrome root is page content and must not follow the live camera transform.
	/// </summary>
	public static float ViewportToChromeLocalY(float viewportY, float orthoSize)
	{
		return (viewportY - 1f) * 2f * orthoSize + ShopChrome.BandInsetFromTop;
	}

	/// <summary>
	/// Left-aligned band anchor to a canvas RectTransform anchoredPosition (the combat
	/// HUD pieces anchor bottom-left). X: world-unit left offset → screen px, where the
	/// aspect term cancels — offset / (2 x halfW) x Screen.width with halfW =
	/// orthoSize x Screen.width/Screen.height reduces to offset x Screen.height /
	/// (2 x orthoSize). Y: viewport fraction x Screen.height, as before. Both then
	/// divide by the canvas scale into reference pixels.
	/// </summary>
	public static Vector2 LeftOffsetToCanvasAnchored(float xFromLeftEdge, float viewportY, Canvas canvas, float orthoSize)
	{
		float scale = canvas != null ? canvas.scaleFactor : 1f;
		if (scale <= 0.0001f)
		{
			scale = 1f;
		}
		float pxPerWorldUnit = Screen.height / Mathf.Max(0.0001f, 2f * orthoSize);
		return new Vector2(xFromLeftEdge * pxPerWorldUnit / scale, viewportY * Screen.height / scale);
	}
}
