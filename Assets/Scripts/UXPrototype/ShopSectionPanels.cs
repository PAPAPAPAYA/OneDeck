using System.Collections.Generic;
using DefaultNamespace.Managers;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-built world-space section panels for the shop page (UIKitDemo 08 port,
/// plan-shop-panels-port-2026-09-18): translucent dark rounded rectangles behind the
/// Shop / Deck / Upgrades rows, with header labels, the Deck slot counter and
/// the reroll button — relocated from the chrome band into the Shop panel header per
/// the 2026-09-18 annotated layout. Built once by Bootstrap (mirrors ShopChrome);
/// refitted by RefreshLayout, which ShopUXManager calls after every relayout. 2026-09-22:
/// appearance values are live-tunable via ShopUXManager.panels
/// (plan-shop-sectionpanels-live-tuning-2026-09-22) — resolved at RefreshLayout time,
/// never snapshotted. Panels
/// carry no colliders and never intercept physics input. Pure helpers (bounds, slot
/// format) are static for EditMode coverage. 2026-09-23: widget recipes (panel bg /
/// header labels / reroll button) build through ShopWorldWidgets — this file keeps fit
/// + refresh logic only (plan-shop-layout-config-widget-factory). 2026-09-29: the reroll
/// button's style moved to the authored RerollButton.prefab (Tpl_WorldButton variant) —
/// this file keeps only its action / dynamic-text / disabled wiring
/// (plan-shop-reroll-button-prefab-2026-09-29). Same day: the deck counter split into
/// two HP-pill-style labels (used + "/" at the big size, total small) positioned by
/// anchor-relative offset tunings (plan-shop-deck-counter-hp-style-2026-09-29).
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
	private const float CounterUsedFontSizeDefault = 10f;
	private const float CounterTotalFontSizeDefault = 5f;
	// Vector2 cannot be a const — static readonly for the manager-absent corner. The
	// offset defaults are starting points (used's right edge ~1 unit left of the anchor,
	// putting the "/" left of the total); the Play tuning session moves them from there.
	private static readonly Vector2 CounterUsedOffsetDefault = new Vector2(-1.0f, 0f);
	private static readonly Vector2 CounterTotalOffsetDefault = Vector2.zero;

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
	// HP-pill-style split (plan-shop-deck-counter-hp-style-2026-09-29): the used side
	// ("03/") renders at the big base size, the total side ("05") at the small size.
	// Zero padding keeps both pieces at a constant 2-char width, so the fixed anchor
	// offsets never misalign as the numbers change.
	private TextMeshPro _deckCounterUsed;
	private TextMeshPro _deckCounterTotal;
	// Upgrade-slot cap counter (plans/plan-upgrade-slot-cap-2026-09-30.md): same HP-pill
	// split, riding the Upgrades header corner.
	private TextMeshPro _upgradeCounterUsed;
	private TextMeshPro _upgradeCounterTotal;
	private PhysButton _rerollButton;
	private TMP_Text _rerollLabel;
	private PaletteTint _rerollLabelTint;
	private GameObject _rerollButtonPrefab;
	private bool _rerollRolling;
	private string _lastRerollLabel;
	private bool _lastRerollDisabled;
	private int _lastCounterUsed = int.MinValue;
	private int _lastCounterTotal = int.MinValue;
	private int _lastUpgradeUsed = int.MinValue;
	private int _lastUpgradeTotal = int.MinValue;

	/// <summary>
	/// Builds the panels once (idempotent) as a scene-root object; shows them when the
	/// game is already in Shop phase (same bootstrap-timing guard as ShopChrome). The
	/// reroll button prefab is optional — a null / unwired value falls back to the legacy
	/// runtime-built button (old scenes / headless).
	/// </summary>
	public static void Bootstrap(Sprite sprite, TMP_FontAsset font, GameObject rerollButtonPrefab)
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
		_instance._rerollButtonPrefab = rerollButtonPrefab;
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

	// Same point-of-use resolver for the counter's Vector2 offset tunings.
	private static Vector2 TuningVec2(System.Func<ShopUXManager.PanelsTuning, Vector2> selector, Vector2 fallback)
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

	// Same point-of-use resolver for the Bold-SDF swap (PanelsTuning.boldFont): null when
	// unwired or manager absent (tests / headless construction) — ApplySharedStyle then
	// keeps the legacy TMP synthetic bold instead.
	private static TMP_FontAsset TuningBoldFont()
	{
		ShopUXManager ux = ShopUXManager.Instance;
		return ux != null && ux.panels != null ? ux.panels.boldFont : null;
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

		FitPanel(_shopPanel, _shopHeader, null, null, TargetsOf(ux != null ? ux.SpawnedShopCards : null), halfW, aboveH, belowH, reroll: true);
		List<Vector3> deckCenters = TargetsOf(ux != null ? ux.SpawnedPlayerCards : null);
		deckCenters.AddRange(TargetsOf(ux != null ? ux.SpawnedEmptySlots : null));
		FitPanel(_deckPanel, _deckHeader, _deckCounterUsed, _deckCounterTotal, deckCenters, halfW, aboveH, belowH, reroll: false);
		FitPanel(_upgradesPanel, _upgradesHeader, _upgradeCounterUsed, _upgradeCounterTotal, TargetsOf(ux != null ? ux.SpawnedUtilityCards : null), halfW, aboveH, belowH, reroll: false);
	}

	// Build-once style values re-applied on every refit so a Play-mode tuning session moves
	// them too (plan §3.3): header font sizes + the deck counter's two labels — the reroll
	// label's style is owned by its prefab since the 2026-09-29 port. Panel geometry
	// re-derives from the paddings inside FitPanel on every call by itself.
	private void ApplySharedStyle()
	{
		float headerFontSize = Tuning(t => t.headerFontSize, HeaderFontSizeDefault);
		// fontBold: wired boldFont swaps in the Bold SDF asset — real Latin weights from its
		// atlas, real CJK weights via its SourceHanSansCN-Bold fallback (faceInfo metrics
		// identical to the Regular asset, no layout shift). Unwired keeps the legacy TMP
		// synthetic bold (FontStyles.Bold vertex dilation, probe-verified 2026-09-25).
		bool fontBold = TuningBold();
		TMP_FontAsset boldFont = TuningBoldFont();
		TMP_FontAsset labelFont = fontBold && boldFont != null ? boldFont : _font;
		FontStyles fontStyle = fontBold && boldFont == null ? FontStyles.Bold : FontStyles.Normal;
		_shopHeader.font = labelFont;
		_shopHeader.fontSize = headerFontSize;
		_shopHeader.fontStyle = fontStyle;
		_deckHeader.font = labelFont;
		_deckHeader.fontSize = headerFontSize;
		_deckHeader.fontStyle = fontStyle;
		_upgradesHeader.font = labelFont;
		_upgradesHeader.fontSize = headerFontSize;
		_upgradesHeader.fontStyle = fontStyle;
		if (_deckCounterUsed != null)
		{
			_deckCounterUsed.font = labelFont;
			_deckCounterUsed.fontSize = Tuning(t => t.counterUsedFontSize, CounterUsedFontSizeDefault);
			_deckCounterUsed.fontStyle = fontStyle;
		}
		if (_deckCounterTotal != null)
		{
			_deckCounterTotal.font = labelFont;
			_deckCounterTotal.fontSize = Tuning(t => t.counterTotalFontSize, CounterTotalFontSizeDefault);
			_deckCounterTotal.fontStyle = fontStyle;
		}
		if (_upgradeCounterUsed != null)
		{
			_upgradeCounterUsed.font = labelFont;
			_upgradeCounterUsed.fontSize = Tuning(t => t.counterUsedFontSize, CounterUsedFontSizeDefault);
			_upgradeCounterUsed.fontStyle = fontStyle;
		}
		if (_upgradeCounterTotal != null)
		{
			_upgradeCounterTotal.font = labelFont;
			_upgradeCounterTotal.fontSize = Tuning(t => t.counterTotalFontSize, CounterTotalFontSizeDefault);
			_upgradeCounterTotal.fontStyle = fontStyle;
		}
		// Force the next RefreshCounter to rewrite both texts so size retunes apply live.
		_lastCounterUsed = int.MinValue;
		_lastCounterTotal = int.MinValue;
		_lastUpgradeUsed = int.MinValue;
		_lastUpgradeTotal = int.MinValue;
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

	/// <summary>Deck counter used side: zero-padded two digits + trailing slash ("03/"),
	/// rendered at the used label's base size (HP pill HpValue format convention).</summary>
	public static string FormatCounterUsed(int used)
	{
		return used.ToString("00") + "/";
	}

	/// <summary>Deck counter total side: zero-padded two digits ("05"), rendered at the
	/// total label's smaller size (HP pill HpMax format convention).</summary>
	public static string FormatCounterTotal(int total)
	{
		return total.ToString("00");
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
		_deckCounterUsed = CreateHeader("DeckCounterUsed", string.Empty, TextAlignmentOptions.Right, GameColorPalette.TooltipTextColor);
		_deckCounterUsed.fontSize = Tuning(t => t.counterUsedFontSize, CounterUsedFontSizeDefault);
		_deckCounterTotal = CreateHeader("DeckCounterTotal", string.Empty, TextAlignmentOptions.Right, GameColorPalette.TooltipTextColor);
		_deckCounterTotal.fontSize = Tuning(t => t.counterTotalFontSize, CounterTotalFontSizeDefault);
		_upgradeCounterUsed = CreateHeader("UpgradeCounterUsed", string.Empty, TextAlignmentOptions.Right, GameColorPalette.TooltipTextColor);
		_upgradeCounterUsed.fontSize = Tuning(t => t.counterUsedFontSize, CounterUsedFontSizeDefault);
		_upgradeCounterTotal = CreateHeader("UpgradeCounterTotal", string.Empty, TextAlignmentOptions.Right, GameColorPalette.TooltipTextColor);
		_upgradeCounterTotal.fontSize = Tuning(t => t.counterTotalFontSize, CounterTotalFontSizeDefault);
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

	// Reroll button: authored prefab when wired (style authority — RerollButton.prefab,
	// a Tpl_WorldButton variant, 2026-09-29 plan), legacy runtime build otherwise (old
	// scenes / headless). Both paths keep the action + label wiring here; position comes
	// from FitPanel.
	private PhysButton CreateRerollButton()
	{
		if (_rerollButtonPrefab != null)
		{
			GameObject go = Instantiate(_rerollButtonPrefab, transform, false);
			go.name = "RerollButton";
			PhysButton button = go.GetComponent<PhysButton>();
			TextMeshPro label = go.GetComponentInChildren<TextMeshPro>(true);
			Transform faceTr = go.transform.Find("Visual/Face");
			SpriteRenderer face = faceTr != null ? faceTr.GetComponent<SpriteRenderer>() : null;
			// VISUAL-FIX(2026-09-29): Reroll face stayed white after a disabled -> re-enable cycle
			//   Cause:    PhysButton._faceColor (the re-enable restore color) is captured only in
			//             SetWorldFace; prefab instances carry _faceRenderer serialized but nobody
			//             called the setter, so _faceColor kept its Color.white default.
			//   Affects:  ShopSectionPanels.CreateRerollButton prefab branch (ApplyDisabledVisual
			//             restore); chrome Exit/Options buttons never disable, so they were unaffected
			//   Regress:  Play: drain purse + free rerolls -> reroll disabled (dim) -> regain money
			//             or a free reroll -> face must return to ButtonFace, not white
			//   Related:  PhysButton.SetWorldFace / ApplyDisabledVisual, RerollButton.prefab
			// The palette Apply first is deliberate: the instance is built under an inactive
			// root, so PaletteTint.OnEnable has not run yet and the capture must see the live
			// palette color, not the prefab's baked-at-port-time one.
			PaletteTint faceTint = face != null ? face.GetComponent<PaletteTint>() : null;
			if (faceTint != null) faceTint.Apply();
			button.SetWorldFace(face);
			button.SetWorldAction(() => { if (ShopManager.me != null) ShopManager.me.Reroll(); });
			_rerollLabel = label;
			_rerollLabelTint = label != null ? label.GetComponent<PaletteTint>() : null;
			return button;
		}

		TextMeshPro fallbackLabel;
		PhysButton fallbackButton = ShopWorldWidgets.CreateWorldButton(transform, "RerollButton", _font, _sprite,
			"重掷 $0", 0f, 0f,
			// Legacy fallback values = the former factory defaults of the retired
			// panels.button* tuning (width / height / font size / rest shadow / deny shift).
			2.0f, 0.56f, 2.4f, 0.05f, 0.075f,
			out fallbackLabel);
		fallbackButton.SetWorldAction(() => { if (ShopManager.me != null) ShopManager.me.Reroll(); });
		_rerollLabel = fallbackLabel;
		_rerollLabelTint = null;
		return fallbackButton;
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

	private void FitPanel(SpriteRenderer panel, TextMeshPro header, TextMeshPro counterUsed, TextMeshPro counterTotal, List<Vector3> centers, float halfW, float aboveH, float belowH, bool reroll)
	{
		Bounds content = ComputeContentBounds(centers, halfW, aboveH, belowH);
		bool visible = content.size.sqrMagnitude > 0.0001f;
		panel.gameObject.SetActive(visible);
		header.gameObject.SetActive(visible);
		if (counterUsed != null) counterUsed.gameObject.SetActive(visible);
		if (counterTotal != null) counterTotal.gameObject.SetActive(visible);
		if (reroll && _rerollButton != null) _rerollButton.gameObject.SetActive(visible);
		if (!visible) return;

		float sidePadding = Tuning(t => t.sidePadding, SidePaddingDefault);
		float topPadding = Tuning(t => t.topPadding, TopPaddingDefault);
		float bottomPadding = Tuning(t => t.bottomPadding, BottomPaddingDefault);
		float headerHeight = Tuning(t => t.headerHeight, HeaderHeightDefault);
		float headerLeftMargin = Tuning(t => t.headerLeftMargin, HeaderLeftMarginDefault);
		float headerRightMargin = Tuning(t => t.headerRightMargin, HeaderRightMarginDefault);
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
		// The counter's two labels (HP-pill style split, plan-shop-deck-counter-hp-style-
		// 2026-09-29) ride anchor-relative offsets: Play tuning moves them, while panel
		// refits (deckSize growth shifts max.x) keep them glued to the header corner.
		if (counterUsed != null)
		{
			Vector3 anchor = new Vector3(max.x - headerRightMargin, headerY, HeaderZ);
			// Explicit Vector2 -> Vector3 cast: UnityEngine defines the conversion both ways,
			// so anchor + Vector2 is an ambiguous '+' (CS0034).
			counterUsed.transform.position = anchor + (Vector3)TuningVec2(t => t.counterUsedOffset, CounterUsedOffsetDefault);
			if (counterTotal != null)
				counterTotal.transform.position = anchor + (Vector3)TuningVec2(t => t.counterTotalOffset, CounterTotalOffsetDefault);
		}
		if (reroll && _rerollButton != null)
		{
			// Width reads the live instance face (prefab is the size authority since the
			// 2026-09-29 port) so a prefab-side resize keeps the button right-aligned.
			Vector2 faceSize = _rerollButton.WorldFaceSize;
			_rerollButton.transform.position = new Vector3(max.x - headerRightMargin - faceSize.x * 0.5f - 0.15f, headerY, 0f);
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
		if (used != _lastCounterUsed || total != _lastCounterTotal)
		{
			_lastCounterUsed = used;
			_lastCounterTotal = total;
			if (_deckCounterUsed != null) _deckCounterUsed.text = FormatCounterUsed(used);
			if (_deckCounterTotal != null) _deckCounterTotal.text = FormatCounterTotal(total);
		}

		// Upgrade-slot cap counter (plans/plan-upgrade-slot-cap-2026-09-30.md): the denominator
		// is the DYNAMIC upgradeCap (not the maxUpgradeSlots ceiling), so buying the upgrade
		// meter card grows it live, same as the deck counter's denominator on a slot card.
		int upgradeTotal = shop != null && shop.upgradeCap != null ? shop.upgradeCap.value : 0;
		int upgradeUsed = shop != null ? UtilityFuncManagerScript.CountUpgradeCards(shop.playerDeckRef) : 0;
		if (upgradeUsed == _lastUpgradeUsed && upgradeTotal == _lastUpgradeTotal) return;
		_lastUpgradeUsed = upgradeUsed;
		_lastUpgradeTotal = upgradeTotal;
		if (_upgradeCounterUsed != null) _upgradeCounterUsed.text = FormatCounterUsed(upgradeUsed);
		if (_upgradeCounterTotal != null) _upgradeCounterTotal.text = FormatCounterTotal(upgradeTotal);
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
			// Prefab label: flip the PaletteTint slot (OwnerText <-> CardTextSoft) instead of
			// a raw color write — a direct color would be clobbered by the next PaletteTint
			// re-apply (OnEnable / editor Changed broadcast). The runtime-built fallback label
			// has no tint component and keeps the direct color flip.
			if (_rerollLabelTint != null)
			{
				_rerollLabelTint.slot = disabled ? PaletteTint.Slot.CardTextSoft : PaletteTint.Slot.OwnerText;
				_rerollLabelTint.Apply();
			}
			else
			{
				_rerollLabel.color = disabled ? GameColorPalette.CardTextSoftColor : GameColorPalette.OwnerTextColor;
			}
		}
	}
}
