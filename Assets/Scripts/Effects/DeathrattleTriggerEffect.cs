using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deathrattle chaining engine (4.0 step-5, LAST_RITES "遗言：触发所有在墓地友方的遗言").
/// Re-raises onMeBuried on each friendly card resting in the graveyard (index below the
/// start card), excluding self, passive cards and neutral/minion cards.
/// Round-trip protection comes free from the effect-chain loop guard: the re-raised
/// InvokeEffectEvent goes through EffectCanBeInvoked, which matches (card instance,
/// effect instance) pairs — so N=LAST_RITES cards in the grave each fire exactly once
/// per burial (A → B → C → A is blocked at the second call of A).
/// </summary>
public class DeathrattleTriggerEffect : EffectScript
{
	[Tooltip("Exclude this card itself from the triggered set")]
	public bool excludeSelf = true;

	public void TriggerAllGraveyardFriendlyDeathrattles()
	{
		var deck = combatManager.combinedDeckZone;
		if (deck == null) return;
		var storage = GameEventStorage.me;
		if (storage == null || storage.onMeBuried == null) return;

		int startCardIndex = -1;
		for (int i = 0; i < deck.Count; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
			{
				startCardIndex = i;
				break;
			}
		}
		if (startCardIndex < 0) return;

		for (int i = 0; i < startCardIndex; i++)
		{
			var cardObj = deck[i];
			if (cardObj == null) continue;
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.myStatusRef != myCardScript.myStatusRef) continue;
			if (excludeSelf && cardObj == myCard) continue;
			// Passive cards never reveal and carry no deathrattle — skip defensively.
			if (cardScript.isPassive) continue;

			storage.onMeBuried.RaiseSpecific(cardObj);
		}
	}

	/// <summary>
	/// RELIC_DEATH_KNELL: trigger the deathrattle of the FRIENDLY card that most recently
	/// revived (lastCardRevived — set by ReviveEffect before the awaken events raise, so the
	/// onFriendlyCardRevived reaction sees exactly the card that woke).
	/// Zero new components; loop guard applies as in TriggerAllGraveyardFriendlyDeathrattles.
	/// </summary>
	public void TriggerDeathrattleOfLastRevivedFriendly()
	{
		var cm = combatManager;
		if (cm == null || cm.lastCardRevived == null) return;
		var revived = cm.lastCardRevived;
		if (revived.myStatusRef != myCardScript.myStatusRef) return;
		if (CombatManager.ShouldSkipEffectProcessing(revived)) return;
		if (revived == myCardScript) return; // never re-trigger self-recursion
		var storage = GameEventStorage.me;
		if (storage == null || storage.onMeBuried == null) return;
		storage.onMeBuried.RaiseSpecific(revived.gameObject);
	}
}
