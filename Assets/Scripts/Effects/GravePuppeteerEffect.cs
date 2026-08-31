using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GRAVE_PUPPETEER engine (4.0 step-5): "让墓地1友方生物攻击；墓地无友方则埋葬1友方遗言卡".
/// Ruling 2026-08-30: A variant — the picked graveyard creature strikes WITHOUT leaving the
/// grave (uses its own AttackEffect via PerformAttackAs); random 1 target; creatures only.
/// When the grave holds no friendly creature, falls back to burying 1 friendly 遗言-tagged card.
/// Graveyard membership = index below the start card. The fallback runs through the
/// referenced BuryEffect so its pools/animation capture stay canonical.
/// </summary>
public class GravePuppeteerEffect : EffectScript
{
	[Tooltip("BuryEffect used for the no-creature fallback (configured on the same child GO)")]
	public BuryEffect fallbackBurier;

	public void RaiseGraveCreatureOrBuryFallback()
	{
		var deck = combatManager.combinedDeckZone;
		if (deck == null) return;

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

		var candidates = new List<CardScript>();
		for (int i = 0; i < startCardIndex; i++)
		{
			var cardObj = deck[i];
			if (cardObj == null) continue;
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.isPassive) continue;
			if (!cardScript.isCreature) continue;
			if (cardScript.myStatusRef != myCardScript.myStatusRef) continue;
			if (cardScript.myStatusRef == null) continue;
			candidates.Add(cardScript);
		}

		if (candidates.Count > 0)
		{
			var chosen = candidates[Random.Range(0, candidates.Count)];
			PerformAttackAs(chosen);
			return;
		}

		// Fallback: no friendly creature in the grave — bury 1 friendly 遗言 card.
		if (fallbackBurier != null)
		{
			fallbackBurier.BuryMyCardsWithTag(1);
		}
	}
}
