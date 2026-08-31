using UnityEngine;

/// <summary>
/// DEATHBED_GRANT engine (4.0 step-5): "被动：友方生物被埋葬时：该友方生物攻击".
/// Reads CombatManager.lastCardBuried (set by BuryEffect right before every bury event raise)
/// and, when the buried card is a friendly creature, performs THE BURIED CARD's own attack
/// — the card's enhanced attack value is what lands, closing the 强化×埋葬 loop
/// (an enhanced creature buried mid-round still strikes with its grown attack).
/// The attack runs through the buried card's own AttackEffect instance, so damage events,
/// animation capture and per-card stats all attribute to the victim, and the effect-chain
/// loop guard still applies (same card + same effect instance cannot repeat in one chain).
/// </summary>
public class BuriedCreatureAttackEffect : EffectScript
{
	public void AttackLastBuriedFriendlyCreature()
	{
		if (combatManager == null || combatManager.lastCardBuried == null) return;
		var buriedScript = combatManager.lastCardBuried;

		// Friendly creature only (defensive: onFriendlyCardBuried is already victim-faction
		// delivered, but the context field is shared across events).
		if (buriedScript.myStatusRef != myCardScript.myStatusRef) return;
		if (!buriedScript.isCreature) return;
		if (CombatManager.ShouldSkipEffectProcessing(buriedScript)) return;
		// Neutral victims never belong to a side.
		if (buriedScript.myStatusRef == null) return;

		PerformAttackAs(buriedScript);
	}
}
