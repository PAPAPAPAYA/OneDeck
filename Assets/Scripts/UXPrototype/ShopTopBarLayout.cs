using UnityEngine;

/// <summary>
/// Shop-phase canvas placement for the reused combat HUD pieces — WORLD-DRIVEN since the
/// mirror prefab port (2026-09-24, plan-shop-mirror-prefab; avatar+HP merged into the
/// NamePlate 2026-10-07, plan-hp-name-plate-2026-10-07): the canvas plate parks at the
/// world position of the built ShopHudPage's NamePlate prefab child, inverted into
/// canvas space, so the prefab is the single placement source (move the widget in the
/// prefab stage and the transition flight follows). Before the chrome is built
/// (headless / batch / early frames) the resolvers fall back to the consts below.
/// </summary>
public static class ShopTopBarLayout
{
	// Shipping Main Camera ortho size — the fallback when Camera.main is unavailable at
	// a canvas placement read (GameScene Main Camera value).
	public const float FallbackOrthoSize = 6.06f;

	// SO-era fallback anchor/scale (chrome-not-built reads only). Retuned 2026-10-07 to
	// the NamePlate mirror's page spot (plan-hp-name-plate-2026-10-07): the built page's
	// NamePlate child at page-local (-4.45, -1.8) resolves to world (-4.35, 3.76) on the
	// authored page root (0.1, 5.56, -98); the live path reads the built page instead.
	// ViewportY derives from the WORLD Y (fixes plan 2026-10-07 §5): (3.76 + ortho 6.06) /
	// (2 x 6.06) = 0.81 — the first cut baked the page-LOCAL Y and parked mid-screen.
	private const float FallbackHpDisplayXFromLeftEdge = 6.42f;
	private const float FallbackHpDisplayViewportY = 0.81f;
	public const float FallbackHpDisplayShopScale = 0.74f;

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
	/// World position → canvas anchoredPosition (bottom-left anchor) — the inverse of the
	/// old left-offset algebra, in the SAME terms: x is re-expressed as an offset from the
	/// view's left edge via orthoSize x Camera.aspect (NOT Screen.width — the camera
	/// viewport aspect is what the old formula baked), then multiplied by
	/// pxPerWorldUnit = Screen.height / (2 x orthoSize); Y offsets from the captured
	/// camera center. Both divide by the canvas scale. The camera center/aspect must come
	/// from the chrome's BUILD-time capture semantics — the parked anchors are page
	/// content and must not follow a scrolled camera.
	/// </summary>
	public static Vector2 WorldToCanvasAnchored(Vector3 worldPos, Vector3 cameraCenter, Canvas canvas, float orthoSize)
	{
		float scale = canvas != null ? canvas.scaleFactor : 1f;
		if (scale <= 0.0001f)
		{
			scale = 1f;
		}
		Camera cam = Camera.main;
		float aspect = cam != null ? cam.aspect : 16f / 9f;
		float pxPerWorldUnit = Screen.height / Mathf.Max(0.0001f, 2f * orthoSize);
		float xFromLeftEdge = (worldPos.x - cameraCenter.x) + orthoSize * aspect;
		float px = xFromLeftEdge * pxPerWorldUnit;
		float py = Screen.height * 0.5f + (worldPos.y - cameraCenter.y) * pxPerWorldUnit;
		return new Vector2(px / scale, py / scale);
	}

	/// <summary>HP name plate shop anchor: the built page's NamePlate world center, inverted; SO-era algebra as headless fallback (plan-hp-name-plate-2026-10-07 §3.4).</summary>
	public static Vector2 ShopAnchorHpDisplay(Canvas canvas)
	{
		return ShopChrome.TryGetNamePlateWorldCenter(out Vector3 world)
			? WorldToCanvasAnchored(world, ShopChrome.BuildCameraCenter, canvas, MainOrthoSize)
			: FallbackAnchor(FallbackHpDisplayXFromLeftEdge, FallbackHpDisplayViewportY, canvas);
	}

	/// <summary>Shop scale authored on the Tpl_HpNamePlate root; fallback const before the chrome is built.</summary>
	public static float ShopScaleHpDisplay => ShopChrome.HpDisplayShopScale ?? FallbackHpDisplayShopScale;

	private static Vector2 FallbackAnchor(float xFromLeftEdge, float viewportY, Canvas canvas)
	{
		float scale = canvas != null ? canvas.scaleFactor : 1f;
		if (scale <= 0.0001f)
		{
			scale = 1f;
		}
		float pxPerWorldUnit = Screen.height / Mathf.Max(0.0001f, 2f * MainOrthoSize);
		return new Vector2(xFromLeftEdge * pxPerWorldUnit / scale, viewportY * Screen.height / scale);
	}
}
