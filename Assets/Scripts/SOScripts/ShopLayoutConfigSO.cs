using UnityEngine;

/// <summary>
/// Shop page layout tuning (2026-09-23, plan-shop-layout-config-widget-factory): every
/// chrome-band placement value (band/root, row 0 buttons + money chip, chip rows 1-2),
/// the avatar/HP mirror viewport anchors + shop scales, and the world-button feel params
/// in ONE Resources asset — the single Inspector surface for shop page layout.
/// Defaults stay in code: the field initializers reference the shipped consts
/// (ShopChrome.*Default / ShopTopBarLayout.*Default), so a MISSING asset is legal and
/// falls back to the same values (headless/tests never hard-fail) and the defaults can
/// never drift between asset and code. Every layout read resolves the asset at point of
/// use (ShopLayoutConfigSO.V / V2) — never snapshotted.
/// Live path: OnValidate nudges ShopChrome.ApplyLayout (band + chips + buttons, same
/// frame) and ShopPageHud.ApplyMirrorLayout (world avatar/HP rebuild, deferred
/// mid-transition); the canvas HUD pieces (CombatIconPresenter / HPNumericDisplayHorizontal)
/// read the shared ShopTopBarLayout resolvers at their next placement application, so
/// they pick up retuned values at the next phase handoff with no extra wiring.
/// Absorbs ShopChromeConfigSO (2026-09-23, exit-button-only knobs) — deleted.
/// </summary>
public class ShopLayoutConfigSO : ScriptableObject
{
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

	/// <summary>Config-aware Vector2 read (viewport anchors): the asset value when present, else the code default.</summary>
	public static Vector2 V2(System.Func<ShopLayoutConfigSO, Vector2> selector, Vector2 fallback)
	{
		ShopLayoutConfigSO cfg = Me;
		return cfg != null ? selector(cfg) : fallback;
	}

	[Header("Band & Root (page content, captured once at Build)")]
	[Tooltip("Reserved clearance zone at the page top in world units; also the shelf layout contract limit via ShopChrome.BandBottomWorldY.")]
	public float bandHeight = ShopChrome.BandHeightDefault;
	[Tooltip("Chrome root center below the viewport top edge, world units.")]
	public float bandInsetFromTop = ShopChrome.BandInsetFromTopDefault;
	[Tooltip("Chrome z distance in front of the camera, world units.")]
	public float cameraForwardOffset = ShopChrome.CameraForwardOffsetDefault;

	[Header("Row 0 — exit / options buttons, money chip")]
	[Tooltip("Viewport Y of the 离开商店 button center. Only this button moves; avatar/HP/money keep their row.")]
	[Range(0.5f, 1f)] public float exitButtonViewportY = ShopTopBarLayout.PlayerIconViewportDefault.y;
	[Tooltip("Horizontal offset of the 离开商店 button from its left-edge anchor, world units (+ = right). Shelf keeps no avoidance against a moved button (user ruling 2026-09-22).")]
	public float exitButtonOffsetX = 0f;
	[Tooltip("离开商店 button face width, world units.")]
	public float exitButtonWidth = ShopChrome.ExitButtonWidthDefault;
	[Tooltip("离开商店 label font size.")]
	public float exitButtonFontSize = ShopChrome.ExitButtonFontSizeDefault;
	[Tooltip("Shared face height of both row-0 buttons, world units.")]
	public float buttonHeight = ShopChrome.ButtonHeightDefault;
	[Tooltip("❚❚ label font size.")]
	public float buttonFontSize = ShopChrome.ButtonFontSizeDefault;
	[Tooltip("❚❚ button face width, world units (right-anchored at the edge margin).")]
	public float optionsButtonWidth = ShopChrome.OptionsButtonWidthDefault;
	[Tooltip("Edge margin for the exit (left anchor) and options (right anchor) buttons, world units.")]
	public float worldEdgeMargin = ShopChrome.EdgeMarginDefault;
	[Tooltip("Money chip center (viewport space, (0,1) = top-left).")]
	public Vector2 moneyChipViewport = ShopTopBarLayout.MoneyChipViewportDefault;
	[Tooltip("Money chip panel width, world units (demo chip-lg).")]
	public float moneyChipWidth = ShopChrome.MoneyChipWidthDefault;
	[Tooltip("Money chip panel height, world units.")]
	public float moneyChipHeight = ShopChrome.MoneyChipHeightDefault;
	[Tooltip("Money chip label font size.")]
	public float moneyChipFontSize = ShopChrome.MoneyChipFontSizeDefault;

	[Header("Rows 1-2 — rarity odds / wins-hearts-income chips")]
	[Tooltip("Left-aligned column X for both chip rows (viewport space; set to clear the reroll button in the Shop panel header).")]
	public float hudColumnViewportX = ShopTopBarLayout.HudColumnViewportXDefault;
	[Tooltip("Rarity odds row Y (viewport space).")]
	public float oddsRowViewportY = ShopTopBarLayout.OddsRowViewportYDefault;
	[Tooltip("Wins/hearts/income row Y (viewport space).")]
	public float statsRowViewportY = ShopTopBarLayout.StatsRowViewportYDefault;
	[Tooltip("In-row chip gap, world units.")]
	public float chipSpacing = ShopChrome.ChipSpacingDefault;
	[Tooltip("Small chip (rarity/wins/hearts) panel width, world units (demo chip-sm).")]
	public float smallChipWidth = ShopChrome.SmallChipWidthDefault;
	[Tooltip("Small chip panel height, world units.")]
	public float chipHeight = ShopChrome.ChipHeightDefault;
	[Tooltip("Small chip label font size.")]
	public float smallChipFontSize = ShopChrome.SmallChipFontSizeDefault;
	[Tooltip("Income chip panel width, world units (large chip).")]
	public float chipWidth = ShopChrome.ChipWidthDefault;
	[Tooltip("Income chip label font size.")]
	public float chipFontSize = ShopChrome.ChipFontSizeDefault;

	[Header("Avatar & HP Mirror (shared with the canvas HUD pieces)")]
	[Tooltip("Player avatar block center (viewport space) — world mirror (ShopPageHud) and canvas icon (CombatIconPresenter).")]
	public Vector2 playerIconViewport = ShopTopBarLayout.PlayerIconViewportDefault;
	[Tooltip("HP pill center (viewport space) — world mirror and canvas HPNumericDisplayHorizontal.")]
	public Vector2 hpDisplayViewport = ShopTopBarLayout.HpDisplayViewportDefault;
	[Tooltip("Shop-phase scale override for the canvas player icon (bounded by its 222x306 decoration — above ~0.35 it clips the viewport top).")]
	public float playerIconShopScale = ShopTopBarLayout.PlayerIconShopScaleDefault;
	[Tooltip("Shop-phase scale override for the canvas horizontal HP display.")]
	public float hpDisplayShopScale = ShopTopBarLayout.HpDisplayShopScaleDefault;

	[Header("Button Feel (both row-0 world buttons)")]
	[Tooltip("Rest/hover shadow depth, world units.")]
	public float restShadowUnits = ShopChrome.RestShadowUnitsDefault;
	[Tooltip("Deny shake distance, world units.")]
	public float denyShiftUnits = ShopChrome.DenyShiftUnitsDefault;

	private void OnValidate()
	{
		// Live tuning path: every Inspector edit (Play included) re-applies the built
		// chrome and rebuilds the world avatar/HP mirrors. No-op before Bootstrap built
		// them (edit mode, headless). The mirror rebuild defers mid-transition itself.
		if (ShopChrome.Instance != null) ShopChrome.Instance.ApplyLayout();
		if (ShopPageHud.Instance != null) ShopPageHud.Instance.ApplyMirrorLayout();
	}
}
