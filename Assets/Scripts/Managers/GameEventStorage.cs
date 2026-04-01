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
	
	// if you want ot invoke all cards with the same event, use Raise()
	[Header("any card")]
	public GameEvent onAnyCardRevealed;
	public GameEvent onTheirPlayerTookDmg;
	public GameEvent onMyPlayerTookDmg;
	public GameEvent onTheirPlayerHealed;
	public GameEvent onMyPlayerHealed;
	public GameEvent onMyPlayerShieldUpped;
	public GameEvent onTheirPlayerShieldUpped;
	public GameEvent afterShuffle; // used for effects that put cards on top or bottom
	public GameEvent beforeRoundStart; // used for effects that activate once in a round
	
	[Header("minion related")]
	public GameEvent onFriendlyMinionAdded; // 当友方minion被添加到卡组中时触发
	
	[Header("exile related")]
	public GameEvent onFriendlyFlyExiled; // 当友方fly被放逐时触发（包括作为minion cost被消耗）
	
	[Header("bury related")]
	public GameEvent onAnyCardBuried; // 当任意可被置底时触发
	public GameEvent onFriendlyCardBuried; // 当友方卡被置底时触发
}
