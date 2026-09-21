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
// Layout contract: the band occupies the top of the page (= the viewport at scroll 0).
// Card layout must keep the shelf below BandBottomWorldY; ShopUXManager asserts it once
// per shelf build.
public class ShopChrome : MonoBehaviour
{
	private const string ChromeName = "Shop Chrome";

	public const float BandHeight = 2.6f;      // reserved clearance zone at the page top (2 HUD rows, no rendered band)
	public const float CameraForwardOffset = 2f; // chrome z distance in front of the camera
	public const float BandInsetFromTop = 0.5f; // band center below the viewport top edge (Build-time root placement)

	private const float EdgeMargin = 0.35f;
	private const float ChipSpacing = 0.25f;
	private const float ChipWidth = 2.2f;
	private const float ChipHeight = 0.5f;
	private const float SmallChipWidth = 1.7f;  // rarity / wins / hearts chips (demo chip-sm)
	private const float SmallChipFontSize = 1.9f;
	private const float MoneyChipWidth = 2.4f;  // demo chip-lg
	private const float MoneyChipHeight = 0.62f;
	private const float MoneyChipFontSize = 2.8f;
	private const float OptionsButtonWidth = 0.72f;
	private const float ButtonHeight = 0.64f;
	private const float ExitButtonWidth = 2.6f;
	// World-TMP calibration: the card price print renders ~0.31u tall at fontSize 12 on a
	// 0.2-scaled transform -> ~0.13u per fontSize point at scale 1.
	private const float ChipFontSize = 2.2f;
	private const float ButtonFontSize = 2.4f;
	private const float ExitButtonFontSize = 2.8f;
	private const float RestShadowUnits = 0.05f; // demo rs 4px at chrome scale (tune at play look)
	private const float DenyShiftUnits = 0.075f;

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
			float cardTop = phys.TargetPosition.y + 2.5f * phys.TargetScale.y; // face half-height + price button allowance
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
		float halfW = cam != null ? cam.orthographicSize * cam.aspect : 8f;

		// One-time root placement (was the ShopChromeAnchor LateUpdate copy): the same spot
		// the anchor wrote at scroll 0, so every child local coordinate below lands
		// pixel-identical — and the whole bar now travels with the page when the rig scrolls
		// (plan-shop-topbar-world-scroll §3.1). Bootstrap runs from ShopUXManager.Start with
		// the rig at shop base Y, so the captured band limit matches the shelf math.
		if (cam != null)
		{
			transform.position = new Vector3(cam.transform.position.x,
				cam.transform.position.y + cam.orthographicSize - BandInsetFromTop,
				cam.transform.position.z + CameraForwardOffset);
			_bandBottomWorldY = cam.transform.position.y + cam.orthographicSize - BandHeight;
		}
		else
		{
			TestManager.LogWarning("[ShopChrome] Camera.main missing at Build — chrome pinned at origin");
		}

		// 2026-09-19 §08 order: row 0 = [离开商店] [avatar | HP | $ (reused combat canvas
		// pieces + this money chip)] ... [❚❚]; row 1 = rarity odds; row 2 = wins / hearts /
		// income. Rows 1-2 are left-aligned on the HUD column the avatar block starts, so
		// their anchor X comes from the shared ShopTopBarLayout viewport, not from halfW.
		float row0Y = ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.PlayerIconViewport.y, cam);
		float row1Y = ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.OddsRowViewportY, cam);
		float row2Y = ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.StatsRowViewportY, cam);
		float columnX = ShopTopBarLayout.ViewportToChromeLocal(new Vector2(ShopTopBarLayout.HudColumnViewportX, 0f), cam).x;

		float exitX = -halfW + EdgeMargin + ExitButtonWidth / 2f;
		float optionsX = halfW - EdgeMargin - OptionsButtonWidth / 2f;
		_exitButton = CreateButton("ExitButton", "离开商店", exitX, row0Y, ExitButtonWidth, ExitButtonFontSize, out _);
		_optionsButton = CreateButton("OptionsButton", "❚❚", optionsX, row0Y, OptionsButtonWidth, ButtonFontSize, out _);
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

		// Money chip: row 0, right of the reused combat HP display.
		Vector2 moneyLocal = ShopTopBarLayout.ViewportToChromeLocal(ShopTopBarLayout.MoneyChipViewport, cam);
		_moneyChip = CreateChip("ChipMoney", moneyLocal.x, moneyLocal.y, MoneyChipWidth, MoneyChipHeight, MoneyChipFontSize, true, false);

		// Row 1 (sm dark chips): rarity odds from the active session weight table,
		// left-aligned on the HUD column.
		float x = columnX + SmallChipWidth * 0.5f;
		_rarityCommonChip = CreateChip("ChipRarityCommon", x, row1Y, SmallChipWidth, ChipHeight, SmallChipFontSize, false, true);
		x += SmallChipWidth + ChipSpacing;
		_rarityUncommonChip = CreateChip("ChipRarityUncommon", x, row1Y, SmallChipWidth, ChipHeight, SmallChipFontSize, false, true);
		x += SmallChipWidth + ChipSpacing;
		_rarityRareChip = CreateChip("ChipRarityRare", x, row1Y, SmallChipWidth, ChipHeight, SmallChipFontSize, false, true);

		// Row 2: wins / hearts / payday, left-aligned on the same column (demo hud-line 3).
		x = columnX + SmallChipWidth * 0.5f;
		_winsChip = CreateChip("ChipWins", x, row2Y, SmallChipWidth, ChipHeight, SmallChipFontSize, false, true);
		x += SmallChipWidth + ChipSpacing;
		_heartsChip = CreateChip("ChipHearts", x, row2Y, SmallChipWidth, ChipHeight, SmallChipFontSize, false, true);
		x += SmallChipWidth + ChipSpacing + ChipWidth * 0.5f;
		_incomeChip = CreateChip("ChipIncome", x, row2Y, ChipWidth, ChipHeight, ChipFontSize, false, false);

		// World avatar + HP pill (plan-shop-topbar-world-scroll §3.2): parented under this
		// root, so show/hide and wheel scroll follow the chrome for free.
		_pageHud = ShopPageHud.Bootstrap(transform);
	}

	private PhysButton CreateButton(string name, string label, float x, float y, float width, float fontSize, out TextMeshPro labelTmp)
	{
		GameObject rootGo = new GameObject(name, typeof(BoxCollider2D));
		rootGo.transform.SetParent(transform, false);
		rootGo.transform.localPosition = new Vector3(x, y, 0f);

		GameObject visualGo = new GameObject("Visual");
		visualGo.transform.SetParent(rootGo.transform, false);

		GameObject shadowGo = new GameObject("Shadow", typeof(SpriteRenderer));
		shadowGo.transform.SetParent(rootGo.transform, false);
		shadowGo.transform.localPosition = new Vector3(0f, 0f, 0.04f);
		SetupSliced(shadowGo.GetComponent<SpriteRenderer>(), GameColorPalette.CardShadowColor);

		GameObject faceGo = new GameObject("Face", typeof(SpriteRenderer));
		faceGo.transform.SetParent(visualGo.transform, false);
		faceGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
		SetupSliced(faceGo.GetComponent<SpriteRenderer>(), GameColorPalette.OwnerCardColor);

		labelTmp = CreateLabel(visualGo.transform, label, fontSize, GameColorPalette.OwnerTextColor,
			new Vector2(width - 0.2f, ButtonHeight));

		PhysButton button = rootGo.AddComponent<PhysButton>();
		button.SetShadowTransform(shadowGo.transform);
		button.SetWorldFace(faceGo.GetComponent<SpriteRenderer>());
		button.SetVisualGroup(visualGo.transform);
		button.restShadow = RestShadowUnits;
		button.hoverLift = RestShadowUnits;
		button.denyShift = DenyShiftUnits;
		button.label = labelTmp;
		// Label rect is centered on the Visual origin, so the face centers there too.
		button.ConfigureWorldFaceSize(new Vector2(width, ButtonHeight), Vector2.zero);
		return button;
	}

	private HudChip CreateChip(string name, float x, float y, float width, float height, float fontSize, bool accent, bool darkPanel)
	{
		GameObject chipGo = new GameObject(name);
		chipGo.transform.SetParent(transform, false);
		chipGo.transform.localPosition = new Vector3(x, y, 0.02f);
		SpriteRenderer panel = chipGo.AddComponent<SpriteRenderer>();
		SetupSliced(panel, darkPanel ? GameColorPalette.ShopPanelBgColor : GameColorPalette.TooltipBgColor);
		panel.size = new Vector2(width, height);
		TextMeshPro label = CreateLabel(chipGo.transform, string.Empty, fontSize,
			accent ? GameColorPalette.HighlightColor : GameColorPalette.TooltipTextColor,
			new Vector2(width - 0.15f, height));
		HudChip chip = chipGo.AddComponent<HudChip>();
		chip.Bind(panel, label);
		return chip;
	}

	private TextMeshPro CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 size)
	{
		GameObject go = new GameObject("Label");
		go.transform.SetParent(parent, false);
		TextMeshPro tmp = go.AddComponent<TextMeshPro>();
		tmp.font = _font;
		tmp.fontSharedMaterial = _font.material;
		tmp.fontSize = fontSize;
		tmp.color = color;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.enableWordWrapping = false;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.rectTransform.sizeDelta = size;
		tmp.text = text;
		return tmp;
	}

	private void SetupSliced(SpriteRenderer sr, Color color)
	{
		sr.sprite = _sprite;
		sr.drawMode = SpriteDrawMode.Sliced;
		sr.color = color;
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
