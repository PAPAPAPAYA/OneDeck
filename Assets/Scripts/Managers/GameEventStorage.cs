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
	public GameEvent onMeBought; // used for effects in shop
	public GameEvent onThisTagResolverAttached; // used for effects that activate as soon as tag is given
	
	// if you want ot invoke all cards with the same event, use Raise()
	[Header("any card")]
	public GameEvent onAnyCardRevealed;
	public GameEvent onAnyCardSentToGrave;
	public GameEvent onTheirPlayerTookDmg;
	public GameEvent onMyPlayerTookDmg;
	public GameEvent onTheirPlayerHealed;
	public GameEvent onMyPlayerHealed;
	public GameEvent onMyPlayerShieldUpped;
	public GameEvent onTheirPlayerShieldUpped;
	public GameEvent afterShuffle; // used for effects that put cards on top or bottom
	public GameEvent beforeRoundStart; // used for effects that activate once in a round
	public GameEvent onAnyCardRevived; // when any card is moved from grave to deck
}