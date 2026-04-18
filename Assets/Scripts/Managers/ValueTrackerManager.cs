using DefaultNamespace.SOScripts;
using UnityEngine;

public class ValueTrackerManager : MonoBehaviour
{
	public static ValueTrackerManager me;

	[Header("Tracker Refs")]
	public IntSO friendlyInGraveAmountRef;
	public IntSO hostileCursePowerCount;
	public IntSO totalPowerCountInDeckRef;
	public IntSO ownerCardCountInDeckRef;
	public IntSO enemyCardCountInDeckRef;
	public IntSO ownerCardsBuriedCountRef;
	public IntSO enemyCardsBuriedCountRef;
	public IntSO stagedOwnerRef;
	public IntSO stagedEnemyRef;

	[Header("Last Applied Status Effect")]
	public StatusEffectSO lastAppliedStatusEffectRef;
	public IntSO lastAppliedStatusEffectAmountRef;

	[Header("Curse Card Config")]
	[Tooltip("Cursed card type ID, used to count total Power on corresponding enemy cards")]
	public StringSO curseCardTypeId;

	private void Awake()
	{
		me = this;
	}

	/// <summary>
	/// Refresh all tracked values before effect execution
	/// </summary>
	public void UpdateAllTrackers()
	{
		UpdateFriendlyInGraveAmount();
		UpdateHostileCursePowerCount();
		UpdateTotalPowerCountInDeck();
		UpdateOwnerCardCountInDeck();
		UpdateEnemyCardCountInDeck();
	}

	/// <summary>
	/// Update FriendlyInGraveAmount: count friendly cards in combinedDeckZone below the Start Card (smaller index).
	/// These cards will be revealed after the Start Card and are considered in the "graveyard".
	/// </summary>
	private void UpdateFriendlyInGraveAmount()
	{
		if (friendlyInGraveAmountRef == null || CombatManager.Me == null) return;

		var deck = CombatManager.Me.combinedDeckZone;
		int startCardIndex = -1;

		// Find the Start Card's index
		for (int i = 0; i < deck.Count; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
			{
				startCardIndex = i;
				break;
			}
		}

		// If Start Card is not found, count is 0
		if (startCardIndex < 0)
		{
			friendlyInGraveAmountRef.value = 0;
			return;
		}

		// Count friendly cards with index smaller than Start Card
		int count = 0;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		for (int i = 0; i < startCardIndex; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == ownerStatus)
			{
				count++;
			}
		}

		friendlyInGraveAmountRef.value = count;
	}

	/// <summary>
	/// Update HostileCursePowerCount: sum of Power status effects on enemy cards with card type id matching curseCardTypeId.
	/// </summary>
	private void UpdateHostileCursePowerCount()
	{
		if (hostileCursePowerCount == null || CombatManager.Me == null) return;

		// If Cursed card type ID is not set, count is 0
		if (curseCardTypeId == null || string.IsNullOrEmpty(curseCardTypeId.value))
		{
			hostileCursePowerCount.value = 0;
			return;
		}

		var deck = CombatManager.Me.combinedDeckZone;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		int totalPower = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;

			// Check if it's a hostile card and card type id matches
			bool isHostileCard = cardScript.myStatusRef != ownerStatus;
			bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

			if (isHostileCard && isMatchingType)
			{
				// Count Power status effects on this card
				int powerCount = EnumStorage.GetStatusEffectCount(
					cardScript.myStatusEffects, 
					EnumStorage.StatusEffect.Power
				);
				totalPower += powerCount;
			}
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null)
			{
				bool isHostileCard = cardScript.myStatusRef != ownerStatus;
				bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

				if (isHostileCard && isMatchingType)
				{
					int powerCount = EnumStorage.GetStatusEffectCount(
						cardScript.myStatusEffects,
						EnumStorage.StatusEffect.Power
					);
					totalPower += powerCount;
				}
			}
		}

		hostileCursePowerCount.value = totalPower;
	}

	/// <summary>
	/// Updates TotalPowerCountInDeck: sums up all Power status effects on every card in combinedDeckZone.
	/// </summary>
	private void UpdateTotalPowerCountInDeck()
	{
		if (totalPowerCountInDeckRef == null || CombatManager.Me == null) return;

		var deck = CombatManager.Me.combinedDeckZone;
		int totalPower = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;

			int powerCount = EnumStorage.GetStatusEffectCount(
				cardScript.myStatusEffects,
				EnumStorage.StatusEffect.Power
			);
			totalPower += powerCount;
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null)
			{
				int powerCount = EnumStorage.GetStatusEffectCount(
					cardScript.myStatusEffects,
					EnumStorage.StatusEffect.Power
				);
				totalPower += powerCount;
			}
		}

		totalPowerCountInDeckRef.value = totalPower;
	}

	/// <summary>
	/// Updates OwnerCardCountInDeck: counts all cards in combinedDeckZone that belong to the owner player.
	/// </summary>
	private void UpdateOwnerCardCountInDeck()
	{
		if (ownerCardCountInDeckRef == null || CombatManager.Me == null) return;

		var deck = CombatManager.Me.combinedDeckZone;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		int count = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == ownerStatus)
			{
				count++;
			}
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == ownerStatus)
			{
				count++;
			}
		}

		ownerCardCountInDeckRef.value = count;
	}

	/// <summary>
	/// Updates EnemyCardCountInDeck: counts all cards in combinedDeckZone that belong to the enemy player.
	/// </summary>
	private void UpdateEnemyCardCountInDeck()
	{
		if (enemyCardCountInDeckRef == null || CombatManager.Me == null) return;

		var deck = CombatManager.Me.combinedDeckZone;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		int count = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef != ownerStatus)
			{
				count++;
			}
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef != ownerStatus)
			{
				count++;
			}
		}

		enemyCardCountInDeckRef.value = count;
	}
}
