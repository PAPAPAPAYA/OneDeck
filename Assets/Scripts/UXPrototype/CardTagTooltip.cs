using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating tag tooltip shown when hovering a physical card. Self-contained:
/// builds its own Screen Space canvas/panel at runtime on first use (no scene
/// wiring needed), follows the mouse, and force-hides on phase change, card
/// flip to face-down, or card destroy. Follows the presenter convention
/// (CombatIconPresenter, CombatHPBarPresenter): pure presentation, no game logic.
/// </summary>
public class CardTagTooltip : MonoBehaviour
{
	private static CardTagTooltip _instance;

	private Canvas _canvas;
	private RectTransform _panel;
	private TextMeshProUGUI _text;
	private CardPhysObjScript _source;
	private EnumStorage.GamePhase? _phaseAtShow;

	/// <summary>
	/// Show the tooltip for the given card (called after the hover delay elapses).
	/// No-op when the card has no visible tags.
	/// </summary>
	public static void ShowFor(CardPhysObjScript card)
	{
		if (card == null) return;
		string tagText = card.GetTagText();
		if (string.IsNullOrEmpty(tagText)) return;
		EnsureInstance();
		if (_instance == null) return;
		_instance.Show(card, tagText);
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
		_panel.pivot = new Vector2(0f, 1f);
		var bg = panelGo.AddComponent<Image>();
		bg.color = new Color(0f, 0f, 0f, 0.85f);
		var fitter = panelGo.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		var layout = panelGo.AddComponent<HorizontalLayoutGroup>();
		layout.padding = new RectOffset(12, 12, 8, 8);

		var textGo = new GameObject("Text");
		textGo.transform.SetParent(panelGo.transform, false);
		_text = textGo.AddComponent<TextMeshProUGUI>();
		_text.fontSize = 28;
		_text.color = Color.white;
		_text.raycastTarget = false;
		bg.raycastTarget = false;

		panelGo.SetActive(false);
	}

	private void Show(CardPhysObjScript card, string tagText)
	{
		_source = card;
		_text.text = tagText;
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
	/// Place the panel next to the cursor, flipping the pivot when the panel
	/// would leave the right/bottom screen edge.
	/// </summary>
	private void UpdatePosition()
	{
		Vector2 mousePos = Input.mousePosition;
		Vector2 size = _panel.rect.size * _canvas.scaleFactor;
		float pivotX = (mousePos.x + 16f + size.x > Screen.width) ? 1f : 0f;
		float pivotY = (mousePos.y - 16f - size.y < 0f) ? 0f : 1f;
		_panel.pivot = new Vector2(pivotX, pivotY);
		float offsetX = pivotX == 0f ? 16f : -16f;
		float offsetY = pivotY == 1f ? -16f : 16f;
		_panel.position = new Vector2(mousePos.x + offsetX, mousePos.y + offsetY);
	}
}
