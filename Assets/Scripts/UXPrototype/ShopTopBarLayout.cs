using UnityEngine;

/// <summary>
/// Shop-phase canvas placement for the reused combat HUD pieces — WORLD-DRIVEN since the
/// mirror prefab port (2026-09-24, plan-shop-mirror-prefab): the canvas avatar/HP park at
/// the world position of the built ShopHudPage's Avatar / HpPill prefab children,
/// inverted into canvas space, so the prefab is the single placement source (move the
/// widget in the prefab stage and the transition flight follows). Before the chrome is
/// built (headless / batch / early frames) the resolvers fall back to the last-authored
/// SO-era values baked as consts below — the numbers the deleted ShopLayoutConfig.asset
/// carried (NOT the old code defaults).
/// </summary>
public static class ShopTopBarLayout
{
	// Shipping Main Camera ortho size — the fallback when Camera.main is unavailable at
	// a canvas placement read (GameScene Main Camera value).
	public const float FallbackOrthoSize = 6.06f;

	// SO-era fallback anchors/scales (chrome-not-built reads only). These are the
	// user-tuned values the deleted ShopLayoutConfig.asset held; the live path reads the
	// built page's prefab children instead.
	private const float FallbackPlayerIconXFromLeftEdge = 2.57f;
	private const float FallbackPlayerIconViewportY = 0.88f;
	public const float FallbackAvatarShopScale = 0.41f;
	private const float FallbackHpDisplayXFromLeftEdge = 4.02f;
	private const float FallbackHpDisplayViewportY = 0.88f;
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

	/// <summary>Player avatar shop anchor: the built page's Avatar world center, inverted; SO-era algebra as headless fallback.</summary>
	public static Vector2 ShopAnchorAvatar(Canvas canvas)
	{
		return ShopChrome.TryGetAvatarWorldCenter(out Vector3 world)
			? WorldToCanvasAnchored(world, ShopChrome.BuildCameraCenter, canvas, MainOrthoSize)
			: FallbackAnchor(FallbackPlayerIconXFromLeftEdge, FallbackPlayerIconViewportY, canvas);
	}

	/// <summary>HP pill shop anchor: the built page's HpPill world center, inverted; SO-era algebra as headless fallback.</summary>
	public static Vector2 ShopAnchorHpDisplay(Canvas canvas)
	{
		return ShopChrome.TryGetHpPillWorldCenter(out Vector3 world)
			? WorldToCanvasAnchored(world, ShopChrome.BuildCameraCenter, canvas, MainOrthoSize)
			: FallbackAnchor(FallbackHpDisplayXFromLeftEdge, FallbackHpDisplayViewportY, canvas);
	}

	/// <summary>Shop scale authored on the Tpl_Avatar root; fallback const before the chrome is built.</summary>
	public static float ShopScaleAvatar => ShopChrome.AvatarShopScale ?? FallbackAvatarShopScale;

	/// <summary>Shop scale authored on the Tpl_HpPill root; fallback const before the chrome is built.</summary>
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
