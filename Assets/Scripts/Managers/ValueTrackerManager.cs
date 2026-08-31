using DefaultNamespace.SOScripts;
using UnityEngine;

public class ValueTrackerManager : MonoBehaviour
{
	public static ValueTrackerManager me;

	[Header("InGraveCount")]
	public IntSO ownerInGraveAmountRef;
	public IntSO enemyInGraveAmountRef;
	[Header("CursePowerCount")]
	public IntSO ownerCursePowerCount;
	public IntSO enemyCursePowerCount;
	[Header("TotalPowerCount")]
	public IntSO totalPowerCountInDeckRef;
	[Header("CardCountInDeck")]
	public IntSO ownerCardCountInDeckRef;
	public IntSO enemyCardCountInDeckRef;
	[Header("CardBuriedCount")]
	public IntSO ownerCardsBuriedCountRef;
	public IntSO enemyCardsBuriedCountRef;
	[Header("StagedCard")]
	public IntSO stagedOwnerRef;
	public IntSO stagedEnemyRef;
	[Header("ReviveCount (4.0)")]
	public IntSO ownerRevivedCountRef; // cumulative across the combat
	public IntSO enemyRevivedCountRef; // cumulative across the combat
	public IntSO ownerRevivedCountThisRoundRef; // reset at every round start (HandleNewRoundStart)
	public IntSO enemyRevivedCountThisRoundRef; // reset at every round start (HandleNewRoundStart)

	[Header("CreatureAttackTimesAura (4.0 E7)")]
	[Tooltip("Faction-level per-round creature attack-times aura (BATTLE_HORN). Read by CardScript.GetAttackTimes for isCreature cards; reset at every round start.")]
	public IntSO creatureAttackTimesAuraOwnerThisRoundRef;
	public IntSO creatureAttackTimesAuraEnemyThisRoundRef;

	[Header("CreaturesBuriedBySide (4.0 E4)")]
	[Tooltip("Per-round count of creature burials CAUSED by each side (RELIC_TALLY). Causer-based — enemy-caused burials never count for my side; my own sacrificed creatures do. Reset at every round start.")]
	public IntSO creaturesBuriedByOwnerThisRoundRef;
	public IntSO creaturesBuriedByEnemyThisRoundRef;

	[Header("GraveCreatureAura (4.0 step-5)")]
	[Tooltip("Per-round attack bonus for friendly creatures resting in the graveyard (RELIC_GRAVE_LORD). Set by the passive carrier on each shuffle; reset at every round start.")]
	public IntSO graveCreatureAuraOwnerThisRoundRef;
	public IntSO graveCreatureAuraEnemyThisRoundRef;

	[Header("FriendlyExiledBySide (4.0 step-5)")]
	[Tooltip("Per-round count of self-side exiles CAUSED by each side (RIFT_REAPER). Reset at every round start.")]
	public IntSO friendlyExiledByOwnerThisRoundRef;
	public IntSO friendlyExiledByEnemyThisRoundRef;

	[Header("CurseAttackOverride (4.0 step-5)")]
	[Tooltip("Armed by RELIC_GRAVE_CURSE each shuffle: while > 0, the side's ENEMY curse cards report 墓地友方卡数量 as their attack (覆盖式 — permanent/given attack on the curse is masked).")]
	public IntSO curseAttackOverrideOwnerThisRoundRef;
	public IntSO curseAttackOverrideEnemyThisRoundRef;

	[Header("BloodPact (4.0 step-5)")]
	[Tooltip("Armed by RELIC_BLOOD_PACT each shuffle: while > 0, the side's friendly attacks on the ENEMY player deal no damage and instead enhance the enemy curse by the same amount (self-damage unaffected).")]
	public IntSO bloodPactOwnerThisRoundRef;
	public IntSO bloodPactEnemyThisRoundRef;

	[Header("Last Applied Status Effect")]
	public StatusEffectSO lastAppliedStatusEffectRef;
	public IntSO lastAppliedStatusEffectAmountRef;

	[Header("Curse Card Config")]
	[Tooltip("Cursed card type ID, used to count total Power on corresponding enemy and owner cards")]
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
		UpdateOwnerInGraveAmount();
		UpdateEnemyInGraveAmount();
		UpdateEnemyCursePowerCount();
		UpdateOwnerCursePowerCount();
		UpdateTotalPowerCountInDeck();
		UpdateOwnerCardCountInDeck();
		UpdateEnemyCardCountInDeck();
	}

	/// <summary>
	/// Update OwnerInGraveAmount: count owner cards in combinedDeckZone below the Start Card (smaller index).
	/// These cards will be revealed after the Start Card and are considered in the "graveyard".
	/// </summary>
	private void UpdateOwnerInGraveAmount()
	{
		if (ownerInGraveAmountRef == null || CombatManager.Me == null) return;

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
			ownerInGraveAmountRef.value = 0;
			return;
		}

		// Count owner cards with index smaller than Start Card
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

		ownerInGraveAmountRef.value = count;
	}

	/// <summary>
	/// Update EnemyInGraveAmount: count enemy cards in combinedDeckZone below the Start Card (smaller index).
	/// These cards will be revealed after the Start Card and are considered in the "graveyard".
	/// </summary>
	private void UpdateEnemyInGraveAmount()
	{
		if (enemyInGraveAmountRef == null || CombatManager.Me == null) return;

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
			enemyInGraveAmountRef.value = 0;
			return;
		}

		// Count enemy cards with index smaller than Start Card
		int count = 0;
		var enemyStatus = CombatManager.Me.enemyPlayerStatusRef;
		for (int i = 0; i < startCardIndex; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == enemyStatus)
			{
				count++;
			}
		}

		enemyInGraveAmountRef.value = count;
	}

	/// <summary>
	/// Update EnemyCursePowerCount: sum of permanent attack on enemy cards with card type id matching curseCardTypeId
	/// (attack-attribute redesign; formerly Power status effect stacks).
	/// </summary>
	private void UpdateEnemyCursePowerCount()
	{
		if (enemyCursePowerCount == null || CombatManager.Me == null) return;

		// If Cursed card type ID is not set, count is 0
		if (curseCardTypeId == null || string.IsNullOrEmpty(curseCardTypeId.value))
		{
			enemyCursePowerCount.value = 0;
			return;
		}

		var deck = CombatManager.Me.combinedDeckZone;
		var enemyStatus = CombatManager.Me.enemyPlayerStatusRef;
		int totalAttack = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;

			// Check if it's an enemy card and card type id matches
			bool isEnemyCard = cardScript.myStatusRef == enemyStatus;
			bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

			if (isEnemyCard && isMatchingType)
			{
				totalAttack += cardScript.GetAttack();
			}
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null)
			{
				bool isEnemyCard = cardScript.myStatusRef == enemyStatus;
				bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

				if (isEnemyCard && isMatchingType)
				{
					totalAttack += cardScript.GetAttack();
				}
			}
		}

		enemyCursePowerCount.value = totalAttack;
	}

	/// <summary>
	/// Update OwnerCursePowerCount: sum of permanent attack on owner cards with card type id matching curseCardTypeId
	/// (attack-attribute redesign; formerly Power status effect stacks).
	/// </summary>
	private void UpdateOwnerCursePowerCount()
	{
		if (ownerCursePowerCount == null || CombatManager.Me == null) return;

		// If Cursed card type ID is not set, count is 0
		if (curseCardTypeId == null || string.IsNullOrEmpty(curseCardTypeId.value))
		{
			ownerCursePowerCount.value = 0;
			return;
		}

		var deck = CombatManager.Me.combinedDeckZone;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		int totalAttack = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;

			// Check if it's an owner card and card type id matches
			bool isOwnerCard = cardScript.myStatusRef == ownerStatus;
			bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

			if (isOwnerCard && isMatchingType)
			{
				totalAttack += cardScript.GetAttack();
			}
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null)
			{
				bool isOwnerCard = cardScript.myStatusRef == ownerStatus;
				bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

				if (isOwnerCard && isMatchingType)
				{
					totalAttack += cardScript.GetAttack();
				}
			}
		}

		ownerCursePowerCount.value = totalAttack;
	}

	/// <summary>
	/// Updates TotalPowerCountInDeck: sums up all permanent attack on every card in combinedDeckZone
	/// (attack-attribute redesign; formerly Power status effect stacks).
	/// </summary>
	private void UpdateTotalPowerCountInDeck()
	{
		if (totalPowerCountInDeckRef == null || CombatManager.Me == null) return;

		var deck = CombatManager.Me.combinedDeckZone;
		int totalAttack = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;

			totalAttack += cardScript.GetAttack();
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null)
			{
				totalAttack += cardScript.GetAttack();
			}
		}

		totalPowerCountInDeckRef.value = totalAttack;
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
		var enemyStatus = CombatManager.Me.enemyPlayerStatusRef;
		int count = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == enemyStatus)
			{
				count++;
			}
		}

		// Include revealZone
		var revealZone = CombatManager.Me.revealZone;
		if (revealZone != null)
		{
			var cardScript = revealZone.GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == enemyStatus)
			{
				count++;
			}
		}

		enemyCardCountInDeckRef.value = count;
	}
}
