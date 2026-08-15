using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode wiring guard for the HUD color single-source mapping: the six
/// GameColorPalette "HP Bar / Numeric" fields must be wired to the canonical
/// ColorSO assets and stay distinct, and the resolved GameColorPalette.<Name>Color
/// statics must return those assets' values. Prevents silent HUD recolors from
/// an accidental palette rewiring.
/// </summary>
public class GameColorPaletteWiringTests
{
	private const string PalettePath = "Assets/Resources/GameColorPalette.asset";

	private static GameColorPalette LoadPalette()
	{
		GameColorPalette palette = AssetDatabase.LoadAssetAtPath<GameColorPalette>(PalettePath);
		Assert.NotNull(palette, "GameColorPalette asset missing at " + PalettePath);
		return palette;
	}

	private static ColorSO LoadColor(string path)
	{
		ColorSO so = AssetDatabase.LoadAssetAtPath<ColorSO>(path);
		Assert.NotNull(so, "ColorSO asset missing at " + path);
		return so;
	}

	[Test]
	public void HpFields_AllWired()
	{
		GameColorPalette palette = LoadPalette();
		Assert.NotNull(palette.hpNormalPlayer, "hpNormalPlayer unwired");
		Assert.NotNull(palette.hpNormalEnemy, "hpNormalEnemy unwired");
		Assert.NotNull(palette.hpLow, "hpLow unwired");
		Assert.NotNull(palette.hpZeroGray, "hpZeroGray unwired");
		Assert.NotNull(palette.hpBarPlayer, "hpBarPlayer unwired");
		Assert.NotNull(palette.hpBarEnemy, "hpBarEnemy unwired");
		Assert.NotNull(palette.hpBarShadow, "hpBarShadow unwired");
	}

	[Test]
	public void FloaterFields_AllWired()
	{
		GameColorPalette palette = LoadPalette();
		Assert.NotNull(palette.floaterPlayer, "floaterPlayer unwired");
		Assert.NotNull(palette.floaterEnemy, "floaterEnemy unwired");
	}

	[Test]
	public void HpFields_DistinctAssets()
	{
		GameColorPalette palette = LoadPalette();
		// Intentional shares: the floater fields both point to Navy, and
		// hpNormalEnemy shares GreyWhite 2 with hpBarPlayer — so only the
		// remaining fields are pinned distinct.
		var refs = new[] { palette.hpNormalPlayer, palette.hpNormalEnemy, palette.hpLow, palette.hpZeroGray, palette.hpBarEnemy, palette.hpBarShadow };
		CollectionAssert.AllItemsAreUnique(refs, "Two HUD color fields share one ColorSO asset; rewire before it changes both HUD elements");
		Assert.AreNotEqual(palette.hpNormalPlayer, palette.hpNormalEnemy, "Player and enemy numeric colors must differ");
	}

	// Golden: pins the canonical HUD wiring so a rewiring is a deliberate,
	// test-visible change (was: hpNormal -> Navy, hpBarEnemy -> Red 2, both
	// drifted from the scene wiring; unified 2026-08-15).
	[Test]
	public void HpFields_WiredToCanonicalAssets()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Navy.asset"), palette.hpNormalPlayer, "hpNormalPlayer must point to Navy.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/GreyWhite 2.asset"), palette.hpNormalEnemy, "hpNormalEnemy must point to GreyWhite 2.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/OpponentCardColor.asset"), palette.hpLow, "hpLow must point to OpponentCardColor.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/HPZeroGray.asset"), palette.hpZeroGray, "hpZeroGray must point to HPZeroGray.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/GreyWhite 2.asset"), palette.hpBarPlayer, "hpBarPlayer must point to GreyWhite 2.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/OpponentCardColor 2.asset"), palette.hpBarEnemy, "hpBarEnemy must point to OpponentCardColor 2.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Shadow.asset"), palette.hpBarShadow, "hpBarShadow must point to Shadow.asset");
	}

	// Golden: floater color is Navy for both sides (user decision 2026-08-15;
	// previously the scene wired both to Black and the palette pointed at
	// FloaterPlayer/FloaterEnemy, neither matching the other).
	[Test]
	public void FloaterFields_WiredToCanonicalAssets()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Navy.asset"), palette.floaterPlayer, "floaterPlayer must point to Navy.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Navy.asset"), palette.floaterEnemy, "floaterEnemy must point to Navy.asset");
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
	}
}
