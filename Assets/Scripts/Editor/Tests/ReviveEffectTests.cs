using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EditMode tests for the 4.0 revive/awaken engine (plans/plan-4.0-revive-awaken-2026-08-29.md).
/// Deck layout convention: indices 0..startCardIndex-1 = grave side, then the Start Card, then the live zone.
/// </summary>
public class ReviveEffectTests : HeadlessCombatTestFixture
{
	private ReviveSpy _spy;

	private class ReviveSpy : MonoBehaviour
	{
		public int meRevivedCount;
		public int anyRevivedCount;
		public int friendlyRevivedCount;

		public void OnMeRevived() { meRevivedCount++; }
		public void OnAnyRevived() { anyRevivedCount++; }
		public void OnFriendlyRevived() { friendlyRevivedCount++; }
	}

	public override void SetUp()
	{
		base.SetUp();
		var spyObj = CreateGameObject("ReviveSpy");
		_spy = spyObj.AddComponent<ReviveSpy>();
	}

	/// <summary>
	/// Register a spy listener directly on a card. Must live on the card GO (not a child):
	/// GameEvent.ExecuteRaiseOwner dereferences GetComponent CardScript on each listener.
	/// Edit Mode does not run OnEnable for plain MonoBehaviours, so registration is manual.
	/// </summary>
	private void AttachSpyListener(GameObject card, GameEvent evt, System.Action spyMethod)
	{
		var listener = card.AddComponent<GameEventListener>();
		listener.@event = evt;
		listener.response = new UnityEvent();
		listener.response.AddListener(() => spyMethod());
		evt.RegisterListener(listener);
	}

	private ReviveEffect CreateReviver(GameObject sourceCard)
	{
		return CreateEffect<ReviveEffect>(sourceCard);
	}

	private void WithRecorder(GameObject sourceCard, EffectScript effect, System.Action action)
	{
		EffectChainManager.MakeANewEffectRecorder(sourceCard, effect.gameObject);
		action();
		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void ReviveMyCards_GraveOnly_MovesGraveCardToTop()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		var grave = CreateCard(true, "Grave");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grave, start, live });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count, "Revive must not change deck size");
		Assert.AreSame(grave, CombatManager.combinedDeckZone[2], "Grave card should land at deck top");
		// Removing a grave-side card shifts everything up by one, then the revived card is appended:
		// [grave, start, live] -> [start, live, grave]. Reveal order: grave, live, then start.
		Assert.AreSame(start, CombatManager.combinedDeckZone[0], "Start Card boundary shifts up as the grave card leaves");
		Assert.AreSame(live, CombatManager.combinedDeckZone[1], "Live zone card keeps its relative order");
	}

	[Test]
	public void ReviveMyCards_EmptyGrave_Fizzles()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		Assert.DoesNotThrow(() => effect.ReviveMyCards(1), "Empty grave must fizzle without exceptions");
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);
		Assert.AreSame(live, CombatManager.combinedDeckZone[1], "Live card must stay in the live zone");
		Assert.AreEqual(0, ValueTrackerManager.ownerRevivedCountRef.value, "No counter movement on fizzle");
	}

	[Test]
	public void ReviveMyCards_FactionFilter_NeverPicksEnemyGrave()
	{
		var graveEnemy = CreateCard(false, "GraveEnemy");
		var graveOwner = CreateCard(true, "GraveOwner");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveEnemy, graveOwner, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreSame(graveOwner, CombatManager.combinedDeckZone[2], "Only the friendly grave card is revivable");
		Assert.AreSame(graveEnemy, CombatManager.combinedDeckZone[0], "Enemy grave card must stay in the grave");
	}

	[Test]
	public void ReviveTheirCards_PicksEnemyGraveCard()
	{
		var graveOwner = CreateCard(true, "GraveOwner");
		var graveEnemy = CreateCard(false, "GraveEnemy");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveOwner, graveEnemy, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		WithRecorder(source, effect, () => effect.ReviveTheirCards(1));

		Assert.AreSame(graveEnemy, CombatManager.combinedDeckZone[2], "Enemy grave card should be revived to top");
		Assert.AreSame(graveOwner, CombatManager.combinedDeckZone[0], "Friendly grave card must stay");
	}

	[Test]
	public void DelayedRevive_LandsAtStartCardTail()
	{
		var start = CreateStartCard();
		var liveA = CreateCard(true, "LiveA");
		var liveB = CreateCard(true, "LiveB");
		var grave = CreateCard(true, "Grave");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grave, start, liveA, liveB });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);
		effect.delayedRevive = true;

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreEqual(4, CombatManager.combinedDeckZone.Count);
		Assert.AreSame(start, CombatManager.combinedDeckZone[0], "Start Card stays at the boundary");
		Assert.AreSame(grave, CombatManager.combinedDeckZone[1], "Delayed revive lands at startCardIndex + 1 (R2 bounce slot)");
	}

	[Test]
	public void ReviveMyCards_ExcludesPassiveCards()
	{
		var passive = CreateCard(true, "Passive");
		passive.GetComponent<CardScript>().isPassive = true;
		var normal = CreateCard(true, "Normal");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { passive, normal, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreSame(normal, CombatManager.combinedDeckZone[2], "Passive card is never revivable; the normal card is picked");
		Assert.AreSame(passive, CombatManager.combinedDeckZone[0], "Passive card must stay in the grave");
	}

	[Test]
	public void ReviveMyCards_TypeIdFilter_PicksOnlyMatchingType()
	{
		var believer = CreateCard(true, "Believer", "RIFT");
		var other = CreateCard(true, "Other", "OTHER_TYPE");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { other, believer, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);
		effect.typeIDFilter = "RIFT";

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreSame(believer, CombatManager.combinedDeckZone[2], "typeIDFilter must select only matching grave cards");
	}

	[Test]
	public void Revive_RaisesAwakenEventFamily()
	{
		var start = CreateStartCard();
		var grave = CreateCard(true, "Grave");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grave, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		AttachSpyListener(grave, GameEventStorage.onMeRevived, () => _spy.OnMeRevived());
		AttachSpyListener(source, GameEventStorage.onAnyCardRevived, () => _spy.OnAnyRevived());
		AttachSpyListener(grave, GameEventStorage.onFriendlyCardRevived, () => _spy.OnFriendlyRevived());

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreEqual(1, _spy.meRevivedCount, "onMeRevived (苏醒) must raise once on the revived card");
		Assert.AreEqual(1, _spy.anyRevivedCount, "onAnyCardRevived must raise once");
		Assert.AreEqual(1, _spy.friendlyRevivedCount, "onFriendlyCardRevived must raise for an owner-side revival");
	}

	[Test]
	public void StageSelf_DoesNotRaiseAwaken()
	{
		var start = CreateStartCard();
		var grave = CreateCard(true, "Grave");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grave, start });
		AttachSpyListener(grave, GameEventStorage.onMeRevived, () => _spy.OnMeRevived());
		var stage = CreateEffect<StageEffect>(grave);

		WithRecorder(grave, stage, () => stage.StageSelf());

		Assert.AreSame(grave, CombatManager.combinedDeckZone[1], "Stage should still move the card to top (sanity)");
		Assert.AreEqual(0, _spy.meRevivedCount, "苏醒 must NOT fire on Stage — only revive effects trigger it");
	}

	[Test]
	public void ReviveCounters_IncrementAndResetPerRound()
	{
		var start = CreateStartCard();
		var grave = CreateCard(true, "Grave");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grave, start });
		var source = CreateCard(true, "Reviver");
		var effect = CreateReviver(source);

		WithRecorder(source, effect, () => effect.ReviveMyCards(1));

		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountRef.value, "Cumulative owner revive count increments");
		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountThisRoundRef.value, "Per-round owner revive count increments");

		var handleNewRoundStart = typeof(CombatManager).GetMethod("HandleNewRoundStart", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.IsNotNull(handleNewRoundStart, "HandleNewRoundStart should exist");
		handleNewRoundStart.Invoke(CombatManager, null);

		Assert.AreEqual(1, ValueTrackerManager.ownerRevivedCountRef.value, "Cumulative count survives the round start");
		Assert.AreEqual(0, ValueTrackerManager.ownerRevivedCountThisRoundRef.value, "Per-round count resets at round start");
	}
}
