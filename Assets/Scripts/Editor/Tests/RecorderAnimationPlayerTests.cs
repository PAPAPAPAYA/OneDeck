using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RecorderAnimationPlayerTests : HeadlessCombatTestFixture
{
	[UnityTest]
	public IEnumerator PlayRecorderCoroutine_MarksAnimationPlayed()
	{
		var rootGo = CreateGameObject("RootRecorder");
		var root = rootGo.AddComponent<EffectRecorder>();
		root.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.Attack });

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		Assert.IsFalse(root.animationPlayed, "Should not be played before coroutine");

		yield return player.PlayRecorderCoroutine(root);

		Assert.IsTrue(root.animationPlayed, "Should be marked as played after coroutine completes");
	}

	[UnityTest]
	public IEnumerator PlayRequestCoroutine_Attack_CallsPlayAttackAnimation()
	{
		var attacker = CreateCard(true, "Attacker");
		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		var request = new AnimationRequest
		{
			type = AnimationRequestType.Attack,
			attackerCard = attacker,
			isAttackingEnemy = true
		};

		int callsBefore = NullVisuals.playAttackAnimCalls;
		yield return player.PlayRequestCoroutine(request);
		int callsAfter = NullVisuals.playAttackAnimCalls;

		Assert.AreEqual(callsBefore + 1, callsAfter, "Should call PlayAttackAnimation once");
	}

	[UnityTest]
	public IEnumerator PlayRequestCoroutine_Destroy_CallsDestroyCard()
	{
		var target = CreateCard(true, "Target");
		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		var request = new AnimationRequest
		{
			type = AnimationRequestType.Destroy,
			targetCard = target
		};

		int callsBefore = NullVisuals.destroyCardCalls;
		yield return player.PlayRequestCoroutine(request);
		int callsAfter = NullVisuals.destroyCardCalls;

		Assert.AreEqual(callsBefore + 1, callsAfter, "Should call DestroyCardWithAnimation once");
	}

	[UnityTest]
	public IEnumerator PlayRequestCoroutine_MoveToBottomBatch_CallsMoveCardToIndex()
	{
		var card1 = CreateCard(true, "Card1");
		var card2 = CreateCard(true, "Card2");
		CombatManager.combinedDeckZone.Add(card1);
		CombatManager.combinedDeckZone.Add(card2);

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		var request = new AnimationRequest
		{
			type = AnimationRequestType.MoveToBottomBatch,
			targetCards = new System.Collections.Generic.List<GameObject> { card1, card2 },
			targetIndices = new System.Collections.Generic.List<int> { 0, 0 },
			snapshotDeckSize = 2
		};

		int callsBefore = NullVisuals.moveCardToIndexCalls;
		yield return player.PlayRequestCoroutine(request);
		int callsAfter = NullVisuals.moveCardToIndexCalls;

		Assert.AreEqual(callsBefore + 2, callsAfter, "Should call MoveCardToIndex for each card in batch");
	}

	[UnityTest]
	public IEnumerator PlayRequestCoroutine_MoveToTopBatch_CallsMoveCardToIndex()
	{
		var card1 = CreateCard(true, "Card1");
		var card2 = CreateCard(true, "Card2");
		CombatManager.combinedDeckZone.Add(card1);
		CombatManager.combinedDeckZone.Add(card2);

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		var request = new AnimationRequest
		{
			type = AnimationRequestType.MoveToTopBatch,
			targetCards = new System.Collections.Generic.List<GameObject> { card1, card2 },
			targetIndices = new System.Collections.Generic.List<int> { 1, 1 },
			snapshotDeckSize = 2
		};

		int callsBefore = NullVisuals.moveCardToIndexCalls;
		yield return player.PlayRequestCoroutine(request);
		int callsAfter = NullVisuals.moveCardToIndexCalls;

		Assert.AreEqual(callsBefore + 2, callsAfter, "Should call MoveCardToIndex for each card in batch");
	}

	[UnityTest]
	public IEnumerator PlayRequestCoroutine_StatusEffectChange_CallsApplyStatusTint()
	{
		var target = CreateCard(true, "Target");
		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		var request = new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectChange,
			targetCard = target,
			statusEffect = EnumStorage.StatusEffect.Power,
			statusEffectAmount = 1
		};

		int logCountBefore = NullVisuals.callLog.Count;
		yield return player.PlayRequestCoroutine(request);
		bool foundApplyTint = false;
		for (int i = logCountBefore; i < NullVisuals.callLog.Count; i++)
		{
			if (NullVisuals.callLog[i].StartsWith("ApplyStatusTint"))
			{
				foundApplyTint = true;
				break;
			}
		}

		Assert.IsTrue(foundApplyTint, "Should call ApplyStatusTint for Power status effect");
	}

	[UnityTest]
	public IEnumerator PlayRecordersCoroutine_ProcessesMultipleRootRecorders()
	{
		var root1 = CreateGameObject("Root1").AddComponent<EffectRecorder>();
		root1.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.Attack });

		var root2 = CreateGameObject("Root2").AddComponent<EffectRecorder>();
		root2.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.Attack });

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		var roots = new System.Collections.Generic.List<GameObject>
		{
			root1.gameObject,
			root2.gameObject
		};

		yield return player.PlayRecordersCoroutine(roots);

		Assert.IsTrue(root1.animationPlayed, "Root1 should be marked as played");
		Assert.IsTrue(root2.animationPlayed, "Root2 should be marked as played");
	}

	[UnityTest]
	public IEnumerator PlayRecorderCoroutine_OffRevealSourceCard_PopsUpBeforeRequests()
	{
		var source = CreateCard(true, "SourceCard");
		var target = CreateCard(true, "TargetCard");

		var recorderGo = CreateGameObject("RootRecorder");
		var recorder = recorderGo.AddComponent<EffectRecorder>();
		recorder.cardObject = source;
		recorder.sourceWasInRevealZone = false;
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectChange,
			targetCard = target,
			statusEffect = EnumStorage.StatusEffect.Power,
			statusEffectAmount = 1
		});

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		int popUpsBefore = NullVisuals.popUpCardCalls;

		yield return player.PlayRecorderCoroutine(recorder);

		Assert.AreEqual(popUpsBefore + 1, NullVisuals.popUpCardCalls, "Off-reveal source card should be popped up automatically");
		int popUpIndex = NullVisuals.callLog.FindIndex(x => x.StartsWith("PopUpCard"));
		int statusChangeIndex = NullVisuals.callLog.FindIndex(x => x.StartsWith("ApplyStatusTint"));
		Assert.Less(popUpIndex, statusChangeIndex, "PopUp should happen before the effect request");
	}

	[UnityTest]
	public IEnumerator PlayRecorderCoroutine_OffRevealSourceCard_SkipsBuiltInSlotIn()
	{
		var source = CreateCard(true, "SourceCard");

		var recorderGo = CreateGameObject("RootRecorder");
		var recorder = recorderGo.AddComponent<EffectRecorder>();
		recorder.cardObject = source;
		recorder.sourceWasInRevealZone = false;
		recorder.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.SlotIn, targetCard = source });

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		yield return player.PlayRecorderCoroutine(recorder);

		Assert.IsFalse(NullVisuals.callLog.Exists(x => x.StartsWith("SlotInCard")), "Built-in SlotIn for source card should be skipped; auto-slotin is responsible");
	}

	[UnityTest]
	public IEnumerator PlayRecorderCoroutine_OffRevealAttackRecorder_DoesNotPopUp()
	{
		var source = CreateCard(true, "SourceCard");

		var recorderGo = CreateGameObject("RootRecorder");
		var recorder = recorderGo.AddComponent<EffectRecorder>();
		recorder.cardObject = source;
		recorder.sourceWasInRevealZone = false;
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.Attack,
			attackerCard = source,
			isAttackingEnemy = true
		});

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		int popUpsBefore = NullVisuals.popUpCardCalls;

		yield return player.PlayRecorderCoroutine(recorder);

		Assert.AreEqual(popUpsBefore, NullVisuals.popUpCardCalls, "Off-reveal Attack recorder should not auto-popup");
		Assert.AreEqual(1, NullVisuals.playAttackAnimCalls, "Attack request should still play");
	}

	[UnityTest]
	public IEnumerator PlayRecordersCoroutine_SameSourceMultipleRecorders_PopsUpOnce()
	{
		var source = CreateCard(true, "SourceCard");

		var r1 = CreateGameObject("R1").AddComponent<EffectRecorder>();
		r1.cardObject = source;
		r1.sourceWasInRevealZone = false;
		r1.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectChange,
			targetCard = source,
			statusEffect = EnumStorage.StatusEffect.Power,
			statusEffectAmount = 1
		});

		var r2 = CreateGameObject("R2").AddComponent<EffectRecorder>();
		r2.cardObject = source;
		r2.sourceWasInRevealZone = false;
		r2.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectChange,
			targetCard = source,
			statusEffect = EnumStorage.StatusEffect.Power,
			statusEffectAmount = 1
		});

		var roots = new System.Collections.Generic.List<GameObject>
		{
			r1.gameObject,
			r2.gameObject
		};

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		int popUpsBefore = NullVisuals.popUpCardCalls;

		yield return player.PlayRecordersCoroutine(roots);

		Assert.AreEqual(popUpsBefore + 1, NullVisuals.popUpCardCalls, "Same source card across multiple recorders should popup only once");
	}


}
