using System;
using System.Collections.Generic;
using System.Linq;
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
	public float statColumnFlex = 1f;

	[Header("Row Width")]
	[Tooltip("Extra left/right inset for title/header/rows inside each half, in reference-resolution pixels. Increase to make rows narrower.")]
	public float rowHorizontalPadding = 0f;

	[Header("Spacing (reference-resolution pixels)")]
	[Tooltip("Vertical gap between title, header and rows inside each half")]
	public float rowSpacing = 4f;

	[Header("Background")]
	[Range(0f, 1f)]
	public float backgroundAlpha = 0.6f;
}

/// <summary>
/// Result-screen per-card combat stats panel. Built entirely at runtime (no prefab/scene wiring):
/// PhaseManager creates one instance, calls Build() once on entering the Result phase,
/// and Clear() on exit.
///
/// The panel is split into two stacked halves: top = cards created by the Player, bottom = cards
/// created by the Enemy (a player-generated enemy-owned curse counts as player-created). Each half
/// shows one row per card type: display name with a copy-count suffix " (X)" (initial-deck copies,
/// shown only when X >= 2) plus all registry stat columns; percentage columns show the row's
/// share of that half's column total.
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

		// Two stacked halves inside the body: top = player-created cards, bottom = enemy-created cards
		var halvesGo = new GameObject("Halves", typeof(RectTransform));
		halvesGo.transform.SetParent(bodyGo.transform, false);
		var halvesRect = (RectTransform)halvesGo.transform;
		halvesRect.anchorMin = Vector2.zero;
		halvesRect.anchorMax = Vector2.one;
		halvesRect.offsetMin = new Vector2(8f, 8f);
		halvesRect.offsetMax = new Vector2(-8f, -8f);
		var halvesLayout = halvesGo.AddComponent<VerticalLayoutGroup>();
		halvesLayout.childControlWidth = true;
		halvesLayout.childForceExpandWidth = true;
		halvesLayout.childControlHeight = true;
		halvesLayout.childForceExpandHeight = true;
		halvesLayout.spacing = 8f;

		BuildHalf(halvesGo.transform, CardFaction.Player, rows);
		BuildHalf(halvesGo.transform, CardFaction.Enemy, rows);
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

	/// <summary>
	/// Build one half (title + header + scrollable rows) for one creator faction.
	/// </summary>
	private void BuildHalf(Transform parent, CardFaction faction, List<PerCardStatRecord> allRows)
	{
		var halfGo = new GameObject("Half_" + faction, typeof(RectTransform));
		halfGo.transform.SetParent(parent, false);
		var halfLayout = halfGo.AddComponent<VerticalLayoutGroup>();
		halfLayout.childControlWidth = true;
		halfLayout.childForceExpandWidth = true;
		halfLayout.childControlHeight = true;
		halfLayout.childForceExpandHeight = false;
		halfLayout.spacing = _layout.rowSpacing;
		int hPad = Mathf.RoundToInt(_layout.rowHorizontalPadding);
		halfLayout.padding = new RectOffset(hPad, hPad, 0, 0);

		var rows = allRows == null
			? new List<PerCardStatRecord>()
			: allRows.Where(r => r.faction == faction).ToList();

		// Half totals per stat column: the base for each row's share percentages
		var halfTotals = new Dictionary<CombatStatType, float>();
		foreach (var def in CombatStatRegistry.Stats)
		{
			float total = 0f;
			foreach (var row in rows) total += row.GetValue(def.type);
			halfTotals[def.type] = total;
		}

		// Title ("YOU" / "ENEMY") in the faction color
		var titleGo = new GameObject("Title", typeof(RectTransform));
		titleGo.transform.SetParent(halfGo.transform, false);
		var titleElement = titleGo.AddComponent<LayoutElement>();
		titleElement.preferredHeight = _layout.rowHeight;
		CreateText(titleGo.transform, FactionLabel(faction), FactionColor(faction), TextAlignmentOptions.Center, true);

		// Header row: Card + one cell per registry column
		var headerGo = new GameObject("Header", typeof(RectTransform));
		headerGo.transform.SetParent(halfGo.transform, false);
		var headerElement = headerGo.AddComponent<LayoutElement>();
		headerElement.preferredHeight = _layout.rowHeight;
		ConfigureRowLayout(headerGo);
		BuildRowCells(headerGo.transform, "Card", null, true, null);

		// Scroll view below the header, taking the half's remaining height
		var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
		scrollGo.transform.SetParent(halfGo.transform, false);
		var scrollElement = scrollGo.AddComponent<LayoutElement>();
		scrollElement.flexibleHeight = 1f;
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

		if (rows.Count == 0)
		{
			var emptyGo = new GameObject("EmptyRow", typeof(RectTransform));
			emptyGo.transform.SetParent(contentGo.transform, false);
			var emptyElement = emptyGo.AddComponent<LayoutElement>();
			emptyElement.preferredHeight = _layout.rowHeight;
			CreateText(emptyGo.transform, "No damage recorded.", Color.white, TextAlignmentOptions.Center, false);
			return;
		}

		foreach (var row in rows)
		{
			var rowGo = new GameObject("Row_" + row.cardTypeID, typeof(RectTransform));
			rowGo.transform.SetParent(contentGo.transform, false);
			var rowElement = rowGo.AddComponent<LayoutElement>();
			rowElement.preferredHeight = _layout.rowHeight;
			ConfigureRowLayout(rowGo);
			BuildRowCells(rowGo.transform, DisplayNameWithCount(row), row, false, halfTotals);
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

	private void BuildRowCells(Transform parent, string cardName, PerCardStatRecord row, bool isHeader, Dictionary<CombatStatType, float> halfTotals)
	{
		var nameText = CreateCell(parent, cardName, _layout.nameColumnFlex, TextAlignmentOptions.Left, isHeader);
		nameText.color = Color.white;

		foreach (var def in CombatStatRegistry.GetColumnsSorted())
		{
			string value = isHeader ? def.columnHeader : FormatStatValue(def, row, halfTotals);
			var cell = CreateCell(parent, value, _layout.statColumnFlex, TextAlignmentOptions.Center, isHeader);
			if (!isHeader && ColorUtility.TryParseHtmlString(def.ColorHex, out var statColor))
			{
				cell.color = statColor;
			}
		}
	}

	/// <summary>Display name with a copy-count suffix " (X)", shown only for 2+ copies in the initial deck.</summary>
	private static string DisplayNameWithCount(PerCardStatRecord row)
	{
		return row.instanceCount >= 2 ? row.displayName + " (" + row.instanceCount + ")" : row.displayName;
	}

	/// <summary>
	/// "12 (34%)" for stats marked showPercentageOfTotal — the row's share of this half's
	/// column total; plain number otherwise (or when the half total is zero).
	/// </summary>
	private string FormatStatValue(CombatStatDef def, PerCardStatRecord row, Dictionary<CombatStatType, float> halfTotals)
	{
		float value = row.GetValue(def.type);
		if (!def.showPercentageOfTotal) return ((int)value).ToString();
		if (value <= 0f) return "0";
		float total = halfTotals != null && halfTotals.TryGetValue(def.type, out var t) ? t : 0f;
		if (total <= 0f) return ((int)value).ToString();
		int pct = Mathf.RoundToInt(value / total * 100f);
		return (int)value + " (" + pct + "%)";
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

	private static Color FactionColor(CardFaction faction)
	{
		var palette = GameColorPalette.Me;
		if (palette == null) return Color.white;
		var so = faction == CardFaction.Player ? palette.ownerCardColor : palette.opponentCardColor;
		return so != null ? so.value : Color.white;
	}
}
