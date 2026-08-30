using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EditMode tests for the 4.0 passive-card engine (plans/plan-4.0-passive-cards-2026-08-29.md).
/// Passive invariant: after every shuffle the passives sit below the Start Card
/// (index &lt; startCardIndex, grave side), are never revealed, and are skipped by
/// every movement-effect selection pool.
/// </summary>
public class PassiveCardTests : HeadlessCombatTestFixture
{
	private GameObject _start;
	private StartCardShuffleEffect _shuffle;

	public override void SetUp()
	{
		base.SetUp();
		// Keep fatigue out of shuffle tests (default threshold 0 would trigger at round 1)
		CombatManager.overtimeRoundThreshold = 999;
		_start = CreateStartCard();
		var shuffleObj = CreateGameObject("StartCardShuffleEffect");
		_shuffle = shuffleObj.AddComponent<StartCardShuffleEffect>();
		// The fresh component defaults to Gaussian placement; the real Start Card uses
		// AlwaysBottom. Force it so the pinned layout assertions are deterministic.
		var shuffleSo = new UnityEditor.SerializedObject(_shuffle);
		shuffleSo.FindProperty("startCardPlacement").enumValueIndex = (int)StartCardShuffleEffect.StartCardPlacement.AlwaysBottom;
		shuffleSo.ApplyModifiedPropertiesWithoutUndo();
	}

	private GameObject CreatePassive(string name)
	{
		var card = CreateCard(true, name);
		card.GetComponent<CardScript>().isPassive = true;
		return card;
	}

	/// <summary>
	/// Simulate one shuffle: deck WITHOUT the Start Card + Start Card in the reveal zone.
	/// Returns the live combinedDeckZone reference.
	/// </summary>
	private List<GameObject> RunShuffle(List<GameObject> deckWithoutStart, bool customOrder = false, List<GameObject> orderPrefabs = null)
	{
		CombatManager.combinedDeckZone.Clear();
		CombatManager.combinedDeckZone.AddRange(deckWithoutStart);
		CombatManager.revealZone = _start;
		if (customOrder)
		{
			var overrideComponent = CombatManager.gameObject.AddComponent<ShuffleOrderOverride>();
			overrideComponent.useCustomOrder = true;
			overrideComponent.customOrderPrefabs = orderPrefabs;
		}
		EffectChainManager.MakeANewEffectRecorder(_start, _shuffle.gameObject);
		_shuffle.ExecuteShuffleEffect();
		EffectChainManager.Me.CloseOpenedChain();
		return CombatManager.combinedDeckZone;
	}

	[Test]
	public void Shuffle_PlacesPassivesBelowStartCard()
	{
		var passive = CreatePassive("Passive");
		var live1 = CreateCard(true, "Live1");
		var live2 = CreateCard(true, "Live2");

		var deck = RunShuffle(new List<GameObject> { live1, passive, live2 });

		Assert.AreEqual(4, deck.Count);
		Assert.AreSame(passive, deck[0], "Passive must be pinned below the Start Card");
		Assert.AreSame(_start, deck[1], "Start Card sits directly above the passives");
		CollectionAssert.AreEquivalent(new[] { live1, live2 }, new[] { deck[2], deck[3] }, "Live cards fill the live zone");
	}

	[Test]
	public void Shuffle_KeepsPassivesBelowStartCard_AcrossRounds()
	{
		var passive = CreatePassive("Passive");
		var live = CreateCard(true, "Live");

		var deck = RunShuffle(new List<GameObject> { passive, live });
		Assert.AreSame(passive, deck[0]);

		// Round 2: the Start Card gets revealed again (move it back to the reveal zone)
		deck.Remove(_start);
		CombatManager.revealZone = _start;
		EffectChainManager.MakeANewEffectRecorder(_start, _shuffle.gameObject);
		_shuffle.ExecuteShuffleEffect();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.Less(deck.IndexOf(passive), deck.IndexOf(_start), "Passive stays on the grave side after the second shuffle");
	}

	[Test]
	public void Passive_NeverEntersLiveZoneThroughRevealCycles()
	{
		var passive = CreatePassive("Passive");
		var live1 = CreateCard(true, "Live1");
		var live2 = CreateCard(true, "Live2");

		var deck = RunShuffle(new List<GameObject> { live1, passive, live2 });

		// Simulate reveal cycles: pop the top card, put it back at index 0 (the reveal flow)
		for (int i = 0; i < 8; i++)
		{
			var top = deck[deck.Count - 1];
			deck.RemoveAt(deck.Count - 1);
			deck.Insert(0, top);
		}

		Assert.Less(deck.IndexOf(passive), deck.IndexOf(_start), "Passive never crosses above the Start Card");
	}

	[Test]
	public void CustomShuffleOrder_StillPinsPassivesBelowStartCard()
	{
		var passive = CreatePassive("Passive");
		var live1 = CreateCard(true, "Live1", "LIVE_TYPE");
		var live2 = CreateCard(true, "Live2", "LIVE_TYPE_2");
		var prefabLive1 = CreateCard(true, "PrefabLive1", "LIVE_TYPE"); // prefab stand-in, not in the deck

		var deck = RunShuffle(
			new List<GameObject> { live1, passive, live2 },
			customOrder: true,
			orderPrefabs: new List<GameObject> { prefabLive1 });

		Assert.AreSame(passive, deck[0], "Passives do not participate in custom orders and stay pinned");
		Assert.Less(deck.IndexOf(_start), deck.IndexOf(live1), "Custom order still applies to the live zone");
	}

	[Test]
	public void StageMyCards_SkipsPassives()
	{
		var passive = CreatePassive("Passive");
		var normal = CreateCard(true, "Normal");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { passive, normal, _start });
		var source = CreateCard(true, "Stager");
		var stage = CreateEffect<StageEffect>(source);

		EffectChainManager.MakeANewEffectRecorder(source, stage.gameObject);
		stage.StageMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(normal, deck[deck.Count - 1], "The only stageable card is the non-passive one");
		Assert.Contains(passive, deck, "Passive stays in the deck, unstaged");
		Assert.AreEqual(0, deck.IndexOf(passive), "Passive position is untouched by staging");
	}

	[Test]
	public void DelayMyCards_SkipsPassives()
	{
		var normal = CreateCard(true, "Normal");
		var passive = CreatePassive("Passive");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { normal, passive, _start });
		var source = CreateCard(true, "Delayer");
		var delay = CreateEffect<CardManipulationEffect>(source);

		// Pool candidates with i > 0: the passive (index 1) is the only non-neutral candidate
		// besides itself — without the isPassive check it would be delayed. With the check the
		// delay fizzles and the deck is untouched.
		EffectChainManager.MakeANewEffectRecorder(source, delay.gameObject);
		delay.DelayMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(normal, deck[0], "Deck must be untouched by the fizzled delay");
		Assert.AreSame(passive, deck[1], "Passive was never delayed");
	}

	[Test]
	public void ExileMyCards_SkipsPassives()
	{
		var passive = CreatePassive("Passive");
		var normal = CreateCard(true, "Normal");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { passive, normal, _start });
		var source = CreateCard(true, "Exiler");
		var exile = CreateEffect<ExileEffect>(source);

		EffectChainManager.MakeANewEffectRecorder(source, exile.gameObject);
		exile.ExileMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		var deck = CombatManager.combinedDeckZone;
		CollectionAssert.DoesNotContain(deck, normal, "The non-passive card is exiled");
		CollectionAssert.Contains(deck, passive, "Passive can never be exiled by a selection pool");
	}

	[Test]
	public void Passive_CountsTowardGraveCount()
	{
		ValueTrackerManager.ownerInGraveAmountRef = CreateScriptableObject<IntSO>();
		var passive = CreatePassive("Passive");
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { passive, _start, live });

		ValueTrackerManager.UpdateAllTrackers();

		Assert.AreEqual(1, ValueTrackerManager.ownerInGraveAmountRef.value, "The passive counts as a grave-side card");
	}

	[Test]
	public void Passive_ListenerFiresOnEveryMatchingEvent()
	{
		var passive = CreatePassive("Passive");
		int fired = 0;
		var listener = passive.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onAnyCardBuried;
		listener.response = new UnityEvent();
		listener.response.AddListener(() => fired++);
		GameEventStorage.onAnyCardBuried.RegisterListener(listener); // Edit Mode skips OnEnable

		GameEventStorage.onAnyCardBuried.Raise();
		GameEventStorage.onAnyCardBuried.Raise();

		Assert.AreEqual(2, fired, "Per-event firing: a passive reacts to every matching event, no per-round cap");
	}

	[Test]
	public void Passive_ChainBurial_BuriesLiveZoneTopOnFriendlyBuried()
	{
		// RELIC_CHAIN_BURIAL regression: 被动：友方被埋葬时，埋葬1卡组顶卡.
		// The passive lives below the Start Card permanently, so BuryNextXCards must
		// not bail on the below-Start-Card source guard and must target the live-zone top.
		var passive = CreatePassive("ChainBurial");
		var liveA = CreateCard(true, "LiveA");
		var liveB = CreateCard(true, "LiveB");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { passive, _start, liveA, liveB });

		// mirror the prefab wiring: listener(onFriendlyCardBuried) -> container -> BuryNextXCards(1)
		var bury = CreateEffect<BuryEffect>(passive);
		var container = CreateCostContainer(passive);
		UnityEditor.Events.UnityEventTools.AddIntPersistentListener(container.effectEvent, bury.BuryNextXCards, 1);
		var listener = passive.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onFriendlyCardBuried;
		listener.response = new UnityEvent();
		listener.response.AddListener(container.InvokeEffectEventVoid);
		GameEventStorage.onFriendlyCardBuried.RegisterListener(listener); // Edit Mode skips OnEnable

		// UnityEventTools defaults to RuntimeOnly, which UnityEvent.Invoke skips in Edit Mode
		// tests. EditorAndRuntime exercises the same path here and still fires in play mode.
		container.effectEvent.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);

		// No pre-opened recorder here: pre-opening one with the container GO would trip the
		// chain loop guard (same card + same effect object invoked twice in one chain) and
		// block the invocation. The container opens and closes its own chain, like in-game.
		int probeFired = 0;
		var probeListener = passive.AddComponent<GameEventListener>();
		probeListener.@event = GameEventStorage.onFriendlyCardBuried;
		probeListener.response = new UnityEvent();
		probeListener.response.AddListener(() => probeFired++);
		GameEventStorage.onFriendlyCardBuried.RegisterListener(probeListener);

		GameEventStorage.onFriendlyCardBuried.RaiseOwner();
		EffectChainManager.Me.CloseOpenedChain();

		var diag = "probeFired=" + probeFired
			+ " openedRecorders=" + EffectChainManager.Me.openedEffectRecorders.Count
			+ " lastEffectObject=" + (EffectChainManager.Me.lastEffectObject != null ? EffectChainManager.Me.lastEffectObject.name : "null")
			+ " deck=" + string.Join(",", CombatManager.combinedDeckZone.ConvertAll(c => c.name));
		var deckNow = CombatManager.combinedDeckZone;
		bool buriedTop = deckNow.Count > 0 && deckNow[0] == liveB;
		Assert.IsTrue(buriedTop, "passive did not bury the live-zone top | " + diag);

		var deck = CombatManager.combinedDeckZone;
		Assert.AreEqual(4, deck.Count, "Bury moves cards within the deck, size unchanged");
		Assert.AreSame(liveB, deck[0], "The live-zone top card is buried to the bottom by the passive");
		Assert.AreSame(liveA, deck[3], "The card under the top stays in the live zone");
	}
}
