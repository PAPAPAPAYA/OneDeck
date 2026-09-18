using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-built world-space section panels for the shop page (UIKitDemo 08 port,
/// plan-shop-panels-port-2026-09-18): translucent dark rounded rectangles behind the
/// Shop / Deck / Upgrades rows, with header labels, the Deck slot counter (03/05) and
/// the reroll button — relocated from the chrome band into the Shop panel header per
/// the 2026-09-18 annotated layout. Built once by Bootstrap (mirrors ShopChrome);
/// refitted by RefreshLayout, which ShopUXManager calls after every relayout. Panels
/// carry no colliders and never intercept physics input. Pure helpers (bounds, slot
/// format) are static for EditMode coverage.
/// </summary>
public class ShopSectionPanels : MonoBehaviour
{
	private const string RootName = "Shop Section Panels";

	private const float PanelZ = 0.5f;   // behind cards (z 0) and empty slots (z 0.1)
	private const float HeaderZ = 0.4f;  // in front of the panel background
	private const float HeaderHeight = 1.2f;
	private const float SidePadding = 0.55f;
	private const float TopPadding = 0.35f;
	private const float BottomPadding = 0.5f;
	private const float FitTweenDuration = 0.3f;
	private const float HeaderFontSize = 3.2f;
	private const float HeaderLeftMargin = 0.25f;
	private const float HeaderRightMargin = 0.25f;
	private const float ButtonWidth = 2.0f;
	private const float ButtonHeight = 0.56f;
	private const float ButtonFontSize = 2.4f;
	private const float RestShadowUnits = 0.05f;
	private const float DenyShiftUnits = 0.075f;

	// Card face half-extents at scale 1, from the EmptyCardSpace bake (3.3 x 4.7 units).
	// The below factor includes the price-button allowance (matches ShopChrome.CheckShelfClearance).
	private const float FaceHalfWidthFactor = 1.65f;
	private const float FaceAboveHalfHeightFactor = 2.35f;
	private const float FaceBelowHalfHeightFactor = 2.5f;

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

	private void OnDestroy()
	{
		if (_instance == this) _instance = null;
	}

	private void Build()
	{
		_shopPanel = CreatePanel("PanelShop");
		_deckPanel = CreatePanel("PanelDeck");
		_upgradesPanel = CreatePanel("PanelUpgrades");
		_shopHeader = CreateHeader("HeaderShop", "Shop", TextAlignmentOptions.Left, GameColorPalette.TooltipTextColor);
		_deckHeader = CreateHeader("HeaderDeck", "Deck", TextAlignmentOptions.Left, GameColorPalette.TooltipTextColor);
		_upgradesHeader = CreateHeader("HeaderUpgrades", "Upgrades", TextAlignmentOptions.Left, GameColorPalette.TooltipTextColor);
		_deckCounter = CreateHeader("DeckCounter", string.Empty, TextAlignmentOptions.Right, GameColorPalette.HighlightColor);
		_rerollButton = CreateRerollButton();
	}

	private SpriteRenderer CreatePanel(string name)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);
		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = _sprite;
		sr.drawMode = SpriteDrawMode.Sliced;
		sr.color = GameColorPalette.ShopPanelBgColor;
		sr.size = new Vector2(1f, 1f);
		go.transform.position = new Vector3(0f, 0f, PanelZ);
		return sr;
	}

	private TextMeshPro CreateHeader(string name, string text, TextAlignmentOptions alignment, Color color)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);
		TextMeshPro tmp = go.AddComponent<TextMeshPro>();
		tmp.font = _font;
		tmp.fontSharedMaterial = _font.material;
		tmp.fontSize = HeaderFontSize;
		tmp.color = color;
		tmp.alignment = alignment;
		tmp.enableWordWrapping = false;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.text = text;
		tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
		tmp.rectTransform.pivot = new Vector2(alignment == TextAlignmentOptions.Left ? 0f : 1f, 0.5f);
		go.transform.position = new Vector3(0f, 0f, HeaderZ);
		return tmp;
	}

	// Same world-button recipe as ShopChrome.CreateButton (kept local: the chrome owns
	// its private copy; the panels own theirs).
	private PhysButton CreateRerollButton()
	{
		GameObject rootGo = new GameObject("RerollButton", typeof(BoxCollider2D));
		rootGo.transform.SetParent(transform, false);

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

		TextMeshPro label = CreateButtonLabel(visualGo.transform, "Reroll: $0");

		PhysButton button = rootGo.AddComponent<PhysButton>();
		button.SetShadowTransform(shadowGo.transform);
		button.SetWorldFace(faceGo.GetComponent<SpriteRenderer>());
		button.SetVisualGroup(visualGo.transform);
		button.restShadow = RestShadowUnits;
		button.hoverLift = RestShadowUnits;
		button.denyShift = DenyShiftUnits;
		button.label = label;
		button.ConfigureWorldFaceSize(new Vector2(ButtonWidth, ButtonHeight), Vector2.zero);
		button.SetWorldAction(() => { if (ShopManager.me != null) ShopManager.me.Reroll(); });
		_rerollLabel = label;
		return button;
	}

	private TextMeshPro CreateButtonLabel(Transform parent, string text)
	{
		GameObject go = new GameObject("Label");
		go.transform.SetParent(parent, false);
		TextMeshPro tmp = go.AddComponent<TextMeshPro>();
		tmp.font = _font;
		tmp.fontSharedMaterial = _font.material;
		tmp.fontSize = ButtonFontSize;
		tmp.color = GameColorPalette.OwnerTextColor;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.enableWordWrapping = false;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.rectTransform.sizeDelta = new Vector2(ButtonWidth - 0.2f, ButtonHeight);
		tmp.text = text;
		return tmp;
	}

	private void SetupSliced(SpriteRenderer sr, Color color)
	{
		sr.sprite = _sprite;
		sr.drawMode = SpriteDrawMode.Sliced;
		sr.color = color;
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

		Vector3 min = content.min - new Vector3(SidePadding, BottomPadding, 0f);
		Vector3 max = content.max + new Vector3(SidePadding, HeaderHeight + TopPadding, 0f);
		Vector3 targetPos = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, PanelZ);
		Vector2 targetSize = new Vector2(max.x - min.x, max.y - min.y);

		panel.transform.DOKill();
		DOTween.Kill(panel);
		panel.transform.DOMove(targetPos, FitTweenDuration).SetEase(Ease.OutQuad);
		DOTween.To(() => panel.size, v => panel.size = v, targetSize, FitTweenDuration).SetEase(Ease.OutQuad);

		float headerY = max.y - HeaderHeight * 0.5f;
		header.transform.position = new Vector3(min.x + HeaderLeftMargin, headerY, HeaderZ);
		if (counter != null)
		{
			counter.transform.position = new Vector3(max.x - HeaderRightMargin, headerY, HeaderZ);
		}
		if (reroll && _rerollButton != null)
		{
			_rerollButton.transform.position = new Vector3(max.x - HeaderRightMargin - ButtonWidth * 0.5f - 0.15f, headerY, 0f);
		}
	}

	private void Refresh()
	{
		RefreshCounter();
		RefreshRerollState();
	}

	private void RefreshCounter()
	{
		int total = ShopManager.me != null && ShopManager.me.deckSize != null ? ShopManager.me.deckSize.value : 0;
		int used = total - (ShopUXManager.Instance != null ? ShopUXManager.Instance.SpawnedEmptySlots.Count : 0);
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
