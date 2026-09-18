using System.Collections.Generic;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;

// Shop top chrome (Guidelines 3.6) as WORLD entities: flat read-only chips plus reroll/exit
// physical buttons floating in a reserved viewport-top zone (no full-bleed band — the demo
// and the 2026-09-17 canvas bar both put the units directly on the shop background). Replaces the 2026-09-17 canvas
// ShopHudBar and the uGUI reroll/exit buttons (plan-world-entity-shop-chrome-2026-09-18) —
// one input pipeline (physics OnMouse), one input gate (ShopInputGate), no canvas layer
// rendering above the world. Runtime-built from ShopUXManager.Start (no scene edit);
// colors come from GameColorPalette statics at build time.
//
// Layout contract: the band occupies the top of the viewport. Card layout must keep the
// shelf below BandBottomWorldY; ShopUXManager asserts it once per shelf build.
public class ShopChrome : MonoBehaviour
{
	private const string ChromeName = "Shop Chrome";

	public const float BandHeight = 1.0f;      // reserved clearance zone at the viewport top (no rendered band)
	public const float CameraForwardOffset = 2f; // chrome z distance in front of the camera
	public const float BandInsetFromTop = 0.5f; // band center below the viewport top edge (ShopChromeAnchor)

	private const float EdgeMargin = 0.35f;
	private const float ChipSpacing = 0.25f;
	private const float ChipWidth = 2.2f;
	private const float ChipHeight = 0.5f;
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
	private HudChip _deckChip;
	private PhysButton _rerollButton;
	private TextMeshPro _rerollLabel;
	private PhysButton _exitButton;

	private int _lastPurse = int.MinValue;
	private int _lastPayday = int.MinValue;
	private int _lastHp = int.MinValue;
	private int _lastHpMax = int.MinValue;
	private int _lastDeck = int.MinValue;
	private int _lastDeckMax = int.MinValue;
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

	/// <summary>Reroll animation guard: the button denies input and the gate covers the rest of the shop.</summary>
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

		// No full-bleed band: per Guidelines 3.6 / the demo the HUD units float directly on
		// the shop background (a navy band behind navy chips made both vanish). BandHeight
		// lives on as the reserved clearance zone only (see ShopChromeAnchor / CheckShelfClearance).

		// Exit (left end) + reroll (right end), per Guidelines 3.6 order.
		// Exit = the old canvas button's exact wiring: PhaseManager.ExitingShopPhase +
		// EnteringCombatPhase (ShopManager.ExitShop only cleans up and is triggered by the
		// onEnterCombatPhase event — it never changes the phase itself).
		float leftX = -halfW + EdgeMargin + ButtonWidth / 2f;
		float rightX = halfW - EdgeMargin - ButtonWidth / 2f;
		_exitButton = CreateButton("ExitButton", "Exit", leftX, out _);
		_rerollButton = CreateButton("RerollButton", "Reroll: $0", rightX, out _rerollLabel);
		_rerollButton.SetWorldAction(() => { if (ShopManager.me != null) ShopManager.me.Reroll(); });
		PhaseManager phaseManager = FindObjectOfType<PhaseManager>();
		if (phaseManager == null)
		{
			TestManager.LogWarning("[ShopChrome] PhaseManager not found in scene — exit button inert");
		}
		_exitButton.SetWorldAction(() =>
		{
			if (phaseManager == null) return;
			phaseManager.ExitingShopPhase();
			phaseManager.EnteringCombatPhase();
		});

		// Chips between the buttons, laid out left to right (no layout group in world space)
		float chipX = leftX + ButtonWidth / 2f + ChipSpacing + ChipWidth / 2f;
		_moneyChip = CreateChip("ChipMoney", chipX, "$0", true);
		chipX += ChipWidth + ChipSpacing;
		_incomeChip = CreateChip("ChipIncome", chipX, "+$0/combat", false);
		chipX += ChipWidth + ChipSpacing;
		_hpChip = CreateChip("ChipHP", chipX, "0/0", false);
		chipX += ChipWidth + ChipSpacing;
		_deckChip = CreateChip("ChipDeck", chipX, "0/0", false);
	}

	private PhysButton CreateButton(string name, string label, float x, out TextMeshPro labelTmp)
	{
		GameObject rootGo = new GameObject(name, typeof(BoxCollider2D));
		rootGo.transform.SetParent(transform, false);
		rootGo.transform.localPosition = new Vector3(x, 0f, 0f);

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

	private HudChip CreateChip(string name, float x, string initialText, bool accent)
	{
		GameObject chipGo = new GameObject(name);
		chipGo.transform.SetParent(transform, false);
		chipGo.transform.localPosition = new Vector3(x, 0f, 0.02f);
		SpriteRenderer panel = chipGo.AddComponent<SpriteRenderer>();
		SetupSliced(panel, GameColorPalette.TooltipBgColor);
		panel.size = new Vector2(ChipWidth, ChipHeight);
		TextMeshPro label = CreateLabel(chipGo.transform, initialText, ChipFontSize,
			accent ? GameColorPalette.HighlightColor : GameColorPalette.TooltipTextColor,
			new Vector2(ChipWidth - 0.15f, ChipHeight));
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

		int deck = shop.playerDeckRef != null && shop.playerDeckRef.deck != null ? shop.playerDeckRef.deck.Count : 0;
		int maxDeck = shop.deckSize != null ? shop.deckSize.value : 0;
		if (deck != _lastDeck || maxDeck != _lastDeckMax)
		{
			_lastDeck = deck;
			_lastDeckMax = maxDeck;
			if (_deckChip != null) _deckChip.SetText(deck + "/" + maxDeck);
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
