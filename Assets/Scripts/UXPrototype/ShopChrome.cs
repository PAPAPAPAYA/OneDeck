using System.Collections.Generic;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;

// Shop top chrome (Guidelines 3.6) as WORLD page content: flat read-only chips plus the
// exit / options physical buttons floating in a reserved viewport-top zone (no full-bleed
// band — the units sit directly on the shop background). 2026-09-19 §08 layout: row 0 =
// 离开商店 | the reused combat pieces as world mirrors (avatar+username, horizontal HP
// pill — ShopPageHud) | money chip ... ❚❚, all on one line; row 1 = rarity odds; row 2 =
// wins / hearts / income — rows 1-2 left-aligned on the HUD column the avatar block
// starts (ShopTopBarLayout viewport anchors, shared with the mirrored components). The
// reroll button and the deck slot count live in the Shop/Deck panel headers
// (ShopSectionPanels). Chrome copy is Chinese (user decision 2026-09-19); glyph labels
// (❚❚ / 🜲 / ♥) resolve through the chrome font fallback chain (NotoSansSymbols2 SDF +
// NotoSansSymbolsAlchemical SDF).
// Replaces the 2026-09-17 canvas ShopHudBar (plan-world-entity-shop-chrome-2026-09-18) —
// one input pipeline (physics OnMouse), one input gate (ShopInputGate), no canvas layer.
// Runtime-built from ShopUXManager.Start (no scene edit); colors from GameColorPalette.
//
// 2026-09-21 world scroll (plan-shop-topbar-world-scroll-2026-09-21): the bar is PAGE
// content, not camera-pinned. The old ShopChromeAnchor LateUpdate copy is gone — Build()
// writes the root position ONCE (the same spot the anchor computed at scroll 0) and the
// whole bar then scrolls away with the wheel like any other shop world content. A
// scrolled-down shop shows no exit button on purpose (user ruling 2026-09-21): scroll
// back up, or press Space (the PhaseManager shortcut). BandBottomWorldY is captured at
// Build for the same reason — at scroll 0 it reads the identical value the live-camera
// version produced.
// VISUAL-FIX(2026-09-21): whole top bar (chips + buttons + avatar/HP) stayed pinned to
//   the viewport top while the shop page scrolled away under the wheel
//   Cause:    ShopChromeAnchor copied Camera.main into the chrome root every LateUpdate,
//             so the bar floated above the page no matter where the camera scrolled; the
//             avatar/HP were canvas pieces for the same reason.
//   Affects:  ShopChrome root placement (now written once at Build) + BandBottomWorldY
//             (build-time captured); canvas avatar/HP visibility moved to the presenters'
//             shop-handoff rule; world copies live in ShopPageHud under this root.
//   Regress:  Shop at scroll 0 renders the bar pixel-identical to the anchor build; wheel
//             down takes chips, buttons, avatar and HP pill away with the cards; wheel up
//             brings them back; Space exits from a fully scrolled shop; CheckShelfClearance
//             limit unchanged; combat/result HUD (canvas) untouched.
//
// 2026-09-23 layout config (plan-shop-layout-config-widget-factory): every placement
// value (band/root, buttons, chips) resolves through ShopLayoutConfigSO
// (Resources/ShopLayoutConfig) with the ...Default consts below as fallbacks, and
// ApplyLayout() re-writes root + all children from the config live (OnValidate path).
// Widget recipes (button/chip/label/sliced) live in ShopWorldWidgets — this file keeps
// placement only.
//
// Layout contract: the band occupies the top of the page (= the viewport at scroll 0).
// Card layout must keep the shelf below BandBottomWorldY; ShopUXManager asserts it once
// per shelf build.
public class ShopChrome : MonoBehaviour
{
	private const string ChromeName = "Shop Chrome";

	// Fallback defaults for the live layout config (single default source: the
	// ShopLayoutConfigSO field initializers reference these). Every placement read
	// resolves the asset at point of use via ShopLayoutConfigSO.V — never snapshotted —
	// so a Play-mode Inspector edit applies on the next ApplyLayout.
	public const float BandHeightDefault = 2.6f;      // reserved clearance zone at the page top (2 HUD rows, no rendered band)
	public const float CameraForwardOffsetDefault = 2f; // chrome z distance in front of the camera
	public const float BandInsetFromTopDefault = 0.5f; // band center below the viewport top edge (Build-time root placement)

	public const float EdgeMarginDefault = 0.35f;
	public const float ChipSpacingDefault = 0.25f;
	public const float ChipWidthDefault = 2.2f;
	public const float ChipHeightDefault = 0.5f;
	public const float SmallChipWidthDefault = 1.7f;  // rarity / wins / hearts chips (demo chip-sm)
	public const float SmallChipFontSizeDefault = 1.9f;
	public const float MoneyChipWidthDefault = 2.4f;  // demo chip-lg
	public const float MoneyChipHeightDefault = 0.62f;
	public const float MoneyChipFontSizeDefault = 2.8f;
	public const float OptionsButtonWidthDefault = 0.72f;
	public const float ButtonHeightDefault = 0.64f;
	public const float ExitButtonWidthDefault = 2.6f;
	// World-TMP calibration: the card price print renders ~0.31u tall at fontSize 12 on a
	// 0.2-scaled transform -> ~0.13u per fontSize point at scale 1.
	public const float ChipFontSizeDefault = 2.2f;
	public const float ButtonFontSizeDefault = 2.4f;
	public const float ExitButtonFontSizeDefault = 2.8f;
	public const float RestShadowUnitsDefault = 0.05f; // demo rs 4px at chrome scale (tune at play look)
	public const float DenyShiftUnitsDefault = 0.075f;

	// Config-aware band values (cross-file readers: ShopTopBarLayout conversion helpers).
	public static float BandHeight => ShopLayoutConfigSO.V(c => c.bandHeight, BandHeightDefault);
	public static float BandInsetFromTop => ShopLayoutConfigSO.V(c => c.bandInsetFromTop, BandInsetFromTopDefault);
	public static float CameraForwardOffset => ShopLayoutConfigSO.V(c => c.cameraForwardOffset, CameraForwardOffsetDefault);

	private static ShopChrome _instance;
	public static ShopChrome Instance => _instance;

	// Shelf layout contract limit, captured ONCE at Build (plan-shop-topbar-world-scroll
	// §3.1): the bar is page content now, so the limit must not follow a scrolled camera.
	// float.MaxValue until built — the same "not ready" reading the old null-cam guard gave.
	private static float _bandBottomWorldY = float.MaxValue;

	private TMP_FontAsset _font;
	private Sprite _sprite;

	private HudChip _moneyChip;
	private HudChip _incomeChip;
	private HudChip _rarityCommonChip;
	private HudChip _rarityUncommonChip;
	private HudChip _rarityRareChip;
	private HudChip _winsChip;
	private HudChip _heartsChip;
	private PhysButton _optionsButton;
	private PhysButton _exitButton;
	// Build-captured geometry for ApplyLayout: viewport math must not read the live
	// camera transform (the bar is page content — re-placement stays scroll-safe).
	private float _halfW = 8f;
	private float _orthoSize = 5f;
	private Vector3 _rigPos;
	private TextMeshPro _exitLabel;
	private TextMeshPro _optionsLabel;
	private PhaseManager _phaseManager;
	private ShopPageHud _pageHud;

	private int _lastPurse = int.MinValue;
	private int _lastPayday = int.MinValue;
	private int _lastCommonOdds = int.MinValue;
	private int _lastUncommonOdds = int.MinValue;
	private int _lastRareOdds = int.MinValue;
	private int _lastWins = int.MinValue;
	private int _lastWinCon = int.MinValue;
	private int _lastHearts = int.MinValue;
	private int _lastHeartMax = int.MinValue;

	/// <summary>
	/// Bottom edge of the chrome band in world Y — the shelf layout contract limit. Captured
	/// once at Build (page content, not camera-relative).
	/// </summary>
	public static float BandBottomWorldY()
	{
		return _bandBottomWorldY;
	}

	/// <summary>
	/// Builds the chrome once (idempotent) as a scene-root object; shows it when the game
	/// is already in Shop phase (the enter-shop UnityEvent may fire before ShopUXManager.Start).
	/// </summary>
	public static void Bootstrap(Sprite sprite, TMP_FontAsset font)
	{
		if (_instance != null) return;
		if (sprite == null || font == null)
		{
			TestManager.LogWarning("[ShopChrome] chromeSprite/chromeFont not wired on ShopUXManager — world chrome skipped");
			return;
		}

		GameObject root = new GameObject(ChromeName);
		root.SetActive(false);
		_instance = root.AddComponent<ShopChrome>();
		_instance._font = font;
		_instance._sprite = sprite;
		_instance.Build();

		if (ShopManager.me != null && ShopManager.me.gamePhaseRef != null
			&& ShopManager.me.gamePhaseRef.currentGamePhase == EnumStorage.GamePhase.Shop)
		{
			ShowIfActive();
		}
	}

	public static void RefreshIfActive()
	{
		if (_instance != null && _instance.gameObject.activeSelf) _instance.Refresh();
	}

	public static void ShowIfActive()
	{
		if (_instance == null) return;
		// Showing the bar mid-travel would float it over the departing/arriving page. The
		// driver calls ShowIfActive again on landing (ResultToShopRoutine).
		if (PhaseTransitionDriver.IsTransitioning) return;
		_instance.gameObject.SetActive(true);
		_instance.Refresh();
	}

	public static void HideIfActive()
	{
		if (_instance != null) _instance.gameObject.SetActive(false);
	}

	/// <summary>
	/// Layout-contract assertion (call after every shelf build): the topmost shelf card must
	/// stay below the band, or hover/deny excursions of a price button can slide under it.
	/// </summary>
	public static void CheckShelfClearance(IEnumerable<GameObject> shelfCards)
	{
		if (_instance == null || !_instance.gameObject.activeSelf || shelfCards == null) return;
		float limit = BandBottomWorldY();
		float top = float.MinValue;
		foreach (GameObject card in shelfCards)
		{
			if (card == null) continue;
			CardPhysObjScript phys = card.GetComponent<CardPhysObjScript>();
			if (phys == null) continue;
			float cardTop = phys.TargetPosition.y + ShopSectionPanels.FaceBelowHalfHeightFactor * phys.TargetScale.y; // face half-height + price button allowance
			if (cardTop > top) top = cardTop;
		}
		if (top > limit)
		{
			TestManager.LogWarning($"[ShopChrome] shelf top {top:0.00} enters the chrome band (limit {limit:0.00}) — raise the shelf start or lower the band");
		}
	}

	private void Build()
	{
		Camera cam = Camera.main;
		_halfW = cam != null ? cam.orthographicSize * cam.aspect : 8f;
		_orthoSize = cam != null ? cam.orthographicSize : 5f;
		if (cam != null) _rigPos = cam.transform.position;

		// 2026-09-19 §08 order: row 0 = [离开商店] [avatar | HP | $ ...] ... [❚❚]; row 1 =
		// rarity odds; row 2 = wins / hearts / income. Creation only wires the objects and
		// their actions at default sizes — placement (positions, sizes, fonts) is
		// ApplyLayout's job, and the chrome root is still inactive here, so the defaults
		// never render. 2026-09-22 user ruling (kept): ONLY the exit button carries a
		// dedicated offset/Y — avatar/HP/money stay on the shared row, and the shelf keeps
		// no avoidance against a moved button (CheckShelfClearance tracks the band).
		_exitButton = ShopWorldWidgets.CreateWorldButton(transform, "ExitButton", _font, _sprite,
			"离开商店", 0f, 0f, ExitButtonWidthDefault, ButtonHeightDefault, ExitButtonFontSizeDefault,
			RestShadowUnitsDefault, DenyShiftUnitsDefault, out _exitLabel);
		_optionsButton = ShopWorldWidgets.CreateWorldButton(transform, "OptionsButton", _font, _sprite,
			"❚❚", 0f, 0f, OptionsButtonWidthDefault, ButtonHeightDefault, ButtonFontSizeDefault,
			RestShadowUnitsDefault, DenyShiftUnitsDefault, out _optionsLabel);
		_optionsButton.SetWorldAction(() => Debug.Log("[ShopChrome] Options pressed (placeholder — no options menu yet)"));
		_phaseManager = FindObjectOfType<PhaseManager>();
		if (_phaseManager == null)
		{
			TestManager.LogWarning("[ShopChrome] PhaseManager not found in scene — exit button inert");
		}
		_exitButton.SetWorldAction(() =>
		{
			if (_phaseManager == null) return;
			// Phase transition driver (plan-phase-transition-world-camera-2026-09-21): the 离开商店
			// button is THE canonical shop->combat trigger (user ruling 2026-09-21); when the driver
			// is available it wraps these calls in the camera travel, else the legacy hard cut runs.
			if (PhaseTransitionDriver.RequestShopToCombat(_phaseManager)) return;
			_phaseManager.ExitingShopPhase();
			_phaseManager.EnteringCombatPhase();
		});

		// Money chip: row 0, right of the reused combat HP display; row 1 (sm dark chips):
		// rarity odds; row 2: wins / hearts / payday (demo hud-line 3).
		_moneyChip = ShopWorldWidgets.CreateChip(transform, "ChipMoney", _sprite, _font,
			MoneyChipWidthDefault, MoneyChipHeightDefault, MoneyChipFontSizeDefault, false);
		_rarityCommonChip = ShopWorldWidgets.CreateChip(transform, "ChipRarityCommon", _sprite, _font,
			SmallChipWidthDefault, ChipHeightDefault, SmallChipFontSizeDefault, true);
		_rarityUncommonChip = ShopWorldWidgets.CreateChip(transform, "ChipRarityUncommon", _sprite, _font,
			SmallChipWidthDefault, ChipHeightDefault, SmallChipFontSizeDefault, true);
		_rarityRareChip = ShopWorldWidgets.CreateChip(transform, "ChipRarityRare", _sprite, _font,
			SmallChipWidthDefault, ChipHeightDefault, SmallChipFontSizeDefault, true);
		_winsChip = ShopWorldWidgets.CreateChip(transform, "ChipWins", _sprite, _font,
			SmallChipWidthDefault, ChipHeightDefault, SmallChipFontSizeDefault, true);
		_heartsChip = ShopWorldWidgets.CreateChip(transform, "ChipHearts", _sprite, _font,
			SmallChipWidthDefault, ChipHeightDefault, SmallChipFontSizeDefault, true);
		_incomeChip = ShopWorldWidgets.CreateChip(transform, "ChipIncome", _sprite, _font,
			ChipWidthDefault, ChipHeightDefault, ChipFontSizeDefault, false);

		ApplyLayout();

		// World avatar + HP pill (plan-shop-topbar-world-scroll §3.2): parented under this
		// root, so show/hide and wheel scroll follow the chrome for free.
		_pageHud = ShopPageHud.Bootstrap(transform);
	}

	/// <summary>
	/// Live-tuning path (ShopLayoutConfigSO.OnValidate) and Build's final placement pass:
	/// rewrites the root position, the BandBottomWorldY shelf limit and every chip/button
	/// position + face size + label font from the config. Uses ONLY Build-captured
	/// geometry (_rigPos / _halfW / _orthoSize), never the live camera transform, so
	/// applying mid-scroll stays correct (page-content rule); the BoxCollider2D rides on
	/// each button root and follows for free.
	/// </summary>
	public void ApplyLayout()
	{
		float bandInset = ShopLayoutConfigSO.V(c => c.bandInsetFromTop, BandInsetFromTopDefault);
		float bandHeight = ShopLayoutConfigSO.V(c => c.bandHeight, BandHeightDefault);
		float forwardOffset = ShopLayoutConfigSO.V(c => c.cameraForwardOffset, CameraForwardOffsetDefault);
		transform.position = new Vector3(_rigPos.x, _rigPos.y + _orthoSize - bandInset, _rigPos.z + forwardOffset);
		_bandBottomWorldY = _rigPos.y + _orthoSize - bandHeight;

		// Row 0 rides the avatar row (ShopTopBarLayout row order); rows 1-2 stack on the
		// shared HUD column the avatar block starts.
		float row0Y = ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.PlayerIconViewport.y, _orthoSize);
		float row1Y = ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.OddsRowViewportY, _orthoSize);
		float row2Y = ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.StatsRowViewportY, _orthoSize);
		float columnX = ShopTopBarLayout.ViewportToChromeLocal(new Vector2(ShopTopBarLayout.HudColumnViewportX, 0f), _halfW, _orthoSize).x;

		float edgeMargin = ShopLayoutConfigSO.V(c => c.worldEdgeMargin, EdgeMarginDefault);
		float exitWidth = ShopLayoutConfigSO.V(c => c.exitButtonWidth, ExitButtonWidthDefault);
		float exitViewportY = ShopLayoutConfigSO.V(c => c.exitButtonViewportY, ShopTopBarLayout.PlayerIconViewportDefault.y);
		float exitOffsetX = ShopLayoutConfigSO.V(c => c.exitButtonOffsetX, 0f);
		float exitFontSize = ShopLayoutConfigSO.V(c => c.exitButtonFontSize, ExitButtonFontSizeDefault);
		float buttonHeight = ShopLayoutConfigSO.V(c => c.buttonHeight, ButtonHeightDefault);
		float optionsWidth = ShopLayoutConfigSO.V(c => c.optionsButtonWidth, OptionsButtonWidthDefault);
		float buttonFontSize = ShopLayoutConfigSO.V(c => c.buttonFontSize, ButtonFontSizeDefault);
		float restShadow = ShopLayoutConfigSO.V(c => c.restShadowUnits, RestShadowUnitsDefault);
		float denyShift = ShopLayoutConfigSO.V(c => c.denyShiftUnits, DenyShiftUnitsDefault);

		ApplyButton(_exitButton, _exitLabel, new Vector3(ExitAnchorX(exitOffsetX, edgeMargin, exitWidth),
			ShopTopBarLayout.ViewportToChromeLocalY(exitViewportY, _orthoSize), 0f),
			exitWidth, buttonHeight, exitFontSize, restShadow, denyShift);
		ApplyButton(_optionsButton, _optionsLabel, new Vector3(_halfW - edgeMargin - optionsWidth * 0.5f, row0Y, 0f),
			optionsWidth, buttonHeight, buttonFontSize, restShadow, denyShift);

		ApplyChip(_moneyChip, ShopTopBarLayout.ViewportToChromeLocal(ShopTopBarLayout.MoneyChipViewport, _halfW, _orthoSize),
			ShopLayoutConfigSO.V(c => c.moneyChipWidth, MoneyChipWidthDefault),
			ShopLayoutConfigSO.V(c => c.moneyChipHeight, MoneyChipHeightDefault),
			ShopLayoutConfigSO.V(c => c.moneyChipFontSize, MoneyChipFontSizeDefault));

		float smallWidth = ShopLayoutConfigSO.V(c => c.smallChipWidth, SmallChipWidthDefault);
		float chipHeight = ShopLayoutConfigSO.V(c => c.chipHeight, ChipHeightDefault);
		float smallFontSize = ShopLayoutConfigSO.V(c => c.smallChipFontSize, SmallChipFontSizeDefault);
		float spacing = ShopLayoutConfigSO.V(c => c.chipSpacing, ChipSpacingDefault);
		// Rows 1-2 (sm dark chips), left-aligned on the HUD column: centers advance by
		// width + gap; the large income chip hangs one extra half-width past hearts.
		ApplyChip(_rarityCommonChip, new Vector2(columnX + smallWidth * 0.5f, row1Y), smallWidth, chipHeight, smallFontSize);
		ApplyChip(_rarityUncommonChip, new Vector2(columnX + smallWidth * 1.5f + spacing, row1Y), smallWidth, chipHeight, smallFontSize);
		ApplyChip(_rarityRareChip, new Vector2(columnX + smallWidth * 2.5f + spacing * 2f, row1Y), smallWidth, chipHeight, smallFontSize);
		ApplyChip(_winsChip, new Vector2(columnX + smallWidth * 0.5f, row2Y), smallWidth, chipHeight, smallFontSize);
		ApplyChip(_heartsChip, new Vector2(columnX + smallWidth * 1.5f + spacing, row2Y), smallWidth, chipHeight, smallFontSize);
		float chipWidth = ShopLayoutConfigSO.V(c => c.chipWidth, ChipWidthDefault);
		float chipFontSize = ShopLayoutConfigSO.V(c => c.chipFontSize, ChipFontSizeDefault);
		ApplyChip(_incomeChip, new Vector2(columnX + smallWidth * 2.5f + spacing * 2f + chipWidth * 0.5f, row2Y), chipWidth, chipHeight, chipFontSize);
	}

	/// <summary>World-local X of the exit button: left-edge anchor plus the config offset.</summary>
	private float ExitAnchorX(float offsetX, float edgeMargin, float width)
	{
		return -_halfW + edgeMargin + width / 2f + offsetX;
	}

	private static void ApplyButton(PhysButton button, TextMeshPro label, Vector3 localPos,
		float width, float height, float fontSize, float restShadow, float denyShift)
	{
		if (button == null) return;
		button.transform.localPosition = localPos;
		button.ConfigureWorldFaceSize(new Vector2(width, height), Vector2.zero);
		button.restShadow = restShadow;
		button.hoverLift = restShadow;
		button.denyShift = denyShift;
		if (label != null)
		{
			label.fontSize = fontSize;
			label.rectTransform.sizeDelta = new Vector2(width - 0.2f, height);
		}
	}

	private static void ApplyChip(HudChip chip, Vector2 localPos, float width, float height, float fontSize)
	{
		if (chip == null) return;
		chip.transform.localPosition = new Vector3(localPos.x, localPos.y, 0.02f);
		chip.ApplyLayout(new Vector2(width, height), fontSize);
	}

	/// <summary>Pulls all shop stats into chips; labels rewrite only on change.</summary>
	public void Refresh()
	{
		ShopManager shop = ShopManager.me;
		if (shop == null) return;

		int purse = shop.purse != null ? shop.purse.value : 0;
		if (purse != _lastPurse)
		{
			_lastPurse = purse;
			if (_moneyChip != null) _moneyChip.SetText("$" + purse);
		}

		int payday = shop.GetCurrentPayday();
		if (payday != _lastPayday)
		{
			_lastPayday = payday;
			if (_incomeChip != null) _incomeChip.SetText("+$" + payday + "/ROUND");
		}

		shop.GetRarityOddsPercents(out float commonPct, out float uncommonPct, out float rarePct);
		if ((int)commonPct != _lastCommonOdds)
		{
			_lastCommonOdds = (int)commonPct;
			if (_rarityCommonChip != null) _rarityCommonChip.SetText("✦ " + (int)commonPct + "%");
		}
		if ((int)uncommonPct != _lastUncommonOdds)
		{
			_lastUncommonOdds = (int)uncommonPct;
			if (_rarityUncommonChip != null) _rarityUncommonChip.SetText("✦✦ " + (int)uncommonPct + "%");
		}
		if ((int)rarePct != _lastRareOdds)
		{
			_lastRareOdds = (int)rarePct;
			if (_rarityRareChip != null) _rarityRareChip.SetText("✦✦✦ " + (int)rarePct + "%");
		}

		if (_phaseManager != null && _phaseManager.wins != null && _phaseManager.winCon != null
			&& _phaseManager.hearts != null && _phaseManager.heartMax != null)
		{
			if (_phaseManager.wins.value != _lastWins || _phaseManager.winCon.value != _lastWinCon)
			{
				_lastWins = _phaseManager.wins.value;
				_lastWinCon = _phaseManager.winCon.value;
				if (_winsChip != null) _winsChip.SetText("🜲 " + _lastWins + "/" + _lastWinCon);
			}
			if (_phaseManager.hearts.value != _lastHearts || _phaseManager.heartMax.value != _lastHeartMax)
			{
				_lastHearts = _phaseManager.hearts.value;
				_lastHeartMax = _phaseManager.heartMax.value;
				if (_heartsChip != null) _heartsChip.SetText("♥ " + _lastHearts + "/" + _lastHeartMax);
			}
		}

		// World avatar + HP pill catch-up (HP utility buy/sell moves hpMax mid-visit).
		if (_pageHud != null) _pageHud.Refresh();
	}
}
