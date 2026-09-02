using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for StatusEffectGiverEffect.onlyTargetEnemyDamagingCards — the damaging filter reads
/// damage capability (IsCreature || HasAttackAttribute, 2026-09-02 card-type split) instead of
/// the legacy DecreaseTheirHp method-name scan — and the AttackGiverEffect receive gate regression.
/// </summary>
public class EnemyDamagingTargetFilterTests : HeadlessCombatTestFixture
{
	private int CountStatusEffect(GameObject card, EnumStorage.StatusEffect effect)
	{
		int count = 0;
		foreach (var e in card.GetComponent<CardScript>().myStatusEffects)
		{
			if (e == effect) count++;
		}
		return count;
	}

	#region onlyTargetEnemyDamagingCards Filter (damaging = creature or holds attack)

	[Test]
	public void GiveStatusEffectToLastXCards_FilterOn_OnlyCreaturesReceivePower()
	{
		var giverCard = CreateCard(true, "Giver");
		var nonCreature = CreateCard(true, "NonCreature");
		var creature = CreateCard(true, "Creature");
		creature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		CombatManager.combinedDeckZone.Add(nonCreature);
		CombatManager.combinedDeckZone.Add(creature);
		CombatManager.combinedDeckZone.Add(giverCard);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.onlyTargetEnemyDamagingCards = true;
		giver.lastXCardsCount = 5;
		giver.statusEffectLayerCount = 1;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToLastXCards();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CountStatusEffect(creature, EnumStorage.StatusEffect.Power),
			"Creature-type card should receive Power");
		Assert.AreEqual(0, CountStatusEffect(nonCreature, EnumStorage.StatusEffect.Power),
			"Non-creature should be skipped when the filter is on");
	}

	[Test]
	public void GiveStatusEffectToLastXCards_FilterOff_AllCardsReceivePower()
	{
		var giverCard = CreateCard(true, "Giver");
		var nonCreature = CreateCard(true, "NonCreature");
		var creature = CreateCard(true, "Creature");
		creature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		CombatManager.combinedDeckZone.Add(nonCreature);
		CombatManager.combinedDeckZone.Add(creature);
		CombatManager.combinedDeckZone.Add(giverCard);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.onlyTargetEnemyDamagingCards = false;
		giver.lastXCardsCount = 5;
		giver.statusEffectLayerCount = 1;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToLastXCards();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CountStatusEffect(creature, EnumStorage.StatusEffect.Power),
			"Creature should receive Power");
		Assert.AreEqual(1, CountStatusEffect(nonCreature, EnumStorage.StatusEffect.Power),
			"Non-creature should also receive Power when the filter is off");
	}

	#endregion

	#region AttackGiver receive gate (regression: 强化 target pool blocked by statusEffectToGive=None)

	[Test]
	public void AttackGiver_GiveAttackToXFriendly_IgnoresStatusEffectToGiveNone()
	{
		var giverCard = CreateCard(true, "Giver");
		var target = CreateCard(true, "Target");
		CombatManager.combinedDeckZone.Add(target);
		CombatManager.combinedDeckZone.Add(giverCard);

		var giver = CreateEffect<AttackGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.None; // legacy gate must not block attack granting
		giver.onlyTargetEnemyDamagingCards = false;
		giver.xFriendlyCount = 1;
		giver.yFriendlyLayerCount = 1;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveAttackToXFriendly();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, target.GetComponent<CardScript>().GetAttack(),
			"Friendly target should gain attack even when statusEffectToGive is None");
	}

	#endregion
}
