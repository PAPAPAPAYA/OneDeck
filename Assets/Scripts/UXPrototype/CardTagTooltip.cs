using System.Text;
using DefaultNamespace.SOScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating tooltip shown when hovering a physical card. Self-contained:
/// builds its own Screen Space canvas/panel at runtime on first use (no scene
/// wiring needed), anchors to the hovered card (right side by default, left
/// side when the right would overflow the screen, vertically centered and
/// clamped), and force-hides on phase change, card flip to face-down, or card
/// destroy. First block is the card type — "[实体]/可攻击" or "[现象]/不可攻击"
/// — resolved via CardTypeTooltipDatabaseSO; types without a database entry
/// (Token 衍生物) and the Start Card show no type block. Each visible tag then
/// gets a bold "[Tag]" title line followed by its explanation on the next
/// line, from TagTooltipDatabaseSO (StringSO per tag); tags without a
/// configured explanation show the title only. Last comes the card-reference
/// block (CardRefTooltipBlocks): when the hovered card's cardDesc names a
/// CardTooltipDatabaseSO card by bare name, that card is shown with a bold bare
/// title and its own cardDesc, on its own label. That block sits on its own
/// "CardRefs" label (a sibling of the "Text" label, so it can carry its own font
/// size); the tooltip appears when either label has text. Follows the
/// presenter convention (CombatIconPresenter, CombatHPBarPresenter): pure
/// presentation, no game logic.
/// </summary>
public class CardTagTooltip : MonoBehaviour
{
	private static CardTagTooltip _instance;

	private const float ScreenMargin = 16f;
	private const float DefaultFontSize = 28f;

	private Canvas _canvas;
	private RectTransform _panel;
	private TextMeshProUGUI _text;
	private TextMeshProUGUI _cardRefsText;
	private VerticalLayoutGroup _layout;
	private CardPhysObjScript _source;
	private Camera _camera;
	private EnumStorage.GamePhase? _phaseAtShow;
	// Persistent mode (plan-shop-card-hover-lift-2026-10-01 §8.4): the enlarge preview shows
	// the tag tooltip for its whole lifetime. While pinned, hover-driven HideFor calls are
	// ignored; only UnpinFor, Show for another card, or the Update force-hide checks (source
	// destroyed / flipped face-down / phase changed) take it down.
	private bool _pinned;

	/// <summary>
	/// Everything the tooltip shows for one hovered card. MainText = card-type block + tag
	/// blocks (the "Text" label). CardRefText = card-reference blocks (the "CardRefs" label,
	/// see CardRefTooltipBlocks). The tooltip is shown when either is non-empty.
	/// </summary>
	private struct TooltipContent
	{
		public string MainText;
		public string CardRefText;

		public bool HasContent
		{
			get { return !string.IsNullOrEmpty(MainText) || !string.IsNullOrEmpty(CardRefText); }
		}
	}

	/// <summary>
	/// Show the tooltip for the given card and PIN it: it stays visible (following the card)
	/// until UnpinFor, even through hover end / cursor离开. Used by the enlarge preview modal.
	/// No-op when the card has no content (no type block, no visible tag and no card-reference
	/// block).
	/// </summary>
	public static void PinFor(CardPhysObjScript card)
	{
		if (card == null) return;
		TooltipContent content = BuildTooltipContent(card);
		if (!content.HasContent) return;
		EnsureInstance();
		if (_instance == null) return;
		_instance._pinned = true;
		_instance.Show(card, content);
	}

	/// <summary>
	/// Remove the persistent tooltip pinned by <see cref="PinFor"/>. Clears the pin only when
	/// the tooltip is currently shown for that card (or the card is already gone).
	/// </summary>
	public static void UnpinFor(CardPhysObjScript card)
	{
		if (_instance == null) return;
		if (card == null || _instance._source == card)
		{
			_instance._pinned = false;
			_instance.Hide();
		}
	}

	/// <summary>
	/// Show the tooltip for the given card (called after the hover delay elapses).
	/// No-op when the card has no content (no type block, no visible tag and no card-reference
	/// block).
	/// </summary>
	public static void ShowFor(CardPhysObjScript card)
	{
		if (card == null) return;
		TooltipContent content = BuildTooltipContent(card);
		if (!content.HasContent) return;
		EnsureInstance();
		if (_instance == null) return;
		_instance.Show(card, content);
	}

	/// <summary>
	/// Hide the tooltip if it is currently shown for the given card.
	/// </summary>
	public static void HideFor(CardPhysObjScript card)
	{
		if (_instance == null) return;
		// A pinned tooltip (enlarge preview) ignores hover-driven hides; only UnpinFor or
		// the Update force-hide checks may take it down.
		if (_instance._pinned) return;
		if (card == null || _instance._source == card)
		{
			_instance.Hide();
		}
	}

	/// <summary>
	/// First the card-type block (via <see cref="TryAppendTypeBlock"/>, skipped for
	/// Token cards and the Start Card), then one block per visible tag: a bold
	/// "[Tag]" title line and the explanation on the next line, separated by a
	/// blank line. The explanation is looked up in TagTooltipDatabaseSO; tags
	/// without a (non-empty) description show the title only. Returns an empty
	/// string when there is neither a type block nor any visible tag (no tooltip).
	/// </summary>
	private static string BuildTooltipText(CardScript card)
	{
		StringBuilder sb = new StringBuilder();
		bool hasTypeBlock = TryAppendTypeBlock(card, sb);
		bool hasVisibleTag = TryAppendTagBlocks(card, sb, hasTypeBlock);
		return hasTypeBlock || hasVisibleTag ? sb.ToString() : string.Empty;
	}

	/// <summary>Null-safe overload resolving the hovered physical card to its CardScript.</summary>
	private static string BuildTooltipText(CardPhysObjScript card)
	{
		return card != null ? BuildTooltipText(card.cardImRepresenting) : string.Empty;
	}

	/// <summary>Null-safe overload resolving the hovered physical card to its CardScript.</summary>
	private static TooltipContent BuildTooltipContent(CardPhysObjScript card)
	{
		if (card == null) return default(TooltipContent);
		return BuildTooltipContent(card.cardImRepresenting);
	}

	/// <summary>
	/// Both labels' text for a card prefab: MainText from the type/tag databases,
	/// CardRefText from CardRefTooltipBlocks (the card-reference block).
	/// </summary>
	private static TooltipContent BuildTooltipContent(CardScript card)
	{
		TooltipContent content;
		content.MainText = BuildTooltipText(card);
		content.CardRefText = card != null ? CardRefTooltipBlocks.BuildCardRefText(card) : string.Empty;
		return content;
	}

	/// <summary>
	/// Type block for the hovered card: bold "[Name]" title from
	/// CardTypeTooltipDatabaseSO, explanation on the next line. Skipped for the
	/// Start Card and for card types without a database entry (Token 衍生物 —
	/// data-driven exclusion, 2026-09-14).
	/// </summary>
	private static bool TryAppendTypeBlock(CardScript card, StringBuilder sb)
	{
		if (card == null || card.isStartCard) return false;

		EnumStorage.CardType cardType = card.cardType;
		CardTypeTooltipDatabaseSO db = CardTypeTooltipDatabaseSO.Me;
		string displayName = db != null ? db.GetDisplayName(cardType) : null;
		if (string.IsNullOrEmpty(displayName)) return false;

		sb.Append("<b>[");
		sb.Append(displayName);
		sb.Append("]</b>");
		StringSO description = db.GetDescription(cardType);
		if (description != null && !string.IsNullOrEmpty(description.value))
		{
			sb.Append("\n");
			sb.Append(description.value);
		}
		return true;
	}

	private static bool TryAppendTagBlocks(CardScript card, StringBuilder sb, bool hasPrecedingBlock)
	{
		if (card == null || card.myTags == null || card.myTags.Count == 0)
		{
			return false;
		}

		TagTooltipDatabaseSO db = TagTooltipDatabaseSO.Me;
		bool hasVisibleTag = false;
		for (int i = 0; i < card.myTags.Count; i++)
		{
			EnumStorage.Tag tag = card.myTags[i];
			if (tag == EnumStorage.Tag.None) continue;
			if (hasVisibleTag || (hasPrecedingBlock && sb.Length > 0))
			{
				sb.Append("\n\n");
			}
			sb.Append("<b>[");
			sb.Append(TagTooltipDatabaseSO.GetTagDisplayName(tag));
			sb.Append("]</b>");
			string description = GetTagDescription(db, tag);
			if (!string.IsNullOrEmpty(description))
			{
				sb.Append("\n");
				sb.Append(description);
			}
			hasVisibleTag = true;
		}

		return hasVisibleTag;
	}

	private static string GetTagDescription(TagTooltipDatabaseSO db, EnumStorage.Tag tag)
	{
		if (db == null) return null;
		var so = db.GetDescription(tag);
		if (so == null || string.IsNullOrEmpty(so.value)) return null;
		return so.value;
	}

	private static void EnsureInstance()
	{
		if (_instance != null) return;
		var go = new GameObject("CardTagTooltip");
		_instance = go.AddComponent<CardTagTooltip>();
		_instance.BuildUI();
	}

	private void BuildUI()
	{
		_canvas = gameObject.AddComponent<Canvas>();
		// Screen Space - Camera so the fullscreen pixelation pass covers the tooltip too.
		_canvas.renderMode = RenderMode.ScreenSpaceCamera;
		_canvas.worldCamera = Camera.main;
		_canvas.planeDistance = 100f;
		_canvas.sortingOrder = 300; // above the existing Combat/Shop canvases
		var scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);

		var panelGo = new GameObject("Panel");
		panelGo.transform.SetParent(transform, false);
		_panel = panelGo.AddComponent<RectTransform>();
		_panel.pivot = new Vector2(0f, 0.5f);
		var bg = panelGo.AddComponent<Image>();
		bg.color = GameColorPalette.TooltipBgColor;
		var fitter = panelGo.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		_layout = panelGo.AddComponent<VerticalLayoutGroup>();
		_layout.padding = new RectOffset(12, 12, 8, 8);
		_layout.childAlignment = TextAnchor.UpperLeft;

		var textGo = new GameObject("Text");
		textGo.transform.SetParent(panelGo.transform, false);
		_text = textGo.AddComponent<TextMeshProUGUI>();
		_text.fontSize = DefaultFontSize;
		_text.color = GameColorPalette.TooltipTextColor;
		_text.richText = true; // tag titles use <b>
		_text.raycastTarget = false;
		bg.raycastTarget = false;

		// Card-reference block label: a sibling of the main label so the block can carry
		// its own font size (CardTooltipDatabaseSO.cardBlock.fontSize). Same font and paragraph
		// settings as the main label; active only when it has text.
		var cardRefsGo = new GameObject("CardRefs");
		cardRefsGo.transform.SetParent(panelGo.transform, false);
		_cardRefsText = cardRefsGo.AddComponent<TextMeshProUGUI>();
		_cardRefsText.font = _text.font;
		_cardRefsText.alignment = _text.alignment;
		_cardRefsText.enableWordWrapping = _text.enableWordWrapping;
		_cardRefsText.overflowMode = _text.overflowMode;
		_cardRefsText.fontSize = DefaultFontSize;
		_cardRefsText.color = GameColorPalette.TooltipTextColor;
		_cardRefsText.richText = true;
		_cardRefsText.raycastTarget = false;
		cardRefsGo.SetActive(false);

		panelGo.SetActive(false);
	}

	private void Show(CardPhysObjScript card, TooltipContent content)
	{
		_source = card;
		_camera = null; // re-resolve per show in case the rendering camera changed
		ApplyContent(content);
		_phaseAtShow = card.currentGamePhaseRef != null ? card.currentGamePhaseRef.Value() : (EnumStorage.GamePhase?)null;
		_panel.gameObject.SetActive(true);
		UpdatePosition();
	}

	/// <summary>
	/// Feed one card's content to the two labels. Each label is active only when it has text.
	/// With both active, the gap between them equals one blank line of the main label (the
	/// separator between the blocks above) plus the database's spacingBefore.
	/// </summary>
	private void ApplyContent(TooltipContent content)
	{
		string mainText = content.MainText ?? string.Empty;
		string cardRefText = content.CardRefText ?? string.Empty;
		bool hasMain = mainText.Length > 0;
		bool hasCardRefs = cardRefText.Length > 0;

		float spacingBefore = 0f;
		if (hasCardRefs)
		{
			_cardRefsText.text = cardRefText;
			ApplyCardBlockStyle();
			CardTooltipDatabaseSO db = CardTooltipDatabaseSO.Me;
			if (db != null && db.cardBlock != null)
			{
				spacingBefore = db.cardBlock.spacingBefore;
			}
		}
		_cardRefsText.gameObject.SetActive(hasCardRefs);

		// Measure the gap while the main label is active, then write the real text last.
		_text.gameObject.SetActive(true);
		float gap = hasMain && hasCardRefs ? ComputeCardRefGap(mainText) + Mathf.Max(0f, spacingBefore) : 0f;
		// Measure the gap first, then write the real text with SetText: GetPreferredValues(string)
		// measures its argument in place by overwriting the label's internal text arrays, and the
		// text setter would early-out on an unchanged string (same card hovered again).
		_text.SetText(mainText);
		_text.gameObject.SetActive(hasMain);
		_layout.spacing = gap;
	}

	/// <summary>
	/// Height of one blank line in the main label: the difference between appending "\n\n"
	/// and "\n" (plus a trailing glyph) to the real text, measured on that label.
	/// </summary>
	private float ComputeCardRefGap(string mainText)
	{
		const string tail = "X";
		float withBlankLine = _text.GetPreferredValues(mainText + "\n\n" + tail).y;
		float withoutBlankLine = _text.GetPreferredValues(mainText + "\n" + tail).y;
		return Mathf.Max(0f, withBlankLine - withoutBlankLine);
	}

	/// <summary>
	/// Card-block tunables that live on the label itself. Colors and the title format are
	/// already applied inside the text by CardRefTooltipBlocks.BuildCardRefText.
	/// </summary>
	private void ApplyCardBlockStyle()
	{
		float fontSize = 0f;
		CardTooltipDatabaseSO db = CardTooltipDatabaseSO.Me;
		if (db != null && db.cardBlock != null)
		{
			fontSize = db.cardBlock.fontSize;
		}
		_cardRefsText.fontSize = fontSize > 0f ? fontSize : DefaultFontSize;
	}

	private void Hide()
	{
		_source = null;
		_pinned = false;
		if (_panel != null)
		{
			_panel.gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		if (_panel == null || !_panel.gameObject.activeSelf) return;

		// Force-hide: source destroyed, flipped face-down, or game phase changed.
		if (_source == null || !_source.isFaceUp || PhaseChanged())
		{
			Hide();
			return;
		}
		UpdatePosition();
	}

	private bool PhaseChanged()
	{
		if (!_phaseAtShow.HasValue || _source.currentGamePhaseRef == null) return false;
		return _source.currentGamePhaseRef.Value() != _phaseAtShow.Value;
	}

	/// <summary>
	/// Anchor the panel to the hovered card: right side of the card by default,
	/// left side when the panel would overflow the right screen edge; vertically
	/// centered on the card and clamped into the screen. Falls back to the
	/// cursor when no rendering camera is available.
	/// </summary>
	private void UpdatePosition()
	{
		if (_camera == null)
		{
			_camera = Camera.main;
		}
		if (_camera == null || _source == null)
		{
			UpdatePositionAtMouse();
			return;
		}

		// Card bounds in world space -> screen space.
		var col = _source.GetComponent<Collider2D>();
		Bounds bounds = col != null
			? col.bounds
			: new Bounds(_source.transform.position, Vector3.zero);
		Vector3 center = _camera.WorldToScreenPoint(bounds.center);
		Vector3 rightEdge = _camera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z));
		Vector3 leftEdge = _camera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z));

		Vector2 size = _panel.rect.size * _canvas.scaleFactor;

		// Horizontal: right side by default, flip to the left on overflow.
		bool fitsRight = rightEdge.x + ScreenMargin + size.x <= Screen.width;
		_panel.pivot = new Vector2(fitsRight ? 0f : 1f, 0.5f);
		float x = fitsRight ? rightEdge.x + ScreenMargin : leftEdge.x - ScreenMargin;

		// Clamp horizontally into the screen (covers panels wider than the gap on both sides).
		float left = Mathf.Clamp(x - _panel.pivot.x * size.x, 0f, Mathf.Max(0f, Screen.width - size.x));
		x = left + _panel.pivot.x * size.x;

		// Vertical: centered on the card, clamped into the screen.
		float halfHeight = size.y * 0.5f;
		float y = size.y >= Screen.height
			? Screen.height * 0.5f
			: Mathf.Clamp(center.y, halfHeight, Screen.height - halfHeight);

		SetPanelScreenPosition(new Vector2(x, y));
	}

	/// <summary>
	/// Fallback: place the panel next to the cursor, flipping the pivot when the
	/// panel would leave the right/bottom screen edge.
	/// </summary>
	private void UpdatePositionAtMouse()
	{
		Vector2 mousePos = Input.mousePosition;
		Vector2 size = _panel.rect.size * _canvas.scaleFactor;
		float pivotX = (mousePos.x + ScreenMargin + size.x > Screen.width) ? 1f : 0f;
		float pivotY = (mousePos.y - ScreenMargin - size.y < 0f) ? 0f : 1f;
		_panel.pivot = new Vector2(pivotX, pivotY);
		float offsetX = pivotX == 0f ? ScreenMargin : -ScreenMargin;
		float offsetY = pivotY == 1f ? -ScreenMargin : ScreenMargin;
		SetPanelScreenPosition(new Vector2(mousePos.x + offsetX, mousePos.y + offsetY));
	}

	// VISUAL-FIX(2026-09-05): Hover tag tooltip never appeared on screen after the pixelation
	//   commit (78adfc8) migrated the runtime-built canvas from Screen Space Overlay to
	//   Screen Space Camera: UpdatePosition/UpdatePositionAtMouse still assigned raw
	//   screen-pixel coordinates to RectTransform.position, an Overlay-canvas idiom.
	//   Cause:    Under Screen Space - Camera, RectTransform.position is WORLD space; with the
	//             camera at z=-100 / fov 7, the visible world at the canvas plane is only
	//             ~12x22 units, so pixel-valued coords landed hundreds of units outside the
	//             frustum — the panel activated (ShowFor fired, text set) but rendered
	//             completely off-screen.
	//   Affects:  CardTagTooltip (UpdatePosition, UpdatePositionAtMouse)
	//   Regress:  In Shop or Combat, hover a face-up card with a tag (诅咒/被动/复活 etc.):
	//             the tooltip panel appears anchored to the card (right side, left on overflow,
	//             clamped into the screen) within ~0.1s and hides when the cursor leaves; in a
	//             narrow Game window it flips/clamps instead of vanishing off-screen.
	private void SetPanelScreenPosition(Vector2 screenPos)
	{
		// Screen point -> canvas world point for the active render mode; an Overlay fallback
		// canvas (null worldCamera) is handled by the utility as well.
		var canvasRect = _canvas.transform as RectTransform;
		if (canvasRect != null
			&& RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, _canvas.worldCamera, out Vector3 worldPos))
		{
			_panel.position = worldPos;
		}
	}
}
