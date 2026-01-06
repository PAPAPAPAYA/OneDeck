using UnityEngine;

public class GameEventStorage : MonoBehaviour
{
	#region singleton
	public static GameEventStorage me;
	private void Awake()
	{
		me = this;
	}
	#endregion

	[Header("Card Specific")]
	public GameEvent onCardActivation;
	public GameEvent onCardBought;
	[Header("Card Not Specific (Linger)")]
	public GameEvent onEnemyTookDmg;
	public GameEvent onPlayerTookDmg;
	public GameEvent afterShuffle;
}