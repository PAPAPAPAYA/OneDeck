using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating tag tooltip shown when hovering a physical card. Self-contained:
/// builds its own Screen Space canvas/panel at runtime on first use (no scene
/// wiring needed), anchors to the hovered card (right side by default, left
/// side when the right would overflow the screen, vertically centered and
/// clamped), and force-hides on phase change, card flip to face-down, or card
/// destroy. Each visible tag gets a bold "[Tag]" title line followed by its
/// explanation on the next line, where the explanation comes from
/// TagTooltipDatabaseSO (StringSO per tag); tags without a configured
/// explanation show the title only. Follows the
/// presenter convention (CombatIconPresenter, CombatHPBarPresenter): pure
/// presentation, no game logic.
/// </summary>
public class CardTagTooltip : MonoBehaviour
{
	private static CardTagTooltip _instance;

	private const float ScreenMargin = 16f;

	private Canvas _canvas;
	private RectTransform _panel;
	private TextMeshProUGUI _text;
	private CardPhysObjScript _source;
	private Camera _camera;
	private EnumStorage.GamePhase? _phaseAtShow;

	/// <summary>
	/// Show the tooltip for the given card (called after the hover delay elapses).
	/// No-op when the card has no visible tags.
	/// </summary>
	public static void ShowFor(CardPhysObjScript card)
	{
		if (card == null) return;
		string tooltipText = BuildTooltipText(card);
		if (string.IsNullOrEmpty(tooltipText)) return;
		EnsureInstance();
		if (_instance == null) return;
		_instance.Show(card, tooltipText);
	}

	/// <summary>
	/// Hide the tooltip if it is currently shown for the given card.
	/// </summary>
	public static void HideFor(CardPhysObjScript card)
	{
		if (_instance == null) return;
		if (card == null || _instance._source == card)
		{
			_instance.Hide();
		}
	}

	/// <summary>
	/// Per visible tag: a bold "[Tag]" title line, then the explanation on the
	/// next line; tag blocks are separated by a blank line. The explanation is
	/// looked up in TagTooltipDatabaseSO; tags without a (non-empty) description
	/// show the title only. Returns an empty string when there are no visible tags.
	/// </summary>
	private static string BuildTooltipText(CardPhysObjScript card)
	{
		if (card.cardImRepresenting == null || card.cardImRepresenting.myTags == null || card.cardImRepresenting.myTags.Count == 0)
		{
			return string.Empty;
		}

		TagTooltipDatabaseSO db = TagTooltipDatabaseSO.Me;
		StringBuilder sb = new StringBuilder();
		bool hasVisibleTag = false;
		for (int i = 0; i < card.cardImRepresenting.myTags.Count; i++)
		{
			EnumStorage.Tag tag = card.cardImRepresenting.myTags[i];
			if (tag == EnumStorage.Tag.None) continue;
			if (hasVisibleTag)
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

		return hasVisibleTag ? sb.ToString() : string.Empty;
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
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = 300; // above the existing Combat/Shop canvases
		var scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);

		var panelGo = new GameObject("Panel");
		panelGo.transform.SetParent(transform, false);
		_panel = panelGo.AddComponent<RectTransform>();
		_panel.pivot = new Vector2(0f, 0.5f);
		var bg = panelGo.AddComponent<Image>();
		bg.color = new Color(0f, 0f, 0f, 0.85f);
		var fitter = panelGo.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		var layout = panelGo.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(12, 12, 8, 8);
		layout.childAlignment = TextAnchor.UpperLeft;

		var textGo = new GameObject("Text");
		textGo.transform.SetParent(panelGo.transform, false);
		_text = textGo.AddComponent<TextMeshProUGUI>();
		_text.fontSize = 28;
		_text.color = Color.white;
		_text.richText = true; // tag titles use <b>
		_text.raycastTarget = false;
		bg.raycastTarget = false;

		panelGo.SetActive(false);
	}

	private void Show(CardPhysObjScript card, string tooltipText)
	{
		_source = card;
		_camera = null; // re-resolve per show in case the rendering camera changed
		_text.text = tooltipText;
		_phaseAtShow = card.currentGamePhaseRef != null ? card.currentGamePhaseRef.Value() : (EnumStorage.GamePhase?)null;
		_panel.gameObject.SetActive(true);
		UpdatePosition();
	}

	private void Hide()
	{
		_source = null;
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

		_panel.position = new Vector2(x, y);
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
		_panel.position = new Vector2(mousePos.x + offsetX, mousePos.y + offsetY);
	}
}
