using UnityEngine;

/// <summary>
/// Central palette aggregating all named ColorSO assets.
/// Access via the static lazy-loaded singleton: GameColorPalette.Me
/// (asset must live at Assets/Resources/GameColorPalette.asset).
/// </summary>
[CreateAssetMenu(fileName = "GameColorPalette", menuName = "SORefs/GameColorPalette")]
public class GameColorPalette : ScriptableObject
{
	private static GameColorPalette _me;

	/// <summary>Lazy-loaded singleton. Loads "GameColorPalette" from Resources on first access.</summary>
	public static GameColorPalette Me
	{
		get
		{
			if (_me == null)
			{
				_me = Resources.Load<GameColorPalette>("GameColorPalette");
				if (_me == null)
				{
					Debug.LogError("[GameColorPalette] No GameColorPalette asset found in Resources. Expected at Assets/Resources/GameColorPalette.asset");
				}
			}
			return _me;
		}
	}

	[Header("Log / Rich Text")]
	public ColorSO friendly;	// #87CEEB
	public ColorSO enemy;		// orange
	public ColorSO damage;		// red
	public ColorSO heal;		// #90EE90
	public ColorSO shield;		// grey
	public ColorSO highlight;	// yellow (numbers, price)

	[Header("Physical Card")]
	public ColorSO ownerCardColor;
	public ColorSO opponentCardColor;
	public ColorSO ownerTextColor;
	public ColorSO opponentTextColor;
	public ColorSO startCardColor;
	public ColorSO startCardTextColor;
	public ColorSO infectedTint;
	public ColorSO powerTint;

	[Header("HP Bar / Numeric")]
	public ColorSO hpBarPlayer;
	public ColorSO hpBarEnemy;
	public ColorSO hpBarShadow;
	public ColorSO hpNormalPlayer;
	public ColorSO hpNormalEnemy;
	public ColorSO hpLow;
	public ColorSO hpZeroGray;

	[Header("Damage Floater")]
	public ColorSO floaterPlayer;
	public ColorSO floaterEnemy;
	public ColorSO floaterShadow;

	[Header("Overlay Panels")]
	public ColorSO tooltipBg;
	public ColorSO tooltipText;
	public ColorSO resultPanelBg;
	public ColorSO resultPanelText;

	// Resolved HUD colors — the single source the HUD components (HPNumericDisplay,
	// HPNumericDisplayHorizontal, CombatHPBarPresenter) read from; they keep no
	// serialized color fields. White fallback keeps a miswired palette visible.
	public static Color HpNormalPlayerColor => Me != null && Me.hpNormalPlayer != null ? Me.hpNormalPlayer.value : Color.white;
	public static Color HpNormalEnemyColor => Me != null && Me.hpNormalEnemy != null ? Me.hpNormalEnemy.value : Color.white;
	public static Color HpLowColor => Me != null && Me.hpLow != null ? Me.hpLow.value : Color.white;
	public static Color HpZeroGrayColor => Me != null && Me.hpZeroGray != null ? Me.hpZeroGray.value : Color.white;
	public static Color HpBarPlayerColor => Me != null && Me.hpBarPlayer != null ? Me.hpBarPlayer.value : Color.white;
	public static Color HpBarEnemyColor => Me != null && Me.hpBarEnemy != null ? Me.hpBarEnemy.value : Color.white;
	public static Color HpBarShadowColor => Me != null && Me.hpBarShadow != null ? Me.hpBarShadow.value : Color.white;

	// Damage floater colors ("Damage Floater" group) — same palette-authoritative
	// pattern as the HP group; both sides share one asset by design.
	public static Color FloaterPlayerColor => Me != null && Me.floaterPlayer != null ? Me.floaterPlayer.value : Color.white;
	public static Color FloaterEnemyColor => Me != null && Me.floaterEnemy != null ? Me.floaterEnemy.value : Color.white;
	public static Color FloaterShadowColor => Me != null && Me.floaterShadow != null ? Me.floaterShadow.value : Color.white;

	// Overlay panel colors ("Overlay Panels" group) — tag tooltip and result stats panel.
	public static Color TooltipBgColor => Me != null && Me.tooltipBg != null ? Me.tooltipBg.value : Color.white;
	public static Color TooltipTextColor => Me != null && Me.tooltipText != null ? Me.tooltipText.value : Color.white;
	public static Color ResultPanelBgColor => Me != null && Me.resultPanelBg != null ? Me.resultPanelBg.value : Color.white;
	public static Color ResultPanelTextColor => Me != null && Me.resultPanelText != null ? Me.resultPanelText.value : Color.white;

	// Physical card colors — the single source CardPhysObjScript reads from (it keeps
	// no serialized color fields). White fallback keeps a miswired palette visible.
	public static Color OwnerCardColor => Me != null && Me.ownerCardColor != null ? Me.ownerCardColor.value : Color.white;
	public static Color OpponentCardColor => Me != null && Me.opponentCardColor != null ? Me.opponentCardColor.value : Color.white;
	public static Color OwnerTextColor => Me != null && Me.ownerTextColor != null ? Me.ownerTextColor.value : Color.white;
	public static Color OpponentTextColor => Me != null && Me.opponentTextColor != null ? Me.opponentTextColor.value : Color.white;
	public static Color StartCardColor => Me != null && Me.startCardColor != null ? Me.startCardColor.value : Color.white;
	public static Color StartCardTextColor => Me != null && Me.startCardTextColor != null ? Me.startCardTextColor.value : Color.white;
	public static Color InfectedTintColor => Me != null && Me.infectedTint != null ? Me.infectedTint.value : Color.white;
	public static Color PowerTintColor => Me != null && Me.powerTint != null ? Me.powerTint.value : Color.white;

#if UNITY_EDITOR
	/// <summary>
	/// Raised in the editor when this asset's serialized data changes (field
	/// rewiring, undo/redo). HUD edit-mode previews subscribe to live-update.
	/// </summary>
	public static event System.Action Changed;

	private void OnValidate()
	{
		if (Changed != null)
		{
			try
			{
				Changed();
			}
			catch (System.Exception e)
			{
				// A dead subscriber (stale delegate to a destroyed object) must
				// not break the live-preview chain for the remaining subscribers.
				Debug.LogError("[GameColorPalette] Palette change broadcast failed: " + e);
			}
		}
	}
#endif
}
