using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

public class PowerReactionEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void GivePowerToCardThatGotPower_AddsPowerToTarget()
	{
		var reactor = CreateCard(true, "Reactor");
		var target = CreateCard(false, "Target");

		var powerReaction = CreateEffect<PowerReactionEffect>(reactor);
		powerReaction.powerAmount = 2;
		powerReaction.excludeSelf = true;

		CombatManager.lastCardGotPower = target.GetComponent<CardScript>();

		EffectChainManager.MakeANewEffectRecorder(reactor, powerReaction.gameObject);
		powerReaction.GivePowerToCardThatGotPower();
		EffectChainManager.Me.CloseOpenedChain();

		int targetPowerCount = EnumStorage.GetStatusEffectCount(target.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(2, targetPowerCount, "Target should gain 2 Power");
	}

	[Test]
	public void GivePowerToCardThatGotPower_ExcludeSelf_SkipsSelf()
	{
		var reactor = CreateCard(true, "Reactor");
		var reactorScript = reactor.GetComponent<CardScript>();

		var powerReaction = CreateEffect<PowerReactionEffect>(reactor);
		powerReaction.powerAmount = 1;
		powerReaction.excludeSelf = true;

		CombatManager.lastCardGotPower = reactorScript;

		EffectChainManager.MakeANewEffectRecorder(reactor, powerReaction.gameObject);
		powerReaction.GivePowerToCardThatGotPower();
		EffectChainManager.Me.CloseOpenedChain();

		int selfPowerCount = EnumStorage.GetStatusEffectCount(reactorScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, selfPowerCount, "Should skip self when excludeSelf is true");
	}

	[Test]
	public void GivePowerToCardThatGotPower_IncludeSelf_AppliesToSelf()
	{
		var reactor = CreateCard(true, "Reactor");
		var reactorScript = reactor.GetComponent<CardScript>();

		var powerReaction = CreateEffect<PowerReactionEffect>(reactor);
		powerReaction.powerAmount = 1;
		powerReaction.excludeSelf = false;

		CombatManager.lastCardGotPower = reactorScript;

		EffectChainManager.MakeANewEffectRecorder(reactor, powerReaction.gameObject);
		powerReaction.GivePowerToCardThatGotPower();
		EffectChainManager.Me.CloseOpenedChain();

		int selfPowerCount = EnumStorage.GetStatusEffectCount(reactorScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, selfPowerCount, "Should apply to self when excludeSelf is false");
	}

	[Test]
	public void GivePowerToCardThatGotPower_NoTarget_DoesNothing()
	{
		var reactor = CreateCard(true, "Reactor");

		var powerReaction = CreateEffect<PowerReactionEffect>(reactor);
		powerReaction.powerAmount = 1;

		CombatManager.lastCardGotPower = null;

		EffectChainManager.MakeANewEffectRecorder(reactor, powerReaction.gameObject);
		powerReaction.GivePowerToCardThatGotPower();
		EffectChainManager.Me.CloseOpenedChain();

		// Should not throw and should do nothing
		Assert.Pass();
	}

	[Test]
	public void GivePowerToCardThatGotPower_TriggeredByEvent_AppliesOnce()
	{
		var reactor = CreateCard(true, "Reactor");
		var target = CreateCard(false, "Target");
		CombatManager.lastCardGotPower = target.GetComponent<CardScript>();

		var powerReaction = CreateEffect<PowerReactionEffect>(reactor);
		powerReaction.powerAmount = 1;
		powerReaction.excludeSelf = true;

		int callCount = 0;
		var listenerObj = CreateGameObject("Listener");
		listenerObj.transform.SetParent(reactor.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onAnyCardGotPower;
		listener.response.AddListener(() => {
			callCount++;
			if (callCount > 1) return; // manual guard to prevent recursion in test
			EffectChainManager.MakeANewEffectRecorder(reactor, powerReaction.gameObject);
			powerReaction.GivePowerToCardThatGotPower();
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = OwnerStatus;
		ls.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
		ls.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();
		GameEventStorage.onAnyCardGotPower.RegisterListener(listener);

		// Trigger the event manually
		GameEventStorage.onAnyCardGotPower.Raise();
		EffectChainManager.Me.CloseOpenedChain();

		int targetPowerCount = EnumStorage.GetStatusEffectCount(target.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, targetPowerCount, "Target should gain 1 Power from event-triggered reaction");
		Assert.AreEqual(2, callCount, "Listener should be called twice (once original + once reactive), manual guard blocks further recursion");
	}

	[Test]
	public void GivePowerToCardThatGotPower_EnemyCard_TriggeredByEvent_AppliesOnce()
	{
		var enemyReactor = CreateCard(false, "EnemyReactor");
		var friendlyTarget = CreateCard(true, "FriendlyTarget");
		CombatManager.lastCardGotPower = friendlyTarget.GetComponent<CardScript>();

		var powerReaction = CreateEffect<PowerReactionEffect>(enemyReactor);
		powerReaction.powerAmount = 1;
		powerReaction.excludeSelf = true;

		int callCount = 0;
		var listenerObj = CreateGameObject("Listener");
		listenerObj.transform.SetParent(enemyReactor.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onAnyCardGotPower;
		listener.response.AddListener(() => {
			callCount++;
			if (callCount > 1) return; // manual guard to prevent recursion in test
			EffectChainManager.MakeANewEffectRecorder(enemyReactor, powerReaction.gameObject);
			powerReaction.GivePowerToCardThatGotPower();
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = EnemyStatus;
		ls.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
		ls.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();
		GameEventStorage.onAnyCardGotPower.RegisterListener(listener);

		// Trigger the event manually
		GameEventStorage.onAnyCardGotPower.Raise();
		EffectChainManager.Me.CloseOpenedChain();

		int targetPowerCount = EnumStorage.GetStatusEffectCount(friendlyTarget.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, targetPowerCount, "Friendly target should gain 1 Power from enemy reactor");
		Assert.AreEqual(2, callCount, "Listener should be called twice, manual guard blocks further recursion");
	}
}
