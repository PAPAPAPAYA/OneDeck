using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode wiring guard for the GameColorPalette single-source mapping.
///
/// The palette is the single source for all game colors (components keep no
/// serialized color fields and read GameColorPalette statics), so exact
/// field-to-asset mappings are free to iterate. These tests therefore pin only
/// color-agnostic invariants:
///   1. every ColorSO field is wired to an asset in the canonical Colors folder;
///   2. the resolved GameColorPalette.<Name>Color statics return wired values;
///   3. fields that must stay visually distinguishable (player vs enemy, start
///      card vs faction cards) never resolve to the same color.
/// Color value tweaks and asset swaps never require touching this file.
/// Golden field->asset pinning removed 2026-08-19 — it broke on every color
/// iteration (overlay/text fields were unified onto Navy/GreyWhite and the
/// goldens went stale), so rewiring is now guarded structurally instead.
/// </summary>
public class GameColorPaletteWiringTests
{
	private const string PalettePath = "Assets/Resources/GameColorPalette.asset";
	private const string ColorsFolder = "Assets/SORefs/Colors/";

	private static GameColorPalette LoadPalette()
	{
		GameColorPalette palette = AssetDatabase.LoadAssetAtPath<GameColorPalette>(PalettePath);
		Assert.NotNull(palette, "GameColorPalette asset missing at " + PalettePath);
		return palette;
	}

	private static IEnumerable<FieldInfo> ColorFields()
	{
		foreach (FieldInfo field in typeof(GameColorPalette).GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			if (field.FieldType == typeof(ColorSO))
			{
				yield return field;
			}
		}
	}

	// Reflection-driven so newly added palette fields are covered automatically.
	[Test]
	public void AllColorFields_Wired()
	{
		GameColorPalette palette = LoadPalette();
		List<string> missing = new List<string>();
		foreach (FieldInfo field in ColorFields())
		{
			if (field.GetValue(palette) == null)
			{
				missing.Add(field.Name);
			}
		}
		Assert.IsEmpty(missing, "Unwired palette fields (wire them in " + PalettePath + "): " + string.Join(", ", missing));
	}

	[Test]
	public void WiredColors_LiveInCanonicalFolder()
	{
		GameColorPalette palette = LoadPalette();
		List<string> misplaced = new List<string>();
		foreach (FieldInfo field in ColorFields())
		{
			ColorSO so = (ColorSO)field.GetValue(palette);
			if (so != null)
			{
				string path = AssetDatabase.GetAssetPath(so);
				if (!path.StartsWith(ColorsFolder))
				{
					misplaced.Add(field.Name + " -> " + path);
				}
			}
		}
		Assert.IsEmpty(misplaced, "Palette fields must reference ColorSO assets under " + ColorsFolder + ": " + string.Join(", ", misplaced));
	}

	// HP numerics/bars may share assets with other groups by design (hpNormalEnemy
	// shares GreyWhite 2 with hpBarPlayer), so only the fields that must stay
	// distinct from one another are pinned.
	[Test]
	public void HpFields_DistinctAssets()
	{
		GameColorPalette palette = LoadPalette();
		var refs = new[] { palette.hpNormalPlayer, palette.hpNormalEnemy, palette.hpLow, palette.hpZeroGray, palette.hpBarEnemy, palette.hpBarShadow };
		CollectionAssert.AllItemsAreUnique(refs, "Two HUD color fields share one ColorSO asset; rewire before it changes both HUD elements");
	}

	// Value-based on purpose: two distinct assets with an identical value are as
	// unreadable as one shared asset, so reference checks are not enough here.
	[Test]
	public void PlayerEnemyPairs_Differ()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreNotEqual(palette.hpNormalPlayer.value, palette.hpNormalEnemy.value, "Player and enemy HP numeric colors must differ");
		Assert.AreNotEqual(palette.hpBarPlayer.value, palette.hpBarEnemy.value, "Player and enemy HP bar colors must differ");
		Assert.AreNotEqual(palette.floaterPlayer.value, palette.floaterEnemy.value, "Player and enemy damage floater colors must differ");
		Assert.AreNotEqual(palette.ownerCardColor.value, palette.opponentCardColor.value, "Owner and opponent card colors must differ");
		Assert.AreNotEqual(palette.ownerTextColor.value, palette.opponentTextColor.value, "Owner and opponent card text colors must differ");
	}

	[Test]
	public void StartCard_DistinctFromFactionCards()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreNotEqual(palette.startCardColor.value, palette.ownerCardColor.value, "Start card must stand out from owner cards");
		Assert.AreNotEqual(palette.startCardColor.value, palette.opponentCardColor.value, "Start card must stand out from opponent cards");
	}

	// Resolved statics follow the wired assets.
	[Test]
	public void ResolvedColors_MatchWiredAssets()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreEqual(palette.hpNormalPlayer.value, GameColorPalette.HpNormalPlayerColor);
		Assert.AreEqual(palette.hpNormalEnemy.value, GameColorPalette.HpNormalEnemyColor);
		Assert.AreEqual(palette.hpLow.value, GameColorPalette.HpLowColor);
		Assert.AreEqual(palette.hpZeroGray.value, GameColorPalette.HpZeroGrayColor);
		Assert.AreEqual(palette.hpBarPlayer.value, GameColorPalette.HpBarPlayerColor);
		Assert.AreEqual(palette.hpBarEnemy.value, GameColorPalette.HpBarEnemyColor);
		Assert.AreEqual(palette.hpBarShadow.value, GameColorPalette.HpBarShadowColor);
		Assert.AreEqual(palette.floaterPlayer.value, GameColorPalette.FloaterPlayerColor);
		Assert.AreEqual(palette.floaterEnemy.value, GameColorPalette.FloaterEnemyColor);
		Assert.AreEqual(palette.floaterShadow.value, GameColorPalette.FloaterShadowColor);
		Assert.AreEqual(palette.tooltipBg.value, GameColorPalette.TooltipBgColor);
		Assert.AreEqual(palette.tooltipText.value, GameColorPalette.TooltipTextColor);
		Assert.AreEqual(palette.resultPanelBg.value, GameColorPalette.ResultPanelBgColor);
		Assert.AreEqual(palette.resultPanelText.value, GameColorPalette.ResultPanelTextColor);
		Assert.AreEqual(palette.ownerCardColor.value, GameColorPalette.OwnerCardColor);
		Assert.AreEqual(palette.opponentCardColor.value, GameColorPalette.OpponentCardColor);
		Assert.AreEqual(palette.ownerTextColor.value, GameColorPalette.OwnerTextColor);
		Assert.AreEqual(palette.opponentTextColor.value, GameColorPalette.OpponentTextColor);
		Assert.AreEqual(palette.startCardColor.value, GameColorPalette.StartCardColor);
		Assert.AreEqual(palette.startCardTextColor.value, GameColorPalette.StartCardTextColor);
		Assert.AreEqual(palette.infectedTint.value, GameColorPalette.InfectedTintColor);
		Assert.AreEqual(palette.powerTint.value, GameColorPalette.PowerTintColor);
	}
}
