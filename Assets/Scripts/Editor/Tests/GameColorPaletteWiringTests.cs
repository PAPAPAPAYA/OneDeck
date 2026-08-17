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
		Assert.NotNull(palette.floaterShadow, "floaterShadow unwired");
	}

	[Test]
	public void OverlayFields_AllWired()
	{
		GameColorPalette palette = LoadPalette();
		Assert.NotNull(palette.tooltipBg, "tooltipBg unwired");
		Assert.NotNull(palette.tooltipText, "tooltipText unwired");
		Assert.NotNull(palette.resultPanelBg, "resultPanelBg unwired");
		Assert.NotNull(palette.resultPanelText, "resultPanelText unwired");
	}

	[Test]
	public void CardFields_AllWired()
	{
		GameColorPalette palette = LoadPalette();
		Assert.NotNull(palette.ownerCardColor, "ownerCardColor unwired");
		Assert.NotNull(palette.opponentCardColor, "opponentCardColor unwired");
		Assert.NotNull(palette.ownerTextColor, "ownerTextColor unwired");
		Assert.NotNull(palette.opponentTextColor, "opponentTextColor unwired");
		Assert.NotNull(palette.startCardColor, "startCardColor unwired");
		Assert.NotNull(palette.startCardTextColor, "startCardTextColor unwired");
		Assert.NotNull(palette.infectedTint, "infectedTint unwired");
		Assert.NotNull(palette.powerTint, "powerTint unwired");
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

	// Golden: physical card colors are palette-authoritative since 2026-08-17
	// (CardPhysObjScript keeps no serialized ColorSO fields and reads these statics).
	[Test]
	public void CardFields_WiredToCanonicalAssets()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/GreyWhite.asset"), palette.ownerCardColor, "ownerCardColor must point to GreyWhite.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Red 1.asset"), palette.opponentCardColor, "opponentCardColor must point to Red 1.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Black.asset"), palette.ownerTextColor, "ownerTextColor must point to Black.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/OpponentTextColor.asset"), palette.opponentTextColor, "opponentTextColor must point to OpponentTextColor.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/StartCardColor.asset"), palette.startCardColor, "startCardColor must point to StartCardColor.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/StartCardTextColor.asset"), palette.startCardTextColor, "startCardTextColor must point to StartCardTextColor.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/InfectedTint.asset"), palette.infectedTint, "infectedTint must point to InfectedTint.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/PowerTint.asset"), palette.powerTint, "powerTint must point to PowerTint.asset");
	}

	// Golden: floater text is Navy on the player side; enemy side moved to
	// GreyWhite on 2026-08-17 (user decision; previously both were Navy from
	// 2026-08-15). Shadow is its own asset.
	[Test]
	public void FloaterFields_WiredToCanonicalAssets()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/Navy.asset"), palette.floaterPlayer, "floaterPlayer must point to Navy.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/GreyWhite.asset"), palette.floaterEnemy, "floaterEnemy must point to GreyWhite.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/FloaterShadow.asset"), palette.floaterShadow, "floaterShadow must point to FloaterShadow.asset");
	}

	// Golden: overlay panel colors (tag tooltip, result stats panel) — added 2026-08-17.
	[Test]
	public void OverlayFields_WiredToCanonicalAssets()
	{
		GameColorPalette palette = LoadPalette();
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/TooltipBg.asset"), palette.tooltipBg, "tooltipBg must point to TooltipBg.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/TooltipText.asset"), palette.tooltipText, "tooltipText must point to TooltipText.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/ResultPanelBg.asset"), palette.resultPanelBg, "resultPanelBg must point to ResultPanelBg.asset");
		Assert.AreEqual(LoadColor("Assets/SORefs/Colors/ResultPanelText.asset"), palette.resultPanelText, "resultPanelText must point to ResultPanelText.asset");
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
