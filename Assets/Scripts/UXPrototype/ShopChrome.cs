using System.Collections.Generic;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;

// Shop top chrome (Guidelines 3.6) as WORLD entities: flat read-only chips plus the exit /
// options physical buttons floating in a reserved viewport-top zone (no full-bleed band —
// the units sit directly on the shop background). Chrome v2 (2026-09-18 annotated layout):
// three chip rows (avatar+username / HP / $ | rarity odds x3 | wins / hearts / income).
// The reroll button and the deck slot count live in the Shop/Deck panel headers
// (ShopSectionPanels). Replaces the 2026-09-17 canvas ShopHudBar (plan-world-entity-shop-
// chrome-2026-09-18) — one input pipeline (physics OnMouse), one input gate (ShopInputGate),
// no canvas layer. Runtime-built from ShopUXManager.Start (no scene edit); colors come
// from GameColorPalette statics at build time.
//
// Layout contract: the band occupies the top of the viewport. Card layout must keep the
// shelf below BandBottomWorldY; ShopUXManager asserts it once per shelf build.
public class ShopChrome : MonoBehaviour
{
	private const string ChromeName = "Shop Chrome";

	public const float BandHeight = 2.6f;      // reserved clearance zone at the viewport top (3 HUD rows, no rendered band)
	public const float CameraForwardOffset = 2f; // chrome z distance in front of the camera
	public const float BandInsetFromTop = 0.5f; // band center below the viewport top edge (ShopChromeAnchor)

	private const float EdgeMargin = 0.35f;
	private const float ChipSpacing = 0.25f;
	private const float ChipWidth = 2.2f;
	private const float ChipHeight = 0.5f;
	private const float SmallChipWidth = 1.9f;  // rarity / wins / hearts chips (demo chip-sm)
	private const float SmallChipFontSize = 1.9f;
	private const float AvatarSquareSize = 0.42f;
	private const float RowPitch = 0.72f;       // vertical distance between HUD chip rows
	private const float ButtonWidth = 2.0f;
	private const float ButtonHeight = 0.56f;
	// World-TMP calibration: the card price print renders ~0.31u tall at fontSize 12 on a
	// 0.2-scaled transform -> ~0.13u per fontSize point at scale 1. Button labels match the
	// price-button text height; chips are a notch smaller.
	private const float ButtonFontSize = 2.4f;
	private const float ChipFontSize = 2.2f;
	private const float RestShadowUnits = 0.05f; // demo rs 4px at chrome scale (tune at play look)
	private const float DenyShiftUnits = 0.075f;

	private static ShopChrome _instance;
	public static ShopChrome Instance => _instance;

	private TMP_FontAsset _font;
	private Sprite _sprite;

	private HudChip _moneyChip;
	private HudChip _incomeChip;
	private HudChip _hpChip;
	private HudChip _rarityCommonChip;
	private HudChip _rarityUncommonChip;
	private HudChip _rarityRareChip;
	private HudChip _winsChip;
	private HudChip _heartsChip;
	private HudChip _avatarChip;
	private TMP_Text _avatarLabel;
	private PhysButton _optionsButton;
	private PhysButton _rerollButton;     // DEPRECATED 2026-09-18: button moves to the Shop panel
	private TextMeshPro _rerollLabel;     // header (ShopSectionPanels, Task 4) — kept until then.
	private PhysButton _exitButton;
	private PhaseManager _phaseManager;

	private int _lastPurse = int.MinValue;
	private int _lastPayday = int.MinValue;
	private int _lastHp = int.MinValue;
	private int _lastHpMax = int.MinValue;
	private int _lastCommonOdds = int.MinValue;
	private int _lastUncommonOdds = int.MinValue;
	private int _lastRareOdds = int.MinValue;
	private int _lastWins = int.MinValue;
	private int _lastWinCon = int.MinValue;
	private int _lastHearts = int.MinValue;
	private int _lastHeartMax = int.MinValue;
	private string _lastUsername;
	private string _lastRerollLabel;
	private bool _lastRerollDisabled;
	private bool _rerollRolling;

	/// <summary>Bottom edge of the chrome band in world Y — the shelf layout contract limit.</summary>
	public static float BandBottomWorldY()
	{
		Camera cam = Camera.main;
		return cam != null ? cam.transform.position.y + cam.orthographicSize - BandHeight : float.MaxValue;
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
		root.AddComponent<ShopChromeAnchor>();
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
		_instance.gameObject.SetActive(true);
		_instance.Refresh();
	}

	public static void HideIfActive()
	{
		if (_instance != null) _instance.gameObject.SetActive(false);
	}

	/// <summary>
	/// Reroll animation guard. DEPRECATED 2026-09-18: the reroll button moves into the Shop
	/// panel header (ShopSectionPanels, plan-shop-panels-port-2026-09-18 Task 4); until that
	/// task lands this remains a null-guarded no-op (Build no longer creates the button).
	/// </summary>
	public void SetRerollRolling(bool rolling)
	{
		_rerollRolling = rolling;
		RefreshRerollState();
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

		// Chrome v2 (2026-09-18 annotated layout): three HUD chip rows float under the
		// viewport top — [avatar | HP | $] / [rarity odds x3] / [wins | hearts | income] —
		// Exit (left) and the options placeholder (right) on the top row. The reroll button
		// and the deck slot count moved into the Shop/Deck panel headers (ShopSectionPanels,
		// plan-shop-panels-port-2026-09-18). No full-bleed band: units float directly on the
		// shop background; BandHeight lives on as the reserved clearance zone only.
		float leftX = -halfW + EdgeMargin + ButtonWidth / 2f;
		float rightX = halfW - EdgeMargin - ButtonWidth / 2f;
		_exitButton = CreateButton("ExitButton", "Exit", leftX, 0f, out _);
		_optionsButton = CreateButton("OptionsButton", "||", rightX, 0f, out _);
		_optionsButton.SetWorldAction(() => Debug.Log("[ShopChrome] Options pressed (placeholder — no options menu yet)"));
		_phaseManager = FindObjectOfType<PhaseManager>();
		if (_phaseManager == null)
		{
			TestManager.LogWarning("[ShopChrome] PhaseManager not found in scene — exit button inert");
		}
		_exitButton.SetWorldAction(() =>
		{
			if (_phaseManager == null) return;
			_phaseManager.ExitingShopPhase();
			_phaseManager.EnteringCombatPhase();
		});

		// Avatar + username chip (demo topbar "sq + 用户名"), same identity source as the
		// combat player name label (CombatIconPresenter -> PlayerIdentity.Username).
		_avatarChip = CreateChip("ChipAvatar", leftX, -RowPitch, ChipWidth, ChipFontSize, false, false);
		_avatarLabel = _avatarChip != null ? _avatarChip.GetComponentInChildren<TMP_Text>() : null;
		GameObject avatarSquare = new GameObject("AvatarSquare", typeof(SpriteRenderer));
		avatarSquare.transform.SetParent(_avatarChip.transform, false);
		avatarSquare.transform.localPosition = new Vector3(-ChipWidth / 2f + AvatarSquareSize / 2f + 0.1f, 0f, -0.01f);
		SpriteRenderer squareSr = avatarSquare.GetComponent<SpriteRenderer>();
		SetupSliced(squareSr, GameColorPalette.ShopPanelBgColor);
		squareSr.size = new Vector2(AvatarSquareSize, AvatarSquareSize);
		if (_avatarLabel != null)
		{
			// Shift the name right of the square; overflow is fine (world TMP extends).
			_avatarLabel.rectTransform.localPosition = new Vector3(AvatarSquareSize / 2f + 0.1f, 0f, 0f);
		}

		// Row 1 (lg chips, centered as a group): HP + money.
		_hpChip = CreateChip("ChipHP", -(ChipWidth + ChipSpacing) / 2f, 0f, ChipWidth, ChipFontSize, false, false);
		_moneyChip = CreateChip("ChipMoney", (ChipWidth + ChipSpacing) / 2f, 0f, ChipWidth, ChipFontSize, true, false);

		// Row 2 (sm dark chips): rarity odds from the active session weight table.
		float rarityPitch = SmallChipWidth + ChipSpacing;
		_rarityCommonChip = CreateChip("ChipRarityCommon", -rarityPitch, -RowPitch, SmallChipWidth, SmallChipFontSize, false, true);
		_rarityUncommonChip = CreateChip("ChipRarityUncommon", 0f, -RowPitch, SmallChipWidth, SmallChipFontSize, false, true);
		_rarityRareChip = CreateChip("ChipRarityRare", rarityPitch, -RowPitch, SmallChipWidth, SmallChipFontSize, false, true);

		// Row 3 (sm dark + lg): wins / hearts / payday. Full-word labels (Wins/Hearts)
		// because the ▦/♥ glyphs are outside every bundled static font atlas (✦ only, see
		// CardPhysObjScript) and would render as tofu.
		float winsHeartsPitch = SmallChipWidth + ChipSpacing;
		float row3Width = winsHeartsPitch + SmallChipWidth + ChipSpacing + ChipWidth;
		float row3Start = -row3Width / 2f;
		_winsChip = CreateChip("ChipWins", row3Start + SmallChipWidth / 2f, -2f * RowPitch, SmallChipWidth, SmallChipFontSize, false, true);
		_heartsChip = CreateChip("ChipHearts", row3Start + winsHeartsPitch + SmallChipWidth / 2f, -2f * RowPitch, SmallChipWidth, SmallChipFontSize, false, true);
		_incomeChip = CreateChip("ChipIncome", row3Start + winsHeartsPitch + SmallChipWidth + ChipSpacing + ChipWidth / 2f, -2f * RowPitch, ChipWidth, ChipFontSize, false, false);
	}

	private PhysButton CreateButton(string name, string label, float x, float y, out TextMeshPro labelTmp)
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

		labelTmp = CreateLabel(visualGo.transform, label, ButtonFontSize, GameColorPalette.OwnerTextColor,
			new Vector2(ButtonWidth - 0.2f, ButtonHeight));

		PhysButton button = rootGo.AddComponent<PhysButton>();
		button.SetShadowTransform(shadowGo.transform);
		button.SetWorldFace(faceGo.GetComponent<SpriteRenderer>());
		button.SetVisualGroup(visualGo.transform);
		button.restShadow = RestShadowUnits;
		button.hoverLift = RestShadowUnits;
		button.denyShift = DenyShiftUnits;
		button.label = labelTmp;
		// Label rect is centered on the Visual origin, so the face centers there too.
		button.ConfigureWorldFaceSize(new Vector2(ButtonWidth, ButtonHeight), Vector2.zero);
		return button;
	}

	private HudChip CreateChip(string name, float x, float y, float width, float fontSize, bool accent, bool darkPanel)
	{
		GameObject chipGo = new GameObject(name);
		chipGo.transform.SetParent(transform, false);
		chipGo.transform.localPosition = new Vector3(x, y, 0.02f);
		SpriteRenderer panel = chipGo.AddComponent<SpriteRenderer>();
		SetupSliced(panel, darkPanel ? GameColorPalette.ShopPanelBgColor : GameColorPalette.TooltipBgColor);
		panel.size = new Vector2(width, ChipHeight);
		TextMeshPro label = CreateLabel(chipGo.transform, string.Empty, fontSize,
			accent ? GameColorPalette.HighlightColor : GameColorPalette.TooltipTextColor,
			new Vector2(width - 0.15f, ChipHeight));
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

	/// <summary>Pulls all shop stats into chips + reroll state; labels rewrite only on change.</summary>
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
			if (_incomeChip != null) _incomeChip.SetText("+$" + payday + "/combat");
		}

		PlayerStatusSO status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
		int hp = status != null ? status.hp : 0;
		int hpMax = status != null ? status.hpMax : 0;
		if (hp != _lastHp || hpMax != _lastHpMax)
		{
			_lastHp = hp;
			_lastHpMax = hpMax;
			if (_hpChip != null) _hpChip.SetText(hp + "/" + hpMax);
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
				if (_winsChip != null) _winsChip.SetText("Wins " + _lastWins + "/" + _lastWinCon);
			}
			if (_phaseManager.hearts.value != _lastHearts || _phaseManager.heartMax.value != _lastHeartMax)
			{
				_lastHearts = _phaseManager.hearts.value;
				_lastHeartMax = _phaseManager.heartMax.value;
				if (_heartsChip != null) _heartsChip.SetText("Hearts " + _lastHearts + "/" + _lastHeartMax);
			}
		}

		string username = PlayerIdentity.Username;
		if (string.IsNullOrEmpty(username)) username = "???";
		if (username != _lastUsername)
		{
			_lastUsername = username;
			if (_avatarLabel != null) _avatarLabel.text = username;
		}

		RefreshRerollState();
	}

	private void RefreshRerollState()
	{
		ShopManager shop = ShopManager.me;
		if (shop == null || _rerollButton == null) return;

		int freeLeft = shop.FreeRerollsLeft;
		string label = freeLeft > 0 ? "Reroll: $0" : "Reroll: $" + shop.RerollPrice;
		bool disabled = _rerollRolling || (freeLeft <= 0 && shop.purse != null && shop.purse.value < shop.RerollPrice);

		if (label != _lastRerollLabel)
		{
			_lastRerollLabel = label;
			_rerollLabel.text = label;
		}
		if (disabled != _lastRerollDisabled)
		{
			_lastRerollDisabled = disabled;
			_rerollButton.SetDisabled(disabled);
			_rerollLabel.color = disabled ? GameColorPalette.CardTextSoftColor : GameColorPalette.OwnerTextColor;
		}
	}
}
