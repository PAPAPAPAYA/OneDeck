using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EditMode tests for the once-per-round revive gate
/// (plans/plan-revive-loop-mitigation-2026-09-19.md §8.2).
/// Gate semantics: per card instance x ReviveEffect component, at most one successful
/// revive per round; empty-grave fizzles do not consume the charge; the charge reopens
/// at every round start (lazy stamp off CombatManager.roundNumRef).
/// </summary>
public class ReviveOncePerRoundGateTests : HeadlessCombatTestFixture
{
	private ReviveEffect CreateGatedReviver(GameObject sourceCard)
	{
		var effect = CreateEffect<ReviveEffect>(sourceCard);
		effect.oncePerRound = true;
		return effect;
	}

	/// <summary>
	/// Wire a hub card the way the prefabs do: onMeRevealed -> ReviveMyCards(1).
	/// </summary>
	private void WireHub(GameObject hub, ReviveEffect effect)
	{
		var listener = hub.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onMeRevealed;
		listener.response = new UnityEvent();
		listener.response.AddListener(() => effect.ReviveMyCards(1));
		GameEventStorage.onMeRevealed.RegisterListener(listener);
	}

	private void InvokeHandleNewRoundStart()
	{
		var handleNewRoundStart = typeof(CombatManager).GetMethod("HandleNewRoundStart", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.IsNotNull(handleNewRoundStart, "HandleNewRoundStart should exist");
		handleNewRoundStart.Invoke(CombatManager, null);
	}

	[Test]
	public void OncePerRoundGate_HubLoop_ReachesStartCard()
	{
		// Minimal loop deck (plan §8.1.1): two gated hubs above the Start Card.
		var start = CreateStartCard();
		var hubA = CreateCard(true, "HubA");
		var hubB = CreateCard(true, "HubB");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, hubA, hubB });
		WireHub(hubA, CreateGatedReviver(hubA));
		WireHub(hubB, CreateGatedReviver(hubB));

		CardScript revealed = null;
		int reveals = 0;
		const int safety = 20;
		while (reveals < safety)
		{
			revealed = RevealTopCard();
			reveals++;
			if (revealed == null || revealed.isStartCard) break;
			TriggerRevealedCard();
			PutRevealedCardToBottom();
		}

		Assert.IsNotNull(revealed, "Loop must reveal something");
		Assert.IsTrue(revealed.isStartCard,
			"Two gated hubs must let the Start Card surface; still cycling after " + safety + " reveals");
		Assert.LessOrEqual(reveals, 6,
			"Start Card should surface within a few reveals once both per-round charges are spent (was " + reveals + ")");
	}

	[Test]
	public void HubLoop_WithoutGate_NeverReachesStartCard()
	{
		// Control: without the gate the same deck loops forever (plan §1).
		var start = CreateStartCard();
		var hubA = CreateCard(true, "HubA");
		var hubB = CreateCard(true, "HubB");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, hubA, hubB });
		WireHub(hubA, CreateEffect<ReviveEffect>(hubA));
		WireHub(hubB, CreateEffect<ReviveEffect>(hubB));

		bool startCardRevealed = false;
		const int iterations = 20;
		for (int i = 0; i < iterations; i++)
		{
			var revealed = RevealTopCard();
			if (revealed == null) break;
			if (revealed.isStartCard) { startCardRevealed = true; break; }
			TriggerRevealedCard();
			PutRevealedCardToBottom();
		}

		Assert.IsFalse(startCardRevealed,
			"Ungated hubs must keep pulling each other over the Start Card (the loop this gate exists to break)");
	}

	[Test]
	public void OncePerRoundGate_SecondSuccessSameRound_Blocked()
	{
		var start = CreateStartCard();
		var graveA = CreateCard(true, "GraveA");
		var liveB = CreateCard(true, "LiveB");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveA, start, liveB });
		var source = CreateCard(true, "Hub");
		var effect = CreateGatedReviver(source);

		effect.ReviveMyCards(1);
		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountRef.value, "First revive succeeds");

		// A fresh valid target in the grave must not matter once the charge is spent.
		var graveC = CreateCard(true, "GraveC");
		CombatManager.combinedDeckZone.Insert(0, graveC);
		effect.ReviveMyCards(1);

		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountRef.value, "Second revive in the same round is gated");
		Assert.AreSame(graveC, CombatManager.combinedDeckZone[0], "Gated revive must not move any card");
	}

	[Test]
	public void OncePerRoundGate_EmptyGrave_DoesNotConsumeCharge()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });
		var source = CreateCard(true, "Hub");
		var effect = CreateGatedReviver(source);

		effect.ReviveMyCards(1); // fizzle: empty grave

		var grave = CreateCard(true, "Grave");
		CombatManager.combinedDeckZone.Insert(0, grave);
		effect.ReviveMyCards(1); // must succeed: the fizzle did not consume the charge

		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountRef.value,
			"Empty-grave fizzle must not consume the per-round charge");
		Assert.AreSame(grave, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"Charge preserved -> the later non-empty revive succeeds");
	}

	[Test]
	public void OncePerRoundGate_TwoInstances_CountSeparately()
	{
		// Two copies of the same card each own one charge per round (plan §8.2 granularity).
		var start = CreateStartCard();
		var graveA = CreateCard(true, "GraveA");
		var graveB = CreateCard(true, "GraveB");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveA, graveB, start });
		var hubA = CreateCard(true, "Hub");
		var hubB = CreateCard(true, "Hub");
		var effectA = CreateGatedReviver(hubA);
		var effectB = CreateGatedReviver(hubB);

		effectA.ReviveMyCards(1);
		effectB.ReviveMyCards(1);
		Assert.AreEqual(2, ValueTrackerManager.ownerRevivedCountRef.value,
			"Each card instance owns its own per-round charge");

		var graveC = CreateCard(true, "GraveC");
		CombatManager.combinedDeckZone.Insert(0, graveC);
		effectA.ReviveMyCards(1);
		Assert.AreEqual(2, ValueTrackerManager.ownerRevivedCountRef.value,
			"Instance A is gated after its own success even though instance B also fired");
	}

	[Test]
	public void OncePerRoundGate_RoundStart_Reopens()
	{
		var start = CreateStartCard();
		var graveA = CreateCard(true, "GraveA");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveA, start });
		var source = CreateCard(true, "Hub");
		var effect = CreateGatedReviver(source);

		effect.ReviveMyCards(1);
		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountRef.value, "First revive succeeds");

		// Production increments roundNumRef inside StartCardShuffleEffect before HandleNewRoundStart.
		CombatManager.roundNumRef.value++;
		InvokeHandleNewRoundStart();

		var graveB = CreateCard(true, "GraveB");
		CombatManager.combinedDeckZone.Insert(0, graveB);
		effect.ReviveMyCards(1);
		Assert.AreEqual(2, ValueTrackerManager.ownerRevivedCountRef.value, "The gate reopens at the new round");
	}

	[Test]
	public void OncePerRoundGate_DefaultOff_Unlimited()
	{
		var start = CreateStartCard();
		var graveA = CreateCard(true, "GraveA");
		var graveB = CreateCard(true, "GraveB");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveA, graveB, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateEffect<ReviveEffect>(source); // oncePerRound defaults to false

		effect.ReviveMyCards(1);
		effect.ReviveMyCards(1);
		Assert.AreEqual(2, ValueTrackerManager.ownerRevivedCountRef.value,
			"Default (flag off) keeps the classic unlimited behavior");
	}
}
