using TMPro;
using UnityEngine;

/// <summary>
/// World-widget recipes for the runtime-built shop panels (2026-09-23,
/// plan-shop-layout-config-widget-factory). Since the 2026-09-24 prefab port
/// (plan-shop-hud-prefab-widgets) the shop top bar is authored in
/// ShopHudPage.prefab — the sole remaining consumer is ShopSectionPanels (panel
/// backgrounds, header labels, reroll button).
/// Byte-level invariants carried over from the original recipes (do not "improve" in
/// place — the panels render against these exact values):
///   - button hierarchy Root(BoxCollider2D)/Visual/Shadow(z +0.04, under Root)/Face
///     (z +0.02, under Visual)/Label(under Visual), colors CardShadowColor /
///     OwnerCardColor / OwnerTextColor, hoverLift = restShadow;
///   - chip root z 0.02, dark chips on ShopPanelBgColor else TooltipBgColor, white
///     TooltipTextColor label with a width-0.15 rect inset;
///   - world TMP: no wrap, Overflow overflow, raycastTarget left at the TMP default.
/// Sizes, fonts and press feel are arguments — callers resolve them from their own
/// Tuning; this class owns no layout decisions.
/// </summary>
public static class ShopWorldWidgets
{
	/// <summary>Sliced-sprite background (panel / chip face class of visual).</summary>
	public static SpriteRenderer CreateSliced(Transform parent, string name, Sprite sprite,
		Color color, Vector3 localPosition, Vector2 size)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, false);
		go.transform.localPosition = localPosition;
		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = sprite;
		sr.drawMode = SpriteDrawMode.Sliced;
		sr.color = color;
		sr.size = size;
		return sr;
	}

	/// <summary>
	/// World TMP label. The header callers pass a left/right pivot so the rect grows away
	/// from its anchor; buttons/chips pass center.
	/// </summary>
	public static TextMeshPro CreateWorldLabel(Transform parent, string name, TMP_FontAsset font,
		string text, float fontSize, Color color, TextAlignmentOptions alignment,
		Vector2 size, Vector2 pivot, Vector3 localPosition)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, false);
		go.transform.localPosition = localPosition;
		TextMeshPro tmp = go.AddComponent<TextMeshPro>();
		tmp.font = font;
		tmp.fontSharedMaterial = font.material;
		tmp.fontSize = fontSize;
		tmp.color = color;
		tmp.alignment = alignment;
		tmp.enableWordWrapping = false;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.rectTransform.sizeDelta = size;
		tmp.rectTransform.pivot = pivot;
		tmp.text = text;
		return tmp;
	}

	// Ported from ShopChrome.CreateChip (VISUAL-FIX 2026-09-22 history): the accent flag
	// was removed there — every chip number is white (UIKitDemo §08); LogHighlight stays
	// combat-log only.
	public static HudChip CreateChip(Transform parent, string name, Sprite sprite, TMP_FontAsset font,
		float width, float height, float fontSize, bool darkPanel)
	{
		GameObject chipGo = new GameObject(name);
		chipGo.transform.SetParent(parent, false);
		chipGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
		SpriteRenderer panel = CreateSliced(chipGo.transform, "Panel", sprite,
			darkPanel ? GameColorPalette.ShopPanelBgColor : GameColorPalette.TooltipBgColor,
			Vector3.zero, new Vector2(width, height));
		TextMeshPro label = CreateWorldLabel(chipGo.transform, "Label", font, string.Empty, fontSize,
			GameColorPalette.TooltipTextColor, TextAlignmentOptions.Center,
			new Vector2(width - 0.15f, height), new Vector2(0.5f, 0.5f), Vector3.zero);
		HudChip chip = chipGo.AddComponent<HudChip>();
		chip.Bind(panel, label);
		return chip;
	}

	/// <summary>
	/// World physical button (exit / options / reroll). Position and size are arguments;
	/// callers re-apply sizes live via PhysButton.ConfigureWorldFaceSize on retune (the
	/// BoxCollider2D rides on the root and follows for free).
	/// </summary>
	public static PhysButton CreateWorldButton(Transform parent, string name, TMP_FontAsset font,
		Sprite sprite, string label, float x, float y, float width, float height, float fontSize,
		float restShadow, float denyShift, out TextMeshPro labelTmp)
	{
		GameObject rootGo = new GameObject(name, typeof(BoxCollider2D));
		rootGo.transform.SetParent(parent, false);
		rootGo.transform.localPosition = new Vector3(x, y, 0f);

		GameObject visualGo = new GameObject("Visual");
		visualGo.transform.SetParent(rootGo.transform, false);

		GameObject shadowGo = new GameObject("Shadow", typeof(SpriteRenderer));
		shadowGo.transform.SetParent(rootGo.transform, false);
		shadowGo.transform.localPosition = new Vector3(0f, 0f, 0.04f);
		SpriteRenderer shadow = shadowGo.GetComponent<SpriteRenderer>();
		shadow.sprite = sprite;
		shadow.drawMode = SpriteDrawMode.Sliced;
		shadow.color = GameColorPalette.CardShadowColor;

		GameObject faceGo = new GameObject("Face", typeof(SpriteRenderer));
		faceGo.transform.SetParent(visualGo.transform, false);
		faceGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
		SpriteRenderer face = faceGo.GetComponent<SpriteRenderer>();
		face.sprite = sprite;
		face.drawMode = SpriteDrawMode.Sliced;
		face.color = GameColorPalette.ButtonFaceColor;

		labelTmp = CreateWorldLabel(visualGo.transform, "Label", font, label, fontSize,
			GameColorPalette.OwnerTextColor, TextAlignmentOptions.Center,
			new Vector2(width - 0.2f, height), new Vector2(0.5f, 0.5f), Vector3.zero);

		PhysButton button = rootGo.AddComponent<PhysButton>();
		button.SetShadowTransform(shadowGo.transform);
		button.SetWorldFace(face);
		button.SetVisualGroup(visualGo.transform);
		button.restShadow = restShadow;
		button.hoverLift = restShadow;
		button.denyShift = denyShift;
		button.label = labelTmp;
		// Label rect is centered on the Visual origin, so the face centers there too.
		button.ConfigureWorldFaceSize(new Vector2(width, height), Vector2.zero);
		return button;
	}
}
