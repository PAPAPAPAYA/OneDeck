using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Layout configuration for ResultStatsPanel. Serialized on PhaseManager so panel
/// position/size/typography are tunable in the Inspector (and live in Play Mode via rebuild).
/// Positions are canvas fractions (0..1), pixels are in the panel's own reference resolution.
/// </summary>
[Serializable]
public class ResultStatsPanelLayout
{
	[Header("Panel Rect (fractions of the screen)")]
	[Tooltip("Bottom-left corner of the panel as a screen fraction (0..1)")]
	public Vector2 anchorMin = new Vector2(0.05f, 0.03f);
	[Tooltip("Top-right corner of the panel as a screen fraction (0..1)")]
	public Vector2 anchorMax = new Vector2(0.95f, 0.32f);

	[Header("Own Canvas")]
	[Tooltip("Reference resolution of the panel's private canvas. All font sizes and row heights are in these pixels.")]
	public Vector2 referenceResolution = new Vector2(1080f, 1920f);
	[Tooltip("Sorting order of the panel canvas (must exceed the game canvas)")]
	public int sortingOrder = 200;

	[Header("Typography (reference-resolution pixels)")]
	public float fontSize = 40f;
	public float headerFontSize = 40f;
	public float rowHeight = 60f;

	[Header("Column Weights (flexible widths)")]
	public float nameColumnFlex = 1.6f;
	public float factionColumnFlex = 0.8f;
	public float statColumnFlex = 1f;

	[Header("Row Width")]
	[Tooltip("Extra left/right inset for header and rows inside the panel body, in reference-resolution pixels. Increase to make rows narrower.")]
	public float rowHorizontalPadding = 0f;

	[Header("Background")]
	[Range(0f, 1f)]
	public float backgroundAlpha = 0.6f;
}

/// <summary>
/// Result-screen per-card combat stats panel. Built entirely at runtime (no prefab/scene wiring):
/// PhaseManager creates one instance, calls Build() once on entering the Result phase,
/// and Clear() on exit. Header and row columns are generated from CombatStatRegistry,
/// so a new stat automatically becomes a new column.
///
/// The panel root is its own Canvas + CanvasScaler, so font sizes and row heights use the
/// configured reference resolution and stay readable regardless of the game canvas scaling.
/// </summary>
public class ResultStatsPanel : MonoBehaviour
{
	private RectTransform _root;
	private Canvas _parentCanvas;
	private List<PerCardStatRecord> _rows;
	private ResultStatsPanelLayout _layout;

	/// <summary>
	/// Build the panel under the given canvas with the given session rows. Call once per Result phase entry.
	/// Layout defaults to ResultStatsPanelLayout defaults when null.
	/// </summary>
	public void Build(Canvas canvas, List<PerCardStatRecord> rows, ResultStatsPanelLayout layout = null)
	{
		Clear();
		if (canvas == null) return;

		_parentCanvas = canvas;
		_rows = rows;
		_layout = layout ?? new ResultStatsPanelLayout();

		// Root: own Canvas + CanvasScaler so internal pixels are predictable regardless of the game canvas
		var rootGo = new GameObject("ResultStatsPanelRoot", typeof(RectTransform));
		rootGo.transform.SetParent(canvas.transform, false);
		_root = (RectTransform)rootGo.transform;
		_root.anchorMin = Vector2.zero;
		_root.anchorMax = Vector2.one;
		_root.offsetMin = Vector2.zero;
		_root.offsetMax = Vector2.zero;

		var ownCanvas = rootGo.AddComponent<Canvas>();
		ownCanvas.overrideSorting = true;
		ownCanvas.sortingOrder = _layout.sortingOrder;
		var scaler = rootGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = _layout.referenceResolution;
		scaler.matchWidthOrHeight = 0f; // portrait game: match width
		rootGo.AddComponent<GraphicRaycaster>();

		// Panel body: screen-fraction rect inside the root canvas
		var bodyGo = new GameObject("Body", typeof(RectTransform));
		bodyGo.transform.SetParent(_root, false);
		var bodyRect = (RectTransform)bodyGo.transform;
		bodyRect.anchorMin = _layout.anchorMin;
		bodyRect.anchorMax = _layout.anchorMax;
		bodyRect.offsetMin = Vector2.zero;
		bodyRect.offsetMax = Vector2.zero;

		var bg = bodyGo.AddComponent<Image>();
		bg.color = new Color(0f, 0f, 0f, _layout.backgroundAlpha);

		// Header row (top of panel body)
		float hPad = _layout.rowHorizontalPadding;
		var headerGo = new GameObject("Header", typeof(RectTransform));
		headerGo.transform.SetParent(bodyGo.transform, false);
		var headerRect = (RectTransform)headerGo.transform;
		headerRect.anchorMin = new Vector2(0f, 1f);
		headerRect.anchorMax = new Vector2(1f, 1f);
		headerRect.pivot = new Vector2(0.5f, 1f);
		headerRect.offsetMin = new Vector2(8f + hPad, -_layout.rowHeight - 4f);
		headerRect.offsetMax = new Vector2(-8f - hPad, -4f);
		ConfigureRowLayout(headerGo);
		BuildRowCells(headerGo.transform, "Card", "Side", null, true);

		// Scroll view below the header
		var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
		scrollGo.transform.SetParent(bodyGo.transform, false);
		var scrollRect = (RectTransform)scrollGo.transform;
		scrollRect.anchorMin = new Vector2(0f, 0f);
		scrollRect.anchorMax = new Vector2(1f, 1f);
		scrollRect.offsetMin = new Vector2(8f + hPad, 8f);
		scrollRect.offsetMax = new Vector2(-8f - hPad, -_layout.rowHeight - 8f);
		var scroll = scrollGo.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.scrollSensitivity = _layout.rowHeight;

		var viewportGo = new GameObject("Viewport", typeof(RectTransform));
		viewportGo.transform.SetParent(scrollGo.transform, false);
		var viewportRect = (RectTransform)viewportGo.transform;
		viewportRect.anchorMin = Vector2.zero;
		viewportRect.anchorMax = Vector2.one;
		viewportRect.offsetMin = Vector2.zero;
		viewportRect.offsetMax = Vector2.zero;
		viewportGo.AddComponent<RectMask2D>();
		scroll.viewport = viewportRect;

		var contentGo = new GameObject("Content", typeof(RectTransform));
		contentGo.transform.SetParent(viewportGo.transform, false);
		var contentRect = (RectTransform)contentGo.transform;
		contentRect.anchorMin = new Vector2(0f, 1f);
		contentRect.anchorMax = new Vector2(1f, 1f);
		contentRect.pivot = new Vector2(0.5f, 1f);
		// Must zero the offsets after changing anchors: a fresh RectTransform carries a
		// default 100x100 sizeDelta, which otherwise makes Content 100px wider than the
		// Viewport and pushes every row past the right edge of the panel.
		contentRect.offsetMin = Vector2.zero;
		contentRect.offsetMax = Vector2.zero;
		var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
		contentLayout.childControlWidth = true;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childForceExpandHeight = false;
		var fitter = contentGo.AddComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		scroll.content = contentRect;

		if (rows == null || rows.Count == 0)
		{
			var emptyGo = new GameObject("EmptyRow", typeof(RectTransform));
			emptyGo.transform.SetParent(contentGo.transform, false);
			var emptyElement = emptyGo.AddComponent<LayoutElement>();
			emptyElement.preferredHeight = _layout.rowHeight;
			CreateText(emptyGo.transform, "No card stats recorded this combat.", Color.white, TextAlignmentOptions.Center, false);
			return;
		}

		foreach (var row in rows)
		{
			var rowGo = new GameObject("Row_" + row.cardTypeID + "_" + row.faction, typeof(RectTransform));
			rowGo.transform.SetParent(contentGo.transform, false);
			var rowElement = rowGo.AddComponent<LayoutElement>();
			rowElement.preferredHeight = _layout.rowHeight;
			ConfigureRowLayout(rowGo);
			BuildRowCells(rowGo.transform, row.displayName, FactionLabel(row.faction), row, false);
		}
	}

	/// <summary>Rebuild the panel with the last Build() arguments (for live layout tuning in Play Mode).</summary>
	public void Rebuild()
	{
		if (_parentCanvas == null) return;
		Build(_parentCanvas, _rows, _layout);
	}

	/// <summary>Destroy the built panel. Call on Result phase exit.</summary>
	public void Clear()
	{
		if (_root != null)
		{
			if (Application.isPlaying)
			{
				Destroy(_root.gameObject);
			}
			else
			{
				DestroyImmediate(_root.gameObject);
			}
			_root = null;
		}
	}

	private static void ConfigureRowLayout(GameObject rowGo)
	{
		var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
		layout.childControlWidth = true;
		layout.childForceExpandWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandHeight = true;
		layout.spacing = 8f;
	}

	private void BuildRowCells(Transform parent, string cardName, string factionLabel, PerCardStatRecord row, bool isHeader)
	{
		var nameText = CreateCell(parent, cardName, _layout.nameColumnFlex, TextAlignmentOptions.Left, isHeader);
		nameText.color = Color.white;

		var factionText = CreateCell(parent, factionLabel, _layout.factionColumnFlex, TextAlignmentOptions.Center, isHeader);
		factionText.color = FactionColor(row, isHeader);

		foreach (var def in CombatStatRegistry.GetColumnsSorted())
		{
			string value = isHeader
				? def.columnHeader
				: ((int)row.GetValue(def.type)).ToString();
			var cell = CreateCell(parent, value, _layout.statColumnFlex, TextAlignmentOptions.Center, isHeader);
			if (!isHeader && ColorUtility.TryParseHtmlString(def.ColorHex, out var statColor))
			{
				cell.color = statColor;
			}
		}
	}

	private TextMeshProUGUI CreateCell(Transform parent, string text, float flexWidth, TextAlignmentOptions alignment, bool isHeader)
	{
		var cellGo = new GameObject("Cell", typeof(RectTransform));
		cellGo.transform.SetParent(parent, false);
		var element = cellGo.AddComponent<LayoutElement>();
		element.flexibleWidth = flexWidth;
		var tmp = CreateText(cellGo.transform, text, Color.white, alignment, isHeader);
		return tmp;
	}

	private TextMeshProUGUI CreateText(Transform parent, string content, Color color, TextAlignmentOptions alignment, bool isHeader)
	{
		var textGo = new GameObject("Text", typeof(RectTransform));
		textGo.transform.SetParent(parent, false);
		var rect = (RectTransform)textGo.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		var tmp = textGo.AddComponent<TextMeshProUGUI>();
		tmp.text = content;
		tmp.fontSize = isHeader ? _layout.headerFontSize : _layout.fontSize;
		tmp.alignment = alignment;
		tmp.raycastTarget = false;
		tmp.color = color;
		tmp.textWrappingMode = TextWrappingModes.NoWrap;
		// Shrink text instead of overflowing when a column is too narrow
		tmp.enableAutoSizing = true;
		tmp.fontSizeMin = Mathf.Max(10f, (isHeader ? _layout.headerFontSize : _layout.fontSize) * 0.4f);
		tmp.fontSizeMax = isHeader ? _layout.headerFontSize : _layout.fontSize;
		if (isHeader)
		{
			tmp.fontStyle = FontStyles.Bold;
		}
		return tmp;
	}

	private static string FactionLabel(CardFaction faction)
	{
		return faction == CardFaction.Player ? "YOU" : "ENEMY";
	}

	private static Color FactionColor(PerCardStatRecord row, bool isHeader)
	{
		if (isHeader || row == null) return Color.white;
		var palette = GameColorPalette.Me;
		if (palette == null) return Color.white;
		var so = row.faction == CardFaction.Player ? palette.ownerCardColor : palette.opponentCardColor;
		return so != null ? so.value : Color.white;
	}
}
