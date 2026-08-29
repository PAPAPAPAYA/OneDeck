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
	public GameEvent onAnyCardAttacked; // raised when any card performs an attack action (once per action, not per segment); self-attacks included
	public GameEvent onAnyFriendlyCardAttacked; // raised when any card performs a non-self attack action; delivered to the attacking card's faction (friendly attacker -> RaiseOwner, enemy attacker -> RaiseOpponent)
	
	[Header("minion related")]
	public GameEvent onFriendlyMinionAdded; // Triggered when a friendly minion is added to the deck
	
	[Header("exile related")]
	public GameEvent onFriendlyCardExiled; // Triggered when a friendly card is exiled
	public GameEvent onFriendlyFlyExiled; // Triggered when a friendly fly is exiled (including being consumed as minion cost)
	
	[Header("bury related")]
	public GameEvent onAnyCardBuried; // Triggered when any card is buried
	public GameEvent onFriendlyCardBuried; // Triggered when a friendly card is buried
	public GameEvent onMeBuried; // Triggered when this card is buried

	[Header("revive related (4.0)")]
	public GameEvent onMeRevived; // Triggered when this card is revived (awaken). Raised ONLY by ReviveEffect — never by Stage or bounce
	public GameEvent onAnyCardRevived; // Triggered when any card is revived
	public GameEvent onFriendlyCardRevived; // Triggered when a friendly card is revived
	public GameEvent onEnemyCardRevived; // Triggered when an enemy card is revived

	[Header("curse related")]
	public StringSO curseCardTypeID;
	public GameEvent onEnemyCurseCardRevealed; // Triggered when an enemy curse card is revealed
	public GameEvent onEnemyCurseCardGotPower; // Triggered when an enemy curse card gains Power
	public GameEvent onEnemyCurseCardGainedAttack; // Triggered when an enemy curse card gains permanent attack (attack-attribute redesign)

	[Header("status effect related")]
	public GameEvent onMeGotStatusEffect; // Triggered when this card gains any status effect

	[Header("power related")]
	public GameEvent onAnyCardGotPower; // Triggered when any card gains Power
	public GameEvent onMeGotPower; // Triggered when this card gains Power
	public GameEvent onFriendlyCardGotPower; // Triggered when a friendly card gains Power
	public GameEvent onEnemyCardGotPower; // Triggered when an enemy card gains Power

	[Header("attack attribute related")]
	public GameEvent onAnyCardGainedAttack; // Triggered when any card gains permanent attack (attack-attribute redesign)
	public GameEvent onMeGainedAttack; // Triggered when this card gains permanent attack
	public GameEvent onFriendlyCardGainedAttack; // Triggered when a friendly card gains permanent attack
	public GameEvent onEnemyCardGainedAttack; // Triggered when an enemy card gains permanent attack
}
