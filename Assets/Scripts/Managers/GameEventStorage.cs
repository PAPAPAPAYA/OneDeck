using DefaultNamespace.SOScripts;
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
	public GameEvent onMeBought; // used for effects in shop
	public GameEvent onThisTagResolverAttached; // used for effects that activate as soon as tag is given
	public GameEvent onMeStaged;

	// if you want ot invoke all cards with the same event, use Raise()
	[Header("any card")]
	public GameEvent onAnyCardRevealed;
	public GameEvent onHostileCardRevealed;
	public GameEvent onTheirPlayerTookDmg;
	public GameEvent onMyPlayerTookDmg;
	public GameEvent onTheirPlayerHealed;
	public GameEvent onMyPlayerHealed;
	public GameEvent onMyPlayerShieldUpped;
	public GameEvent onTheirPlayerShieldUpped;
	public GameEvent afterShuffle; // used for effects that put cards on top or bottom
	public GameEvent beforeRoundStart; // used for effects that activate once in a round
	
	[Header("minion related")]
	public GameEvent onFriendlyMinionAdded; // Triggered when a friendly minion is added to the deck
	
	[Header("exile related")]
	public GameEvent onFriendlyCardExiled; // Triggered when a friendly card is exiled
	public GameEvent onFriendlyFlyExiled; // Triggered when a friendly fly is exiled (including being consumed as minion cost)
	
	[Header("bury related")]
	public GameEvent onAnyCardBuried; // Triggered when any card is buried
	public GameEvent onFriendlyCardBuried; // Triggered when a friendly card is buried
	public GameEvent onMeBuried; // Triggered when this card is buried
	
	[Header("curse related")]
	public StringSO curseCardTypeID;
	public GameEvent onEnemyCurseCardRevealed; // Triggered when an enemy curse card is revealed
	public GameEvent onEnemyCurseCardGotPower; // Triggered when an enemy curse card gains Power

	[Header("power related")]
	public GameEvent onAnyCardGotPower; // Triggered when any card gains Power
	public GameEvent onMeGotPower; // Triggered when this card gains Power
	public GameEvent onFriendlyCardGotPower; // Triggered when a friendly card gains Power
	public GameEvent onEnemyCardGotPower; // Triggered when an enemy card gains Power
}
