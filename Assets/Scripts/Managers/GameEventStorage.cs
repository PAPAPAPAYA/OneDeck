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
	// if you want to invoke a specific card's event use RaiseSpecific()
	[Header("card specific")]
	public GameEvent onMeRevealed;
	public GameEvent onMeSentToGrave;
	public GameEvent onMeBought;
	// if you want ot invoke all cards with the same event, use Raise()
	[Header("any card")]
	public GameEvent onAnyCardRevealed;
	public GameEvent onAnyCardSentToGrave;
	public GameEvent onEnemyTookDmg;
	public GameEvent onPlayerTookDmg;
	public GameEvent onEnemyHealed;
	public GameEvent onPlayerHealed;
	public GameEvent afterShuffle;
}