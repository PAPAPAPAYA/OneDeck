using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI kit 3.5 read-only info unit: flat panel + one label. No shadow, no lift, no
// raycast target (R2 converse: players never try to press it). Phase-agnostic on
// purpose: the shop top bar uses it today, the combat-side own-HP display is
// planned to reuse the same prefab.
public class HudChip : MonoBehaviour
{
	[SerializeField] private Image _panel;
	[SerializeField] private TMP_Text _label;

	/// <summary>
	/// Applies the read-only look from the palette. Accent (true) uses the numeric
	/// highlight token for emphasized values (money); otherwise the soft tooltip text.
	/// </summary>
	public void Setup(string initialText, bool accent)
	{
		if (_panel != null) _panel.color = GameColorPalette.TooltipBgColor;
		if (_label != null)
		{
			_label.color = accent ? GameColorPalette.HighlightColor : GameColorPalette.TooltipTextColor;
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
