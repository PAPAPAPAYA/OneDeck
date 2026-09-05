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
	[Tooltip("Top-right corner of the panel as a screen fraction (0..1). The panel auto-shrinks vertically to fit its rows, so anchorMax.y acts as the upper cap.")]
	public Vector2 anchorMax = new Vector2(0.95f, 0.32f);

	[Header("Own Canvas")]
	[Tooltip("Reference resolution of the panel's private canvas. All font sizes and row heights are in these pixels.")]
	public Vector2 referenceResolution = new Vector2(1080f, 1920f);
	[Tooltip("Sorting order of the panel canvas (must exceed the game canvas)")]
	public int sortingOrder = 200;

	[Header("Typography (reference-resolution pixels)")]
	[Tooltip("Font size of the data rows (card names and stat values).")]
	public float fontSize = 40f;
	[Tooltip("Font size of the half titles (YOU/ENEMY) and the column header row (Card, Dmg>Opp, ...).")]
	public float headerFontSize = 40f;
	[Tooltip("Height of each data row (one row per card type). Also used as the scroll sensitivity.")]
	public float rowHeight = 60f;
	[Tooltip("Height of the half title (YOU/ENEMY) and the column header row. Independent from rowHeight so tightening the header area does not change the spacing between data rows.")]
	public float headerRowHeight = 60f;

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
/// share of that half's column total. Rows beyond the visible area scroll (mouse wheel / drag),
/// and the panel height auto-shrinks toward its rows with the configured anchorMax.y as the cap.
///
/// The panel root is its own Canvas + CanvasScaler, so font sizes and row heights use the
/// configured reference resolution and stay readable regardless of the game canvas scaling.
/// </summary>
public class ResultStatsPanel : MonoBehaviour
{
	// Halves container geometry, shared by Build() and the adaptive-height math
	private const float HalvesInset = 8f;   // Inset of the Halves rect from the body edges
	private const float HalvesSpacing = 8f; // Vertical gap between the player and enemy halves

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
		// VISUAL-FIX(2026-09-04): Landscape Game window showed only ~1 row per half of the panel
		//   Cause:    The scaler matched width (matchWidthOrHeight = 0, ref 1080x1920), so on a
		//             landscape window the scale factor (~1.78) shrank the fraction-sized body
		//             rect far below its reference-pixel budget: 0.61 screen height became only
		//             ~370 reference px, and after title + header each half fit less than one
		//             rowHeight row. The body rect is sized by screen fractions while typography
		//             and row heights are reference pixels, so both sides must share one axis.
		//   Affects:  ResultStatsPanel Build (own CanvasScaler), AdaptiveAnchorMax
		//   Regress:  Open the Result panel in a landscape (e.g. 1920x1080) Game view — each half
		//             must still show ~7 rows (scroll for the rest); portrait unchanged.
		// Match height: screen height in reference pixels is then exactly referenceResolution.y,
		// which is the axis both the body fractions and the pixel budgets are derived from.
		scaler.matchWidthOrHeight = 1f;
		rootGo.AddComponent<GraphicRaycaster>();

		// Panel body: screen-fraction rect inside the root canvas. Height auto-shrinks to fit
		// the rows (VISUAL-FIX(2026-09-04), see AdaptiveAnchorMax); anchorMax.y is the cap.
		var bodyGo = new GameObject("Body", typeof(RectTransform));
		bodyGo.transform.SetParent(_root, false);
		var bodyRect = (RectTransform)bodyGo.transform;
		bodyRect.anchorMin = _layout.anchorMin;
		bodyRect.anchorMax = AdaptiveAnchorMax(rows);
		bodyRect.offsetMin = Vector2.zero;
		bodyRect.offsetMax = Vector2.zero;

		var bg = bodyGo.AddComponent<Image>();
		// RGB from the palette; alpha stays a layout knob (backgroundAlpha).
		Color bgColor = GameColorPalette.ResultPanelBgColor;
		bg.color = new Color(bgColor.r, bgColor.g, bgColor.b, _layout.backgroundAlpha);

		// Two stacked halves inside the body: top = player-created cards, bottom = enemy-created cards
		var halvesGo = new GameObject("Halves", typeof(RectTransform));
		halvesGo.transform.SetParent(bodyGo.transform, false);
		var halvesRect = (RectTransform)halvesGo.transform;
		halvesRect.anchorMin = Vector2.zero;
		halvesRect.anchorMax = Vector2.one;
		halvesRect.offsetMin = new Vector2(HalvesInset, HalvesInset);
		halvesRect.offsetMax = new Vector2(-HalvesInset, -HalvesInset);
		var halvesLayout = halvesGo.AddComponent<VerticalLayoutGroup>();
		halvesLayout.childControlWidth = true;
		halvesLayout.childForceExpandWidth = true;
		halvesLayout.childControlHeight = true;
		halvesLayout.childForceExpandHeight = true;
		halvesLayout.spacing = HalvesSpacing;

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
	/// VISUAL-FIX(2026-09-04): Result panel showed only a few stat rows with no way to see the rest
	///   Cause:    The body rect used the configured anchorMax.y (scene: 0.64) as a fixed height
	///             regardless of row count — small data sets wasted screen space, and large ones
	///             relied on the ScrollView, which could not scroll (see the scroll hit-area fix
	///             in BuildHalf).
	///   Affects:  ResultStatsPanel Build (Body rect)
	///   Regress:  Enter Result after a 1-2 distinct-card combat — the panel hugs its content;
	///             after a full-deck combat — the panel grows to the configured anchorMax.y cap
	///             and the overflow rows scroll (needs the scroll hit-area fix).
	/// Height-capped anchorMax: shrink the panel from the configured anchorMax.y to just fit the
	/// taller half (title + header + rows, plus the Halves container inset and gap). The
	/// configured value stays the cap; the ScrollView remains the fallback beyond it. Valid
	/// because the scaler matches height, so screen height in reference pixels is exactly
	/// referenceResolution.y.
	/// </summary>
	private Vector2 AdaptiveAnchorMax(List<PerCardStatRecord> rows)
	{
		int playerRows = rows != null ? rows.Count(r => r.faction == CardFaction.Player) : 0;
		int enemyRows = rows != null ? rows.Count(r => r.faction == CardFaction.Enemy) : 0;
		// Empty halves still render one "No damage recorded." row
		int maxHalfRows = Mathf.Max(1, Mathf.Max(playerRows, enemyRows));
		// Half = title + header (each headerRowHeight) + 2 rowSpacing gaps + N rows (content row spacing is 0)
		float halfNeeded = 2f * _layout.headerRowHeight + 2f * _layout.rowSpacing + maxHalfRows * _layout.rowHeight;
		float neededFraction = (2f * halfNeeded + HalvesSpacing + 2f * HalvesInset) / _layout.referenceResolution.y;
		var anchorMax = _layout.anchorMax;
		anchorMax.y = Mathf.Min(anchorMax.y, _layout.anchorMin.y + neededFraction);
		return anchorMax;
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

		// VISUAL-FIX(2026-08-02): Header row stretches to fill half the panel, centering its text
		//   with huge gaps above/below (looks like broken row spacing)
		//   Cause:    ConfigureRowLayout adds a HorizontalLayoutGroup with childForceExpandHeight=true,
		//             which reports flexibleHeight=1; LayoutElement.flexibleHeight=-1 falls through to it,
		//             so the parent VerticalLayoutGroup shares the leftover height equally between Header
		//             and ScrollView. Explicit flexibleHeight=0 blocks the leak. Same guard applied to
		//             Title/rows defensively (TMP/HLG children can expose flexibleHeight).
		//   Affects:  ResultStatsPanel half layout (Title / Header / ScrollView / data rows)
		//   Regress:  Result phase: header row height must equal headerRowHeight exactly and sit directly
		//             under the title; data rows start flush under the header.
		// Title ("YOU" / "ENEMY") in the faction color
		var titleGo = new GameObject("Title", typeof(RectTransform));
		titleGo.transform.SetParent(halfGo.transform, false);
		var titleElement = titleGo.AddComponent<LayoutElement>();
		titleElement.preferredHeight = _layout.headerRowHeight;
		titleElement.flexibleHeight = 0f;
		CreateText(titleGo.transform, FactionLabel(faction), FactionColor(faction), TextAlignmentOptions.Center, true);

		// Header row: Card + one cell per registry column
		var headerGo = new GameObject("Header", typeof(RectTransform));
		headerGo.transform.SetParent(halfGo.transform, false);
		var headerElement = headerGo.AddComponent<LayoutElement>();
		headerElement.preferredHeight = _layout.headerRowHeight;
		headerElement.flexibleHeight = 0f;
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

		// VISUAL-FIX(2026-09-04): Overflow stat rows could never be scrolled into view
		//   Cause:    The only raycastable graphic in the panel was the Body background Image,
		//             whose parent chain does not contain the ScrollRect (row texts are
		//             raycastTarget = false, the Viewport has no Graphic). EventSystem walks up
		//             from the hit object to find IScrollHandler/IDragHandler, so wheel and drag
		//             events never reached the ScrollView and the content stayed clamped.
		//   Affects:  ResultStatsPanel BuildHalf (ScrollView input path)
		//   Regress:  Enter Result with more rows than fit a half; mouse-wheel and drag over the
		//             rows — the content must scroll within the half and clamp at both ends.
		// Invisible raycast target under the ScrollRect so wheel/drag hits resolve to it
		// (alpha 0 still raycasts; only raycastTarget matters).
		var scrollHitArea = scrollGo.AddComponent<Image>();
		scrollHitArea.color = new Color(0f, 0f, 0f, 0f);
		scrollHitArea.raycastTarget = true;

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
			emptyElement.flexibleHeight = 0f; // see VISUAL-FIX(2026-08-02) above
			CreateText(emptyGo.transform, "No damage recorded.", GameColorPalette.ResultPanelTextColor, TextAlignmentOptions.Center, false);
			return;
		}

		foreach (var row in rows)
		{
			var rowGo = new GameObject("Row_" + row.cardTypeID, typeof(RectTransform));
			rowGo.transform.SetParent(contentGo.transform, false);
			var rowElement = rowGo.AddComponent<LayoutElement>();
			rowElement.preferredHeight = _layout.rowHeight;
			rowElement.flexibleHeight = 0f; // see VISUAL-FIX(2026-08-02) above
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
		nameText.color = GameColorPalette.ResultPanelTextColor;

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
		var tmp = CreateText(cellGo.transform, text, GameColorPalette.ResultPanelTextColor, alignment, isHeader);
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
		return faction == CardFaction.Player ? GameColorPalette.OwnerCardColor : GameColorPalette.OpponentCardColor;
	}
}
