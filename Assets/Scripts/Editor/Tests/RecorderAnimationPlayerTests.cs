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
}
