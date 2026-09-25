using TMPro;
using UnityEngine;

/// <summary>
/// Palette-driven tint for prefab HUD widgets (2026-09-24, plan-shop-hud-prefab-widgets
/// §3.2): resolves one named GameColorPalette slot and paints it onto the SpriteRenderer
/// and/or TMP_Text on the SAME GameObject. ExecuteAlways so prefab-stage editing shows
/// real colors (the palette statics are editor-available — existing HUD preview
/// precedent); variants recolor by overriding the slot field, no code. Re-applies on
/// palette asset edits via the editor-only GameColorPalette.Changed broadcast.
/// </summary>
[ExecuteAlways]
public class PaletteTint : MonoBehaviour
{
	public enum Slot
	{
		ShopPanelBg,
		TooltipBg,
		TooltipText,
		OwnerCard,
		OwnerText,
		CardShadow,
		CardFaceDim,
		CardTextSoft,
		IconNameLabel,
		HpNormalPlayer,
		// Append-only (serialized by int — inserting would shift existing widgets' slots;
		// plan-palette-chip-button-color-decoupling-2026-09-25).
		SlotRecess,
		ChipBg,
		ChipText,
		ButtonFace,
	}

	[Tooltip("Which GameColorPalette slot paints this GameObject's SpriteRenderer / TMP_Text.")]
	public Slot slot = Slot.TooltipBg;

	private void OnEnable()
	{
#if UNITY_EDITOR
		GameColorPalette.Changed += Apply;
		ColorSO.Changed += OnColorSoChanged;
#endif
		Apply();
	}

	private void OnDisable()
	{
#if UNITY_EDITOR
		GameColorPalette.Changed -= Apply;
		ColorSO.Changed -= OnColorSoChanged;
#endif
	}

#if UNITY_EDITOR
	// ColorSO.value Inspector edits broadcast the per-asset event (same channel the HUD
	// previews use) — without this, widget tint only refreshed on palette REWIRING, not
	// on value tuning of the ColorSO asset itself.
	private void OnColorSoChanged(ColorSO changed) { Apply(); }
#endif

	private void OnValidate()
	{
		Apply();
	}

	/// <summary>Paints the resolved slot color onto the local SpriteRenderer and/or TMP_Text (whichever exist).</summary>
	public void Apply()
	{
		Color c = Resolve(slot);
		SpriteRenderer sr = GetComponent<SpriteRenderer>();
		if (sr != null) sr.color = c;
		TMP_Text tmp = GetComponent<TMP_Text>();
		if (tmp != null) tmp.color = c;
	}

	/// <summary>Slot → GameColorPalette static. Append rows here when the palette grows.</summary>
	public static Color Resolve(Slot slot)
	{
		switch (slot)
		{
			case Slot.ShopPanelBg: return GameColorPalette.ShopPanelBgColor;
			case Slot.TooltipBg: return GameColorPalette.TooltipBgColor;
			case Slot.TooltipText: return GameColorPalette.TooltipTextColor;
			case Slot.OwnerCard: return GameColorPalette.OwnerCardColor;
			case Slot.OwnerText: return GameColorPalette.OwnerTextColor;
			case Slot.CardShadow: return GameColorPalette.CardShadowColor;
			case Slot.CardFaceDim: return GameColorPalette.CardFaceDimColor;
			case Slot.CardTextSoft: return GameColorPalette.CardTextSoftColor;
			case Slot.IconNameLabel: return GameColorPalette.IconNameLabelColor;
			case Slot.HpNormalPlayer: return GameColorPalette.HpNormalPlayerColor;
			case Slot.SlotRecess: return GameColorPalette.SlotRecessColor;
			case Slot.ChipBg: return GameColorPalette.ChipBgColor;
			case Slot.ChipText: return GameColorPalette.ChipTextColor;
			case Slot.ButtonFace: return GameColorPalette.ButtonFaceColor;
			default: return Color.white;
		}
	}
}
