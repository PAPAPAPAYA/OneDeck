using UnityEngine;

/// <summary>
/// Unified factory for card creation in combat.
/// Ensures logical cards and physical cards are created in sync,
/// eliminating "invisible card" bugs caused by manual synchronization.
///
/// Usage:
/// - For bulk deck setup (GatherDecks): use CreateLogicalCard + add to list.
///   Physical cards are created later by CombatUXManager.InstantiateAllPhysicalCards.
/// - For mid-combat card insertion: use SpawnCardToDeck (atomic logical + physical creation).
/// </summary>
public class CardFactory : MonoBehaviour
{
	#region Singleton
	public static CardFactory me;

	private void Awake()
	{
		me = this;
	}
	#endregion

	[Header("References")]
	[Tooltip("CombatManager reference — auto-fetched if null")]
	public CombatManager combatManager;

	private void OnEnable()
	{
		if (combatManager == null)
			combatManager = CombatManager.Me;
	}

	#region Logical Card Creation

	/// <summary>
	/// Create a logical card from prefab with ownership setup.
	/// Does NOT create physical card — intended for bulk deck setup (GatherDecks).
	/// </summary>
	/// <param name="prefab">Card prefab to instantiate</param>
	/// <param name="myStatus">Owner status reference</param>
	/// <param name="theirStatus">Opponent status reference</param>
	/// <param name="parent">Transform parent</param>
	/// <param name="customName">Optional custom name (null uses prefab name)</param>
	/// <returns>The instantiated logical card GameObject</returns>
	public GameObject CreateLogicalCard(GameObject prefab, PlayerStatusSO myStatus, PlayerStatusSO theirStatus,
		Transform parent, string customName = null)
	{
		if (prefab == null)
		{
			Debug.LogError("[CardFactory] prefab is null!");
			return null;
		}

		var instance = Instantiate(prefab, parent);
		instance.name = customName ?? instance.name.Replace("(Clone)", "");

		var cardScript = instance.GetComponent<CardScript>();
		if (cardScript != null)
		{
			cardScript.myStatusRef = myStatus;
			cardScript.theirStatusRef = theirStatus;
		}
		else
		{
			Debug.LogWarning("[CardFactory] Instantiated card has no CardScript component: " + prefab.name);
		}

		return instance;
	}

	/// <summary>
	/// Create a Start Card logical instance.
	/// Does NOT create physical card.
	/// </summary>
	public GameObject CreateStartCard(GameObject startCardPrefab, Transform parent)
	{
		var instance = CreateLogicalCard(startCardPrefab, null, null, parent, "Start Card");
		if (instance != null)
		{
			var cardScript = instance.GetComponent<CardScript>();
			if (cardScript != null)
			{
				cardScript.isStartCard = true;
			}
		}
		return instance;
	}

	#endregion

	#region Atomic Logical + Physical Creation

	/// <summary>
	/// Spawn a card into combat deck mid-combat.
	/// Atomically creates both logical card and physical card to prevent sync bugs.
	/// </summary>
	/// <param name="prefab">Card prefab to instantiate</param>
	/// <param name="myStatus">Owner status reference</param>
	/// <param name="theirStatus">Opponent status reference</param>
	/// <param name="parent">Transform parent for logical card</param>
	/// <param name="deckIndex">Index in combinedDeckZone to insert at (0 = bottom)</param>
	/// <param name="triggerMinionEvent">Whether to trigger onFriendlyMinionAdded event if card is a minion</param>
	/// <returns>The instantiated logical card GameObject</returns>
	public GameObject SpawnCardToDeck(GameObject prefab, PlayerStatusSO myStatus, PlayerStatusSO theirStatus,
		Transform parent, int deckIndex = 0, bool triggerMinionEvent = false)
	{
		// 1. Create logical card
		var cardInstance = CreateLogicalCard(prefab, myStatus, theirStatus, parent);
		if (cardInstance == null) return null;

		// 2. Add to combined deck
		if (combatManager != null)
		{
			combatManager.combinedDeckZone.Insert(deckIndex, cardInstance);
		}
		else
		{
			Debug.LogError("[CardFactory] combatManager is null — cannot add card to deck!");
			return cardInstance;
		}

		// 3. Create corresponding physical card (atomic — prevents "invisible card" bug)
		if (combatManager.visuals != null)
		{
			combatManager.visuals.AddCardToDeckVisual(cardInstance);
		}
		else
		{
			Debug.LogWarning("[CardFactory] visuals is null — physical card was not created for " + cardInstance.name);
		}

		// 4. Trigger minion event
		if (triggerMinionEvent)
		{
			var cardScript = cardInstance.GetComponent<CardScript>();
			if (cardScript != null && cardScript.isMinion)
			{
				GameEventStorage.me?.onFriendlyMinionAdded?.RaiseOwner();
			}
		}

		return cardInstance;
	}

	/// <summary>
	/// Convenience overload: spawn a card for a specific player.
	/// Automatically resolves myStatus/theirStatus from CombatManager.
	/// </summary>
	/// <param name="prefab">Card prefab to instantiate</param>
	/// <param name="targetPlayerStatus">The player this card belongs to</param>
	/// <param name="deckIndex">Index in combinedDeckZone to insert at</param>
	/// <param name="triggerMinionEvent">Whether to trigger onFriendlyMinionAdded event</param>
	/// <returns>The instantiated logical card GameObject</returns>
	public GameObject SpawnCardForPlayer(GameObject prefab, PlayerStatusSO targetPlayerStatus,
		int deckIndex = 0, bool triggerMinionEvent = false)
	{
		if (combatManager == null)
		{
			Debug.LogError("[CardFactory] combatManager is null!");
			return null;
		}

		Transform parent;
		PlayerStatusSO myStatus;
		PlayerStatusSO theirStatus;

		if (targetPlayerStatus == combatManager.ownerPlayerStatusRef)
		{
			parent = combatManager.playerDeckParent.transform;
			myStatus = combatManager.ownerPlayerStatusRef;
			theirStatus = combatManager.enemyPlayerStatusRef;
		}
		else
		{
			parent = combatManager.enemyDeckParent.transform;
			myStatus = combatManager.enemyPlayerStatusRef;
			theirStatus = combatManager.ownerPlayerStatusRef;
		}

		return SpawnCardToDeck(prefab, myStatus, theirStatus, parent, deckIndex, triggerMinionEvent);
	}

	#endregion
}
