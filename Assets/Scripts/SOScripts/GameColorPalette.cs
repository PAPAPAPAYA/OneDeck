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
	public ColorSO ownerCardEdgeColor;
	public ColorSO opponentCardEdgeColor;
	public ColorSO ownerTextColor;
	public ColorSO opponentTextColor;
	public ColorSO infectedTint;
	public ColorSO powerTint;

	[Header("HP Bar / Numeric")]
	public ColorSO hpBarPlayer;
	public ColorSO hpBarEnemy;
	public ColorSO hpBarShadow;
	public ColorSO hpNormal;
	public ColorSO hpLow;
	public ColorSO hpZeroGray;
}
