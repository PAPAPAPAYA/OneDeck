using UnityEngine;

/// <summary>
/// Shop page layout tuning, per-element placement model (2026-09-23 step 2,
/// plan-shop-per-element-placement; supersedes the row/stack model of cf8c52c): every
/// top-bar element — 7 chips, the exit button, the avatar/HP mirror anchors — carries
/// its OWN placement in ONE Resources asset, the single Inspector surface.
/// Coordinates: X = the element center's world-unit offset from the view's LEFT edge
/// (the whole band rides the edge, so chip gaps stay FIXED across aspect changes);
/// Y = viewport fraction (aspect-safe: the vertical span is locked by orthoSize).
/// ❚❚ stays right-edge anchored (corner hug). Widths/heights/font sizes are per
/// element, world units.
/// Defaults: the ...Default statics below are the single default source — instance
/// field initializers AND code fallbacks reference them. They are baked from the
/// former row-stack formula at the shipping camera (ortho 6.06, aspect 16:9, 2x halfW
/// = 21.5467), so the default layout is pixel-identical to cf8c52c at that window;
/// other aspects need a one-time Play calibration (what this feature is for).
/// Live path: OnValidate → ShopChrome.ApplyLayout / ShopPageHud.ApplyMirrorLayout
/// (same mechanics as cf8c52c); canvas HUD pieces read the shared ShopTopBarLayout
/// resolvers at their next placement application. Overlap between elements is NOT
/// guarded (user ruling 2026-09-23) — values are hand-tuned.
/// </summary>
public class ShopLayoutConfigSO : ScriptableObject
{
	/// <summary>
	/// One element's placement. X is world units from the view's left edge to the
	/// element CENTER; Y a viewport fraction; size/font world units (world-TMP
	/// calibration: ~0.13u per font point at scale 1).
	/// </summary>
	[System.Serializable]
	public class ElementPlacement
	{
		public float xFromLeftEdge;
		public float viewportY;
		public float width;
		public float height;
		public float fontSize;
	}

	// ------------------------------------------------------------------ defaults
	// Baked at ortho 6.06 / aspect 16:9 (2x halfW = 21.5467) from the former row-stack
	// formula — see plan §2 for the derivation of each number.

	public static readonly ElementPlacement ExitButtonDefault = new ElementPlacement
		{ xFromLeftEdge = 1.65f, viewportY = 0.956f, width = 2.6f, height = 0.64f, fontSize = 2.8f };
	public static readonly ElementPlacement MoneyChipDefault = new ElementPlacement
		{ xFromLeftEdge = 14.09f, viewportY = 0.959f, width = 2.4f, height = 0.62f, fontSize = 2.8f };
	public static readonly ElementPlacement RarityCommonDefault = new ElementPlacement
		{ xFromLeftEdge = 6.28f, viewportY = 0.883f, width = 1.7f, height = 0.5f, fontSize = 1.9f };
	public static readonly ElementPlacement RarityUncommonDefault = new ElementPlacement
		{ xFromLeftEdge = 8.23f, viewportY = 0.883f, width = 1.7f, height = 0.5f, fontSize = 1.9f };
	public static readonly ElementPlacement RarityRareDefault = new ElementPlacement
		{ xFromLeftEdge = 10.18f, viewportY = 0.883f, width = 1.7f, height = 0.5f, fontSize = 1.9f };
	public static readonly ElementPlacement WinsChipDefault = new ElementPlacement
		{ xFromLeftEdge = 6.28f, viewportY = 0.810f, width = 1.7f, height = 0.5f, fontSize = 1.9f };
	public static readonly ElementPlacement HeartsChipDefault = new ElementPlacement
		{ xFromLeftEdge = 8.23f, viewportY = 0.810f, width = 1.7f, height = 0.5f, fontSize = 1.9f };
	public static readonly ElementPlacement IncomeChipDefault = new ElementPlacement
		{ xFromLeftEdge = 11.28f, viewportY = 0.810f, width = 2.2f, height = 0.5f, fontSize = 2.2f };

	public const float DefaultOptionsRightMargin = 0.35f; // face right edge to the view's right edge, world units
	public const float DefaultOptionsViewportY = 0.956f;
	public const float DefaultOptionsWidth = 0.72f;
	public const float DefaultOptionsHeight = 0.64f;
	public const float DefaultOptionsFontSize = 2.4f;
	public const float RestShadowUnitsDefault = 0.05f;
	public const float DenyShiftUnitsDefault = 0.075f;

	// ------------------------------------------------------------------ singleton + resolvers

	private static ShopLayoutConfigSO _me;
	private static bool _loadAttempted;

	/// <summary>Lazy singleton. Null (no error) when the asset does not exist: defaults apply.</summary>
	public static ShopLayoutConfigSO Me
	{
		get
		{
			if (_me == null && !_loadAttempted)
			{
				_loadAttempted = true;
				_me = Resources.Load<ShopLayoutConfigSO>("ShopLayoutConfig");
			}
			return _me;
		}
	}

	/// <summary>Config-aware float read: the asset value when present, else the code default.</summary>
	public static float V(System.Func<ShopLayoutConfigSO, float> selector, float fallback)
	{
		ShopLayoutConfigSO cfg = Me;
		return cfg != null ? selector(cfg) : fallback;
	}

	/// <summary>Config-aware placement read: the asset's element when present, else the code default.</summary>
	public static ElementPlacement Placement(System.Func<ShopLayoutConfigSO, ElementPlacement> selector, ElementPlacement fallback)
	{
		ShopLayoutConfigSO cfg = Me;
		return cfg != null ? selector(cfg) : fallback;
	}

	// ------------------------------------------------------------------ fields

	[Header("Band & Root (page content, captured once at Build)")]
	[Tooltip("Reserved clearance zone at the page top in world units; also the shelf layout contract limit via ShopChrome.BandBottomWorldY.")]
	public float bandHeight = ShopChrome.BandHeightDefault;
	[Tooltip("Chrome root center below the viewport top edge, world units.")]
	public float bandInsetFromTop = ShopChrome.BandInsetFromTopDefault;
	[Tooltip("Chrome z distance in front of the camera, world units.")]
	public float cameraForwardOffset = ShopChrome.CameraForwardOffsetDefault;

	[Header("Exit Button (离开商店; X = world offset from the view's left edge to center)")]
	public ElementPlacement exitButton = ExitButtonDefault;

	[Header("Options Button (❚❚, right-edge anchored)")]
	[Tooltip("❚❚ face right edge to the view's right edge, world units (corner hug survives aspect changes).")]
	public float optionsRightMargin = DefaultOptionsRightMargin;
	public float optionsViewportY = DefaultOptionsViewportY;
	public float optionsWidth = DefaultOptionsWidth;
	public float optionsHeight = DefaultOptionsHeight;
	public float optionsFontSize = DefaultOptionsFontSize;

	[Header("Chips (each fully placeable; X = world offset from the view's left edge to center)")]
	public ElementPlacement moneyChip = MoneyChipDefault;
	public ElementPlacement rarityCommon = RarityCommonDefault;
	public ElementPlacement rarityUncommon = RarityUncommonDefault;
	public ElementPlacement rarityRare = RarityRareDefault;
	public ElementPlacement wins = WinsChipDefault;
	public ElementPlacement hearts = HeartsChipDefault;
	public ElementPlacement income = IncomeChipDefault;

	[Header("Avatar & HP Mirror (shared with the canvas HUD pieces)")]
	[Tooltip("Player avatar block center: world-unit offset from the view's left edge.")]
	public float playerIconXFromLeftEdge = 6.29f;
	public float playerIconViewportY = 0.956f;
	[Tooltip("Shop-phase scale override for the canvas player icon (bounded by its 222x306 decoration — above ~0.35 it clips the viewport top).")]
	public float playerIconShopScale = ShopTopBarLayout.PlayerIconShopScaleDefault;
	[Tooltip("HP pill center: world-unit offset from the view's left edge.")]
	public float hpDisplayXFromLeftEdge = 9.42f;
	public float hpDisplayViewportY = 0.959f;
	[Tooltip("Shop-phase scale override for the canvas horizontal HP display.")]
	public float hpDisplayShopScale = ShopTopBarLayout.HpDisplayShopScaleDefault;

	[Header("Button Feel (both world buttons)")]
	[Tooltip("Rest/hover shadow depth, world units.")]
	public float restShadowUnits = RestShadowUnitsDefault;
	[Tooltip("Deny shake distance, world units.")]
	public float denyShiftUnits = DenyShiftUnitsDefault;

	private void OnValidate()
	{
		// Live tuning path: every Inspector edit (Play included) re-applies the built
		// chrome and rebuilds the world avatar/HP mirrors. No-op before Bootstrap built
		// them (edit mode, headless). The mirror rebuild defers mid-transition itself.
		if (ShopChrome.Instance != null) ShopChrome.Instance.ApplyLayout();
		if (ShopPageHud.Instance != null) ShopPageHud.Instance.ApplyMirrorLayout();
	}
}
