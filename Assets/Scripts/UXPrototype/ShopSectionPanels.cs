using System.Collections.Generic;
using DefaultNamespace.Managers;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-built world-space section panels for the shop page (UIKitDemo 08 port,
/// plan-shop-panels-port-2026-09-18): translucent dark rounded rectangles behind the
/// Shop / Deck / Upgrades rows, with header labels, the Deck slot counter (03/05) and
/// the reroll button — relocated from the chrome band into the Shop panel header per
/// the 2026-09-18 annotated layout. Built once by Bootstrap (mirrors ShopChrome);
/// refitted by RefreshLayout, which ShopUXManager calls after every relayout. 2026-09-22:
/// appearance values are live-tunable via ShopUXManager.panels
/// (plan-shop-sectionpanels-live-tuning-2026-09-22) — resolved at RefreshLayout time,
/// never snapshotted. Panels
/// carry no colliders and never intercept physics input. Pure helpers (bounds, slot
/// format) are static for EditMode coverage. 2026-09-23: widget recipes (panel bg /
/// header labels / reroll button) build through ShopWorldWidgets — this file keeps fit
/// + refresh logic only (plan-shop-layout-config-widget-factory).
/// </summary>
public class ShopSectionPanels : MonoBehaviour
{
	private const string RootName = "Shop Section Panels";

	private const float PanelZ = 0.5f;   // behind cards (z 0) and empty slots (z 0.1)
	private const float HeaderZ = 0.4f;  // in front of the panel background

	// Fallback defaults for the live tuning on ShopUXManager.panels
	// (plan-shop-sectionpanels-live-tuning-2026-09-22): every layout/style read resolves
	// ShopUXManager.Instance.panels at point of use (Tuning helper below) — never
	// snapshotted — so a Play-mode Inspector edit applies on the next RefreshLayout. The
	// consts only cover the panels-exist-without-manager corner (tests / headless).
	private const float HeaderHeightDefault = 1.2f;
	private const float SidePaddingDefault = 0.55f;
	private const float TopPaddingDefault = 0.35f;
	private const float BottomPaddingDefault = 0.5f;
	private const float FitTweenDurationDefault = 0.3f;
	private const float HeaderFontSizeDefault = 3.2f;
	private const float HeaderLeftMarginDefault = 0.25f;
	private const float HeaderRightMarginDefault = 0.25f;
	private const float ButtonWidthDefault = 2.0f;
	private const float ButtonHeightDefault = 0.56f;
	private const float ButtonFontSizeDefault = 2.4f;
	private const float RestShadowUnitsDefault = 0.05f;
	private const float DenyShiftUnitsDefault = 0.075f;

	// Card face half-extents at scale 1, from the EmptyCardSpace bake (3.3 x 4.7 units).
	// The below factor includes the price-button allowance; it is THE shared source —
	// ShopChrome.CheckShelfClearance reads it too (2026-09-23, ex-hardcoded 2.5f).
	public const float FaceHalfWidthFactor = 1.65f;
	public const float FaceAboveHalfHeightFactor = 2.35f;
	public const float FaceBelowHalfHeightFactor = 2.5f;

	private static ShopSectionPanels _instance;
	public static ShopSectionPanels Instance => _instance;

	private Sprite _sprite;
	private TMP_FontAsset _font;

	private SpriteRenderer _shopPanel;
	private SpriteRenderer _deckPanel;
	private SpriteRenderer _upgradesPanel;
	private TextMeshPro _shopHeader;
	private TextMeshPro _deckHeader;
	private TextMeshPro _upgradesHeader;
	private TextMeshPro _deckCounter;
	private PhysButton _rerollButton;
	private TMP_Text _rerollLabel;
	private bool _rerollRolling;
	private string _lastRerollLabel;
	private bool _lastRerollDisabled;
	private int _lastCounterUsed = int.MinValue;
	private int _lastCounterTotal = int.MinValue;

	/// <summary>
	/// Builds the panels once (idempotent) as a scene-root object; shows them when the
	/// game is already in Shop phase (same bootstrap-timing guard as ShopChrome).
	/// </summary>
	public static void Bootstrap(Sprite sprite, TMP_FontAsset font)
	{
		if (_instance != null) return;
		if (sprite == null || font == null)
		{
			TestManager.LogWarning("[ShopSectionPanels] sprite/font missing — shop section panels skipped");
			return;
		}

		GameObject root = new GameObject(RootName);
		root.SetActive(false);
		_instance = root.AddComponent<ShopSectionPanels>();
		_instance._sprite = sprite;
		_instance._font = font;
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

	/// <summary>
	/// Live tuning entry (plan-shop-sectionpanels-live-tuning-2026-09-22 §3.5): re-fit all
	/// three panels from the current ShopUXManager.panels values. No-op before Bootstrap;
	/// callers skip it mid-travel (IsTransitioning) — landing ShowIfActive re-fits anyway.
	/// </summary>
	public static void RefitIfBuilt()
	{
		if (_instance != null) _instance.RefreshLayout();
	}

	// Point-of-use resolver for the shared tuning: reads the field off ShopUXManager.panels
	// at call time so retuned values never go stale; falls back to the *Default consts when
	// the manager is absent (tests / headless construction).
	private static float Tuning(System.Func<ShopUXManager.PanelsTuning, float> selector, float fallback)
	{
		ShopUXManager ux = ShopUXManager.Instance;
		return ux != null && ux.panels != null ? selector(ux.panels) : fallback;
	}

	// Point-of-use resolver for the font-bold toggle (PanelsTuning.fontBold).
	private static bool TuningBold()
	{
		ShopUXManager ux = ShopUXManager.Instance;
		return ux != null && ux.panels != null && ux.panels.fontBold;
	}

	public static void ShowIfActive()
	{
		if (_instance == null) return;
		_instance.gameObject.SetActive(true);
		_instance.RefreshLayout();
		_instance.Refresh();
	}

	public static void HideIfActive()
	{
		if (_instance != null) _instance.gameObject.SetActive(false);
	}

	/// <summary>Reroll animation guard: the button denies input while the shelf rebuilds.</summary>
	public static void SetRerollRolling(bool rolling)
	{
		if (_instance == null) return;
		_instance._rerollRolling = rolling;
		_instance.RefreshRerollState();
	}

	/// <summary>Re-fit all three panels around the current content (call after every relayout).</summary>
	public void RefreshLayout()
	{
		ApplySharedStyle();
		ShopUXManager ux = ShopUXManager.Instance;
		float scale = ux != null ? ux.physCardSize.x : 0.8f;
		float halfW = FaceHalfWidthFactor * scale;
		float aboveH = FaceAboveHalfHeightFactor * scale;
		float belowH = FaceBelowHalfHeightFactor * scale;

		FitPanel(_shopPanel, _shopHeader, null, TargetsOf(ux != null ? ux.SpawnedShopCards : null), halfW, aboveH, belowH, reroll: true);
		List<Vector3> deckCenters = TargetsOf(ux != null ? ux.SpawnedPlayerCards : null);
		deckCenters.AddRange(TargetsOf(ux != null ? ux.SpawnedEmptySlots : null));
		FitPanel(_deckPanel, _deckHeader, _deckCounter, deckCenters, halfW, aboveH, belowH, reroll: false);
		FitPanel(_upgradesPanel, _upgradesHeader, null, TargetsOf(ux != null ? ux.SpawnedUtilityCards : null), halfW, aboveH, belowH, reroll: false);
	}

	// Build-once style values re-applied on every refit so a Play-mode tuning session moves
	// them too (plan §3.3): header font sizes, reroll label font/rect, reroll face+collider
	// size and its press/hover/deny params. Panel geometry re-derives from the paddings
	// inside FitPanel on every call by itself.
	private void ApplySharedStyle()
	{
		float headerFontSize = Tuning(t => t.headerFontSize, HeaderFontSizeDefault);
		// FontStyles.Bold is TMP synthetic bold (vertex dilation) — emboldens glyphs served
		// by the CJK fallback chain too, no Bold font asset needed (probe-verified 2026-09-25).
		FontStyles fontStyle = TuningBold() ? FontStyles.Bold : FontStyles.Normal;
		_shopHeader.fontSize = headerFontSize;
		_shopHeader.fontStyle = fontStyle;
		_deckHeader.fontSize = headerFontSize;
		_deckHeader.fontStyle = fontStyle;
		_upgradesHeader.fontSize = headerFontSize;
		_upgradesHeader.fontStyle = fontStyle;
		if (_deckCounter != null) _deckCounter.fontStyle = fontStyle;

		if (_rerollButton == null) return;
		float buttonWidth = Tuning(t => t.buttonWidth, ButtonWidthDefault);
		float buttonHeight = Tuning(t => t.buttonHeight, ButtonHeightDefault);
		_rerollButton.ConfigureWorldFaceSize(new Vector2(buttonWidth, buttonHeight), Vector2.zero);
		_rerollButton.restShadow = Tuning(t => t.restShadowUnits, RestShadowUnitsDefault);
		_rerollButton.hoverLift = Tuning(t => t.restShadowUnits, RestShadowUnitsDefault);
		_rerollButton.denyShift = Tuning(t => t.denyShiftUnits, DenyShiftUnitsDefault);
		if (_rerollLabel != null)
		{
			_rerollLabel.fontSize = Tuning(t => t.buttonFontSize, ButtonFontSizeDefault);
			_rerollLabel.fontStyle = fontStyle;
			_rerollLabel.rectTransform.sizeDelta = new Vector2(buttonWidth - 0.2f, buttonHeight);
		}
	}

	/// <summary>Content bounds for a set of card/slot centers: X expanded by halfWidth,
	/// Y expanded by aboveHalfHeight (top, no price button) / belowHalfHeight (bottom incl. price button).</summary>
	public static Bounds ComputeContentBounds(IList<Vector3> centers, float halfWidth, float aboveHalfHeight, float belowHalfHeight)
	{
		if (centers == null || centers.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);
		float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
		foreach (Vector3 c in centers)
		{
			if (c.x < minX) minX = c.x;
			if (c.x > maxX) maxX = c.x;
			if (c.y < minY) minY = c.y;
			if (c.y > maxY) maxY = c.y;
		}
		Vector3 min = new Vector3(minX - halfWidth, minY - belowHalfHeight, 0f);
		Vector3 max = new Vector3(maxX + halfWidth, maxY + aboveHalfHeight, 0f);
		return new Bounds((min + max) * 0.5f, max - min);
	}

	/// <summary>Deck panel slot counter text: two-digit zero-padded "03/05".</summary>
	public static string FormatSlotCount(int used, int total)
	{
		return used.ToString("00") + "/" + total.ToString("00");
	}

	/// <summary>
	/// Deck slots currently occupied, read from the player's deck SO — the authoritative
	/// count (same helper as the BuyFunc deck-full gate). Slot-free utility passives never
	/// count (they render in the Upgrades panel); with the duplicate-share-slot rule on,
	/// copies of one cardTypeID count once. The spawned empty slots cannot be used for
	/// this: they are one recessed frame per GRID slot — always deckSize of them, cards
	/// drawn on top — not a count of the free ones.
	/// </summary>
	public static int CountUsedSlots(DeckSO deck, bool duplicatesShareSlot)
	{
		return deck != null ? UtilityFuncManagerScript.CountSlotOccupyingCards(deck, duplicatesShareSlot) : 0;
	}

	private void OnDestroy()
	{
		if (_instance == this) _instance = null;
	}

	private void Build()
	{
		_shopPanel = CreatePanel("PanelShop");
		_deckPanel = CreatePanel("PanelDeck");
		_upgradesPanel = CreatePanel("PanelUpgrades");
		_shopHeader = CreateHeader("HeaderShop", "商店", TextAlignmentOptions.Left, GameColorPalette.TooltipTextColor);
		_deckHeader = CreateHeader("HeaderDeck", "卡组", TextAlignmentOptions.Left, GameColorPalette.TooltipTextColor);
		_upgradesHeader = CreateHeader("HeaderUpgrades", "商店升级", TextAlignmentOptions.Left, GameColorPalette.TooltipTextColor);
		// VISUAL-FIX(2026-09-22): Deck panel counter (03/05) rendered yellow — it read the
		//   log-highlight token (#FFEB04) while the UIKitDemo §08 slot-count is white
		//   Regress: counter matches the white panel headers; LogHighlight.asset stays for combat logs
		_deckCounter = CreateHeader("DeckCounter", string.Empty, TextAlignmentOptions.Right, GameColorPalette.TooltipTextColor);
		_rerollButton = CreateRerollButton();
	}

	private SpriteRenderer CreatePanel(string name)
	{
		return ShopWorldWidgets.CreateSliced(transform, name, _sprite,
			GameColorPalette.ShopPanelBgColor, new Vector3(0f, 0f, PanelZ), new Vector2(1f, 1f));
	}

	private TextMeshPro CreateHeader(string name, string text, TextAlignmentOptions alignment, Color color)
	{
		// Header pivot rides the alignment (left headers grow right, the right-aligned
		// deck counter grows left) — same rule as the pre-factory CreateHeader.
		Vector2 pivot = new Vector2(alignment == TextAlignmentOptions.Left ? 0f : 1f, 0.5f);
		return ShopWorldWidgets.CreateWorldLabel(transform, name, _font, text,
			Tuning(t => t.headerFontSize, HeaderFontSizeDefault), color, alignment,
			new Vector2(6f, 1f), pivot, new Vector3(0f, 0f, HeaderZ));
	}

	// Same world-button recipe as ShopChrome's (both via ShopWorldWidgets since
	// 2026-09-23); the reroll keeps its action + label wiring here, position comes from
	// FitPanel.
	private PhysButton CreateRerollButton()
	{
		TextMeshPro label;
		PhysButton button = ShopWorldWidgets.CreateWorldButton(transform, "RerollButton", _font, _sprite,
			"重掷 $0", 0f, 0f,
			Tuning(t => t.buttonWidth, ButtonWidthDefault),
			Tuning(t => t.buttonHeight, ButtonHeightDefault),
			Tuning(t => t.buttonFontSize, ButtonFontSizeDefault),
			Tuning(t => t.restShadowUnits, RestShadowUnitsDefault),
			Tuning(t => t.denyShiftUnits, DenyShiftUnitsDefault),
			out label);
		button.SetWorldAction(() => { if (ShopManager.me != null) ShopManager.me.Reroll(); });
		_rerollLabel = label;
		return button;
	}

	private static List<Vector3> TargetsOf(IReadOnlyList<GameObject> cards)
	{
		List<Vector3> result = new List<Vector3>();
		if (cards == null) return result;
		foreach (GameObject card in cards)
		{
			if (card == null) continue;
			CardPhysObjScript phys = card.GetComponent<CardPhysObjScript>();
			result.Add(phys != null ? phys.TargetPosition : (Vector3)card.transform.position);
		}
		return result;
	}

	private void FitPanel(SpriteRenderer panel, TextMeshPro header, TextMeshPro counter, List<Vector3> centers, float halfW, float aboveH, float belowH, bool reroll)
	{
		Bounds content = ComputeContentBounds(centers, halfW, aboveH, belowH);
		bool visible = content.size.sqrMagnitude > 0.0001f;
		panel.gameObject.SetActive(visible);
		header.gameObject.SetActive(visible);
		if (counter != null) counter.gameObject.SetActive(visible);
		if (reroll && _rerollButton != null) _rerollButton.gameObject.SetActive(visible);
		if (!visible) return;

		float sidePadding = Tuning(t => t.sidePadding, SidePaddingDefault);
		float topPadding = Tuning(t => t.topPadding, TopPaddingDefault);
		float bottomPadding = Tuning(t => t.bottomPadding, BottomPaddingDefault);
		float headerHeight = Tuning(t => t.headerHeight, HeaderHeightDefault);
		float headerLeftMargin = Tuning(t => t.headerLeftMargin, HeaderLeftMarginDefault);
		float headerRightMargin = Tuning(t => t.headerRightMargin, HeaderRightMarginDefault);
		float buttonWidth = Tuning(t => t.buttonWidth, ButtonWidthDefault);
		// Snap while hidden (phase travel): a tween would land later than the next show; the
		// visible case keeps the gliding fit (tunable via fitTweenDuration).
		float fitDuration = Tuning(t => t.fitTweenDuration, FitTweenDurationDefault);
		if (!gameObject.activeSelf) fitDuration = 0f;

		Vector3 min = content.min - new Vector3(sidePadding, bottomPadding, 0f);
		Vector3 max = content.max + new Vector3(sidePadding, headerHeight + topPadding, 0f);
		Vector3 targetPos = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, PanelZ);
		Vector2 targetSize = new Vector2(max.x - min.x, max.y - min.y);

		panel.transform.DOKill();
		DOTween.Kill(panel);
		panel.transform.DOMove(targetPos, fitDuration).SetEase(Ease.OutQuad);
		DOTween.To(() => panel.size, v => panel.size = v, targetSize, fitDuration).SetEase(Ease.OutQuad);

		float headerY = max.y - headerHeight * 0.5f;
		header.transform.position = new Vector3(min.x + headerLeftMargin, headerY, HeaderZ);
		if (counter != null)
		{
			counter.transform.position = new Vector3(max.x - headerRightMargin, headerY, HeaderZ);
		}
		if (reroll && _rerollButton != null)
		{
			_rerollButton.transform.position = new Vector3(max.x - headerRightMargin - buttonWidth * 0.5f - 0.15f, headerY, 0f);
		}
	}

	private void Refresh()
	{
		RefreshCounter();
		RefreshRerollState();
	}

	// VISUAL-FIX(2026-09-21): Deck panel slot counter read 00/NN forever (never 01/03, 03/03)
	//   Cause:    used = deckSize - SpawnedEmptySlots.Count, assuming the spawned empty slots are
	//             the FREE ones. They are one recessed frame per GRID slot instead — spawned up to
	//             deckSize on shop entry and on deckSize growth, cleared only with the cards — so
	//             the subtraction was always deckSize - deckSize = 0 and the left number could
	//             never leave 00. (The port plan carried the same wrong assumption.)
	//   Affects:  ShopSectionPanels.RefreshCounter (Deck panel header), reached from
	//             ShopManager buy/sell/reroll RefreshIfActive and ShowIfActive
	//   Regress:  Enter Shop: counter reads 00/03 with an empty deck; buy a slot card -> 01/03,
	//             02/03 ... 03/03; sell -> decrements; buy the deck-slot meter card -> denominator
	//             grows; owning a utility passive (Upgrades row) never moves the number
	//   Related:  ShopManager.BuyFunc deck-full gate, UtilityFuncManagerScript.CountSlotOccupyingCards
	private void RefreshCounter()
	{
		ShopManager shop = ShopManager.me;
		int total = shop != null && shop.deckSize != null ? shop.deckSize.value : 0;
		int used = shop != null ? CountUsedSlots(shop.playerDeckRef, shop.DuplicateCopiesShareSlot) : 0;
		if (used == _lastCounterUsed && total == _lastCounterTotal) return;
		_lastCounterUsed = used;
		_lastCounterTotal = total;
		if (_deckCounter != null) _deckCounter.text = FormatSlotCount(used, total);
	}

	// Reroll label/disabled port from ShopChrome.RefreshRerollState (button relocated here).
	private void RefreshRerollState()
	{
		ShopManager shop = ShopManager.me;
		if (shop == null || _rerollButton == null) return;

		int freeLeft = shop.FreeRerollsLeft;
		string label = freeLeft > 0 ? "重掷 $0" : "重掷 $" + shop.RerollPrice;
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
