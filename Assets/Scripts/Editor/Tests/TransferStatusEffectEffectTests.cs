using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

public class TransferStatusEffectEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void TransferAllStatusEffectToHostileCurse_TransfersFromFriendlyToCurse()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemyCurse = CreateCard(false, "EnemyCurse");
		enemyCurse.GetComponent<CardScript>().cardTypeID = "curse_type";
		var otherEnemy = CreateCard(false, "OtherEnemy");

		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(enemyCurse);
		CombatManager.combinedDeckZone.Add(otherEnemy);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;
		transfer.isFromFriendly = true;
		transfer.curseCardTypeID = CreateScriptableObject<StringSO>();
		transfer.curseCardTypeID.value = "curse_type";

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferAllStatusEffectToHostileCurse();
		EffectChainManager.Me.CloseOpenedChain();

		int friendlyPowerCount = EnumStorage.GetStatusEffectCount(friendly1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		int cursePowerCount = EnumStorage.GetStatusEffectCount(enemyCurse.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, friendlyPowerCount, "Friendly should lose all Power");
		Assert.AreEqual(2, cursePowerCount, "Curse card should gain 2 Power");
	}

	[Test]
	public void TransferAllStatusEffectToHostileCurse_TransfersFromHostileToCurse()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var enemy1 = CreateCard(false, "Enemy1");
		enemy1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemyCurse = CreateCard(false, "EnemyCurse");
		enemyCurse.GetComponent<CardScript>().cardTypeID = "curse_type";

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemyCurse);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;
		transfer.isFromFriendly = false;
		transfer.curseCardTypeID = CreateScriptableObject<StringSO>();
		transfer.curseCardTypeID.value = "curse_type";

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferAllStatusEffectToHostileCurse();
		EffectChainManager.Me.CloseOpenedChain();

		int enemyPowerCount = EnumStorage.GetStatusEffectCount(enemy1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		int cursePowerCount = EnumStorage.GetStatusEffectCount(enemyCurse.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, enemyPowerCount, "Enemy should lose all Power");
		Assert.AreEqual(1, cursePowerCount, "Curse card should gain 1 Power");
	}

	[Test]
	public void TransferAllStatusEffectToHostileCurse_NoCurseCard_DoesNothing()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		CombatManager.combinedDeckZone.Add(friendly1);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;
		transfer.isFromFriendly = true;
		transfer.curseCardTypeID = CreateScriptableObject<StringSO>();
		transfer.curseCardTypeID.value = "curse_type";

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferAllStatusEffectToHostileCurse();
		EffectChainManager.Me.CloseOpenedChain();

		int friendlyPowerCount = EnumStorage.GetStatusEffectCount(friendly1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, friendlyPowerCount, "Should not transfer when no curse card exists");
	}

	[Test]
	public void TransferAllStatusEffectToHostileCurse_CapturesAnimationRequests()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemyCurse = CreateCard(false, "EnemyCurse");
		enemyCurse.GetComponent<CardScript>().cardTypeID = "curse_type";
		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(enemyCurse);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;
		transfer.isFromFriendly = true;
		transfer.curseCardTypeID = CreateScriptableObject<StringSO>();
		transfer.curseCardTypeID.value = "curse_type";

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferAllStatusEffectToHostileCurse();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		bool hasStatusEffectChange = false;
		foreach (var req in recorder.animationRequests)
		{
			if (req.type == AnimationRequestType.StatusEffectChange)
			{
				hasStatusEffectChange = true;
			}
		}
		Assert.IsTrue(hasStatusEffectChange, "Should capture StatusEffectChange animation request");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void TransferOneStatusEffectToSelf_FromFriendly_TransfersToSelf()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var friendly2 = CreateCard(true, "Friendly2");
		friendly2.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(friendly2);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferOneStatusEffectToSelf(true);
		EffectChainManager.Me.CloseOpenedChain();

		int selfPowerCount = EnumStorage.GetStatusEffectCount(sourceCard.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		int friendly1PowerCount = EnumStorage.GetStatusEffectCount(friendly1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		int friendly2PowerCount = EnumStorage.GetStatusEffectCount(friendly2.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(2, selfPowerCount, "Self should gain 2 Power (1 from each friendly)");
		Assert.AreEqual(1, friendly1PowerCount, "Friendly1 should lose 1 Power");
		Assert.AreEqual(0, friendly2PowerCount, "Friendly2 should lose 1 Power");
	}

	[Test]
	public void TransferOneStatusEffectToSelf_FromHostile_TransfersToSelf()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var enemy1 = CreateCard(false, "Enemy1");
		enemy1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemy2 = CreateCard(false, "Enemy2");
		enemy2.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Infected);

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferOneStatusEffectToSelf(false);
		EffectChainManager.Me.CloseOpenedChain();

		int selfPowerCount = EnumStorage.GetStatusEffectCount(sourceCard.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		int enemy1PowerCount = EnumStorage.GetStatusEffectCount(enemy1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, selfPowerCount, "Self should gain 1 Power from enemy");
		Assert.AreEqual(0, enemy1PowerCount, "Enemy1 should lose 1 Power");
	}

	[Test]
	public void TransferOneStatusEffectToSelf_CapturesAnimationRequests()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		CombatManager.combinedDeckZone.Add(friendly1);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferOneStatusEffectToSelf(true);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		// When RecorderAnimationPlayer.me is null, only ApplyStatusEffectCore adds StatusEffectChange requests
		Assert.GreaterOrEqual(recorder.animationRequests.Count, 1, "Should capture at least 1 animation request");
		bool hasStatusEffectChange = false;
		foreach (var req in recorder.animationRequests)
		{
			if (req.type == AnimationRequestType.StatusEffectChange)
			{
				hasStatusEffectChange = true;
			}
		}
		Assert.IsTrue(hasStatusEffectChange, "Should capture StatusEffectChange animation request");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void TransferOneStatusEffectToSelf_NoSourceCards_DoesNothing()
	{
		var sourceCard = CreateCard(true, "Transferer");
		var friendly1 = CreateCard(true, "Friendly1");
		// No Power on friendly
		CombatManager.combinedDeckZone.Add(friendly1);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferOneStatusEffectToSelf(true);
		EffectChainManager.Me.CloseOpenedChain();

		int selfPowerCount = EnumStorage.GetStatusEffectCount(sourceCard.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, selfPowerCount, "Should not gain Power when no source cards have it");
	}

	[Test]
	public void TransferAllStatusEffectToHostileCurse_EnemyCard_TransfersFromEnemyToFriendlyCurse()
	{
		var sourceCard = CreateCard(false, "EnemyTransferer");
		var enemy1 = CreateCard(false, "Enemy1");
		enemy1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var friendlyCurse = CreateCard(true, "FriendlyCurse");
		friendlyCurse.GetComponent<CardScript>().cardTypeID = "curse_type";

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(friendlyCurse);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;
		transfer.isFromFriendly = true;
		transfer.curseCardTypeID = CreateScriptableObject<StringSO>();
		transfer.curseCardTypeID.value = "curse_type";

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferAllStatusEffectToHostileCurse();
		EffectChainManager.Me.CloseOpenedChain();

		int enemyPowerCount = EnumStorage.GetStatusEffectCount(enemy1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		int cursePowerCount = EnumStorage.GetStatusEffectCount(friendlyCurse.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, enemyPowerCount, "Enemy should lose all Power");
		Assert.AreEqual(1, cursePowerCount, "Friendly curse should gain 1 Power");
	}

	[Test]
	public void TransferOneStatusEffectToSelf_EnemyCard_FromHostile_TransfersToEnemySelf()
	{
		var sourceCard = CreateCard(false, "EnemyTransferer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		CombatManager.combinedDeckZone.Add(friendly1);

		var transfer = CreateEffect<TransferStatusEffectEffect>(sourceCard);
		transfer.statusEffectToTransfer = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, transfer.gameObject);
		transfer.TransferOneStatusEffectToSelf(false);
		EffectChainManager.Me.CloseOpenedChain();

		int selfPowerCount = EnumStorage.GetStatusEffectCount(sourceCard.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, selfPowerCount, "Enemy self should gain 1 Power from friendly");
	}
}
