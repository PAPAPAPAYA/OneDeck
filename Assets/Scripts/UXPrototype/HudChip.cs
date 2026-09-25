using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI kit 3.5 read-only info unit: flat panel + one label. No shadow, no lift, no
// raycast target (R2 converse: players never try to press it). Render-agnostic since
// the 2026-09-18 world-chrome port: the panel is an Image (canvas) or a SpriteRenderer
// (world) — Setup picks whichever is present; the label is TMP_Text, the common base of
// UI and world TMP.
public class HudChip : MonoBehaviour
{
	[SerializeField] private Image _panelImage;
	[SerializeField] private SpriteRenderer _panelSprite;
	[SerializeField] private TMP_Text _label;

	/// <summary>Runtime-built variant (world chrome): wires refs that serialization can't.</summary>
	public void Bind(SpriteRenderer panel, TMP_Text label)
	{
		_panelSprite = panel;
		_label = label;
	}

	// VISUAL-FIX(2026-09-22): the accent flag routed labels to the yellow HighlightColor;
	//   UIKitDemo §08 chips carry no accent — every number is white. Parameter removed;
	//   LogHighlight.asset stays for combat-log highlights only.
	/// <summary>
	/// Applies the read-only look from the palette: tooltip text on tooltip background.
	/// UIKitDemo §08 chips carry no accent color — every number is white.
	/// </summary>
	public void Setup(string initialText)
	{
		Color panelColor = GameColorPalette.TooltipBgColor;
		if (_panelImage != null) _panelImage.color = panelColor;
		if (_panelSprite != null) _panelSprite.color = panelColor;
		if (_label != null)
		{
			_label.color = GameColorPalette.TooltipTextColor;
			_label.text = initialText;
		}
	}

	public void SetText(string text)
	{
		if (_label != null) _label.text = text;
	}

	public void SetVisible(bool visible)
	{
		gameObject.SetActive(visible);
	}
}
