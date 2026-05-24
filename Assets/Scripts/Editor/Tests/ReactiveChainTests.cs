using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

public class ReactiveChainTests : HeadlessCombatTestFixture
{
	[Test]
	public void BuryTriggersStage_ReactiveEffectCreatesChildRecorder()
	{
		var burier = CreateCard(true, "Burier");
		var target = CreateCard(false, "TargetToBury");
		var bottomCard = CreateCard(true, "BottomCard");
		CombatManager.combinedDeckZone.Add(bottomCard);
		CombatManager.combinedDeckZone.Add(target);

		var buryEffect = CreateEffect<BuryEffect>(burier);
		var stageEffect = CreateEffect<StageEffect>(target);

		// Listener on target card reacts to onMeBuried by staging self
		var listenerObj = CreateGameObject("BuryListener");
		listenerObj.transform.SetParent(target.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onMeBuried;
		listener.response.AddListener(() => {
			EffectChainManager.MakeANewEffectRecorder(target, stageEffect.gameObject);
			stageEffect.StageSelf();
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = OwnerStatus;
		ls.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onMeBuried.RegisterListener(listener);

		EffectChainManager.MakeANewEffectRecorder(burier, buryEffect.gameObject);
		buryEffect.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		// Verify recorder tree: root (bury) -> child (stage)
		Assert.AreEqual(2, EffectChainManager.Me.closedEffectRecorders.Count, "Should have 2 recorders (root + child)");
		var rootGo = EffectChainManager.Me.closedEffectRecorders[0];
		Assert.AreEqual(1, rootGo.transform.childCount, "Root should have 1 child recorder");

		var root = rootGo.GetComponent<EffectRecorder>();
		var child = rootGo.transform.GetChild(0).GetComponent<EffectRecorder>();
		Assert.AreEqual(AnimationRequestType.PopUpBatch, root.animationRequests[0].type, "Root should have PopUpBatch request");
		Assert.AreEqual(AnimationRequestType.MoveToBottomBatch, root.animationRequests[1].type, "Root should have MoveToBottomBatch request");
		Assert.AreEqual(AnimationRequestType.MoveToTopPopUpBatch, child.animationRequests[0].type, "Child should have MoveToTopPopUpBatch request");

		// Verify final deck: target was buried then staged back to top
		Assert.AreEqual(target, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1], "Target should end up at top after reactive stage");
	}

	[Test]
	public void NestedReactiveEffects_ThreeLevelRecorderTree()
	{
		// Card1 (Curse) gives Power to Card2
		// Card2 listens to onMeGotPower -> attacks enemy
		// Card3 listens to onTheirPlayerTookDmg -> attacks enemy again
		var card1 = CreateCard(false, "Curser");
		var card2 = CreateCard(true, "PowerTarget");
		var card3 = CreateCard(true, "Reactor");

		CombatManager.combinedDeckZone.Add(card2);
		CombatManager.combinedDeckZone.Add(card3);

		// Card1: CurseEffect gives Power to Card2
		var curse = CreateEffect<CurseEffect>(card1);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "power_target_type";
		card2.GetComponent<CardScript>().cardTypeID = "power_target_type";

		// Card2: HPAlterEffect triggered by onMeGotPower
		var hpa2 = CreateEffect<HPAlterEffect>(card2);
		hpa2.baseDmg = CreateScriptableObject<IntSO>();
		hpa2.baseDmg.value = 3;

		var listener2Obj = CreateGameObject("PowerListener");
		listener2Obj.transform.SetParent(card2.transform);
		var listener2 = listener2Obj.AddComponent<GameEventListener>();
		listener2.@event = GameEventStorage.onMeGotPower;
		listener2.response.AddListener(() => {
			EffectChainManager.MakeANewEffectRecorder(card2, hpa2.gameObject);
			if (EffectChainManager.Me.EffectCanBeInvoked("hpa2_reactive"))
			{
				hpa2.DecreaseTheirHp();
			}
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls2 = listener2Obj.AddComponent<CardScript>();
		ls2.myStatusRef = OwnerStatus;
		ls2.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls2.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onMeGotPower.RegisterListener(listener2);

		// Card3: HPAlterEffect triggered by onTheirPlayerTookDmg (from Card2)
		var hpa3 = CreateEffect<HPAlterEffect>(card3);
		hpa3.baseDmg = CreateScriptableObject<IntSO>();
		hpa3.baseDmg.value = 4;

		var listener3Obj = CreateGameObject("DmgListener");
		listener3Obj.transform.SetParent(card3.transform);
		var listener3 = listener3Obj.AddComponent<GameEventListener>();
		listener3.@event = GameEventStorage.onTheirPlayerTookDmg;
		listener3.response.AddListener(() => {
			EffectChainManager.MakeANewEffectRecorder(card3, hpa3.gameObject);
			if (EffectChainManager.Me.EffectCanBeInvoked("hpa3_reactive"))
			{
				hpa3.DecreaseTheirHp();
			}
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls3 = listener3Obj.AddComponent<CardScript>();
		ls3.myStatusRef = OwnerStatus;
		ls3.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls3.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onTheirPlayerTookDmg.RegisterListener(listener3);

		// Execute: Card1 gives Power to Card2
		EffectChainManager.MakeANewEffectRecorder(card1, curse.gameObject);
		curse.EnhanceCurse(1);
		EffectChainManager.Me.CloseOpenedChain();

		// Verify damage: 3 (hpa2) + 4 (hpa3) = 7
		Assert.AreEqual(93, EnemyStatus.hp, "Total damage should be 3+4=7");

		// Verify recorder tree depth: root -> child -> grandchild
		// Note: 4 total recorders because onTheirPlayerTookDmg triggers listener3 twice (hpa2 + hpa3),
		// but the second attempt is blocked by loop guard, creating an extra empty recorder.
		Assert.AreEqual(4, EffectChainManager.Me.closedEffectRecorders.Count, "Should have 4 recorders total");
		var root = EffectChainManager.Me.closedEffectRecorders[0];
		Assert.AreEqual(1, root.transform.childCount, "Root should have 1 child");
		var child = root.transform.GetChild(0);
		Assert.AreEqual(1, child.childCount, "Child should have 1 grandchild");
	}

	[Test]
	public void SameCardSameEffect_LoopGuardBlocksSecondInvoke()
	{
		var card = CreateCard(true, "Looper");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		int listenerCallCount = 0;
		var listenerObj = CreateGameObject("LoopListener");
		listenerObj.transform.SetParent(card.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onTheirPlayerTookDmg;
		listener.response.AddListener(() => {
			listenerCallCount++;
			// Attempt to invoke same card+effect again (should be blocked)
			EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
			bool canInvoke = EffectChainManager.Me.EffectCanBeInvoked("same_hp_effect");
			if (canInvoke)
			{
				hpa.DecreaseTheirHp(); // Should NOT execute
			}
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = OwnerStatus;
		ls.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onTheirPlayerTookDmg.RegisterListener(listener);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		if (EffectChainManager.Me.EffectCanBeInvoked("hp_effect"))
		{
			hpa.DecreaseTheirHp();
		}
		EffectChainManager.Me.PopCurrentRecorder();
		EffectChainManager.Me.CloseOpenedChain();

		// Enemy should only take 5 damage once, not twice
		Assert.AreEqual(95, EnemyStatus.hp, "Enemy should only take damage once (loop guard blocks second invoke)");
		Assert.AreEqual(1, listenerCallCount, "Listener should be called once");
	}

	[Test]
	public void DamageEventTriggersReactiveHeal()
	{
		var attacker = CreateCard(true, "Attacker");
		var healer = CreateCard(true, "Healer");

		var hpa = CreateEffect<HPAlterEffect>(attacker);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		var hpaHeal = CreateEffect<HPAlterEffect>(healer);

		// Healer reacts to enemy taking damage by healing owner
		var listenerObj = CreateGameObject("HealListener");
		listenerObj.transform.SetParent(healer.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onTheirPlayerTookDmg;
		listener.response.AddListener(() => {
			EffectChainManager.MakeANewEffectRecorder(healer, hpaHeal.gameObject);
			if (EffectChainManager.Me.EffectCanBeInvoked("heal_reactive"))
			{
				hpaHeal.IncreaseMyHp(10);
			}
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = OwnerStatus;
		ls.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onTheirPlayerTookDmg.RegisterListener(listener);

		OwnerStatus.hp = 50;
		EffectChainManager.MakeANewEffectRecorder(attacker, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, EnemyStatus.hp, "Enemy should take 5 damage");
		Assert.AreEqual(60, OwnerStatus.hp, "Owner should be healed 10 HP");
	}

	[Test]
	public void StageSelfOnMeStaged_NoInfiniteLoop()
	{
		var card = CreateCard(true, "Stager");
		var topCard = CreateCard(true, "TopCard");
		CombatManager.combinedDeckZone.Add(card);
		CombatManager.combinedDeckZone.Add(topCard);

		var stageEffect = CreateEffect<StageEffect>(card);

		// Listener on card reacts to onMeStaged by calling StageSelf again
		var listenerObj = CreateGameObject("StageListener");
		listenerObj.transform.SetParent(card.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onMeStaged;
		listener.response.AddListener(() => {
			EffectChainManager.MakeANewEffectRecorder(card, stageEffect.gameObject);
			stageEffect.StageSelf(); // Should be no-op because card is already at top
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = OwnerStatus;
		ls.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onMeStaged.RegisterListener(listener);

		EffectChainManager.MakeANewEffectRecorder(card, stageEffect.gameObject);
		if (EffectChainManager.Me.EffectCanBeInvoked("stage_effect"))
		{
			stageEffect.StageSelf();
		}
		EffectChainManager.Me.PopCurrentRecorder();
		EffectChainManager.Me.CloseOpenedChain();

		// Root + child recorder (child is created by reactive listener, but loop guard blocks StageSelf)
		Assert.AreEqual(2, EffectChainManager.Me.closedEffectRecorders.Count);
		var root = EffectChainManager.Me.closedEffectRecorders[0];
		Assert.AreEqual(1, root.transform.childCount, "Should have 1 child recorder for reactive attempt");
		// Child recorder should have 0 animation requests because loop guard blocked StageSelf
		var child = root.transform.GetChild(0).GetComponent<EffectRecorder>();
		Assert.AreEqual(0, child.animationRequests.Count, "Reactive StageSelf should produce no requests because loop guard blocks second invoke");
	}

	[Test]
	public void CursePowerTriggersDamageChain()
	{
		var curser = CreateCard(true, "Curser");
		var curseTarget = CreateCard(false, "CursedEnemy");
		var reactor = CreateCard(true, "Reactor");

		CombatManager.combinedDeckZone.Add(curseTarget);
		CombatManager.combinedDeckZone.Add(reactor);

		// Curser enhances curse target
		var curse = CreateEffect<CurseEffect>(curser);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_type";
		curseTarget.GetComponent<CardScript>().cardTypeID = "curse_type";

		// Reactor attacks when any card gets Power
		var hpa = CreateEffect<HPAlterEffect>(reactor);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 7;

		var listenerObj = CreateGameObject("PowerDmgListener");
		listenerObj.transform.SetParent(reactor.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onAnyCardGotPower;
		listener.response.AddListener(() => {
			EffectChainManager.MakeANewEffectRecorder(reactor, hpa.gameObject);
			if (EffectChainManager.Me.EffectCanBeInvoked("power_dmg"))
			{
				hpa.DecreaseTheirHp();
			}
			EffectChainManager.Me.PopCurrentRecorder();
		});
		var ls = listenerObj.AddComponent<CardScript>();
		ls.myStatusRef = OwnerStatus;
		ls.myStatusEffects = new List<EnumStorage.StatusEffect>();
		ls.myTags = new List<EnumStorage.Tag>();
		GameEventStorage.onAnyCardGotPower.RegisterListener(listener);

		EffectChainManager.MakeANewEffectRecorder(curser, curse.gameObject);
		curse.EnhanceCurse(2);
		EffectChainManager.Me.CloseOpenedChain();

		// Curse target gets 2 Power, then reactor deals 7 damage
		Assert.AreEqual(93, EnemyStatus.hp, "Enemy should take 7 reactive damage");
		int powerCount = 0;
		foreach (var effect in curseTarget.GetComponent<CardScript>().myStatusEffects)
		{
			if (effect == EnumStorage.StatusEffect.Power) powerCount++;
		}
		Assert.AreEqual(2, powerCount, "Curse target should have 2 Power stacks");
	}
}
