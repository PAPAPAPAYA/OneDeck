using System.Collections;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EffectChainTests : HeadlessCombatTestFixture
{
	[Test]
	public void SameEffectID_CannotBeInvokedTwiceInOpenChain()
	{
		var card = CreateCard(true, "TestCard");
		var effectObj = CreateGameObject("EffectObj");
		effectObj.transform.SetParent(card.transform);

		EffectChainManager.MakeANewEffectRecorder(card, effectObj);

		bool first = EffectChainManager.EffectCanBeInvoked("effect_001");
		bool second = EffectChainManager.EffectCanBeInvoked("effect_001");

		Assert.IsTrue(first, "First invocation should succeed");
		Assert.IsFalse(second, "Second invocation of same effect ID should be blocked");
	}

	[Test]
	public void ChainDepthExceeds99_BlocksFurtherEffects()
	{
		UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "ERROR: chain depth reached limit");

		var card = CreateCard(true, "TestCard");
		var effectObj = CreateGameObject("EffectObj");
		effectObj.transform.SetParent(card.transform);

		EffectChainManager.MakeANewEffectRecorder(card, effectObj);
		EffectChainManager.chainDepth = 100;

		bool result = EffectChainManager.EffectCanBeInvoked("deep_effect");

		Assert.IsFalse(result, "Effect should be blocked when chain depth exceeds 99");
	}

	[Test]
	public void DifferentEffectObject_OnSameCard_StartsNewChain()
	{
		var card = CreateCard(true, "TestCard");
		var effectA = CreateGameObject("EffectA");
		effectA.transform.SetParent(card.transform);
		var effectB = CreateGameObject("EffectB");
		effectB.transform.SetParent(card.transform);

		// Open first chain
		EffectChainManager.CheckShouldIStartANewChain(card, effectA);
		EffectChainManager.MakeANewEffectRecorder(card, effectA);
		Assert.AreEqual(1, EffectChainManager.openedEffectRecorders.Count, "First chain should be opened");

		// Same card, different effect object -> should close old chain and start new
		EffectChainManager.CheckShouldIStartANewChain(card, effectB);
		Assert.AreEqual(0, EffectChainManager.openedEffectRecorders.Count, "Old chain should be closed");
	}

	[Test]
	public void SameEffectObject_DoesNotStartNewChain()
	{
		var card = CreateCard(true, "TestCard");
		var effectA = CreateGameObject("EffectA");
		effectA.transform.SetParent(card.transform);

		EffectChainManager.CheckShouldIStartANewChain(card, effectA);
		EffectChainManager.MakeANewEffectRecorder(card, effectA);
		Assert.AreEqual(1, EffectChainManager.openedEffectRecorders.Count, "First chain should be opened");

		// Same card, same effect object -> should NOT close chain
		EffectChainManager.CheckShouldIStartANewChain(card, effectA);
		Assert.AreEqual(1, EffectChainManager.openedEffectRecorders.Count, "Chain should remain open for same effect object");
	}

	[Test]
	public void CloseOpenedChain_FinalizesRecorders()
	{
		var card = CreateCard(true, "TestCard");
		var effectObj = CreateGameObject("EffectObj");
		effectObj.transform.SetParent(card.transform);

		EffectChainManager.MakeANewEffectRecorder(card, effectObj);
		Assert.AreEqual(1, EffectChainManager.openedEffectRecorders.Count);

		EffectChainManager.CloseOpenedChain();

		Assert.AreEqual(0, EffectChainManager.openedEffectRecorders.Count);
		Assert.AreEqual(1, EffectChainManager.closedEffectRecorders.Count);
	}

	[Test]
	public void AnimationRequest_CapturedOnEffectRecorder()
	{
		// Ensure RecorderAnimationPlayer exists for capture path (Awake may not fire in Edit Mode)
		if (RecorderAnimationPlayer.me == null)
		{
			var rapGo = new GameObject("RecorderAnimationPlayer");
			var rap = rapGo.AddComponent<RecorderAnimationPlayer>();
			RecorderAnimationPlayer.me = rap;
		}

		var card = CreateCard(true, "TestCard");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.isStatusEffectDamage = false;
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 3;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(1, recorder.animationRequests.Count, "Should have 1 animation request");
		Assert.AreEqual(AnimationRequestType.Attack, recorder.animationRequests[0].type, "Should be Attack type");
		Assert.AreEqual(card, recorder.animationRequests[0].attackerCard, "Attacker should be the card");
	}

	[Test]
	public void AnimationRequest_BatchMoveCaptured()
	{
		// Ensure RecorderAnimationPlayer exists for capture path (Awake may not fire in Edit Mode)
		if (RecorderAnimationPlayer.me == null)
		{
			var rapGo = new GameObject("RecorderAnimationPlayer");
			var rap = rapGo.AddComponent<RecorderAnimationPlayer>();
			RecorderAnimationPlayer.me = rap;
		}

		var effectCard = CreateCard(true, "BuryCard");
		var target1 = CreateCard(false, "Target1");
		var target2 = CreateCard(false, "Target2");

		CombatManager.combinedDeckZone.Clear();
		CombatManager.combinedDeckZone.Add(CreateCard(true, "DummyBottom"));
		CombatManager.combinedDeckZone.Add(target1);
		CombatManager.combinedDeckZone.Add(target2);

		var buryEffect = CreateEffect<BuryEffect>(effectCard);
		EffectChainManager.MakeANewEffectRecorder(effectCard, buryEffect.gameObject);

		buryEffect.BuryTheirCards(2);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(1, recorder.animationRequests.Count, "Should have 1 batch request");
		Assert.AreEqual(AnimationRequestType.MoveToBottomBatch, recorder.animationRequests[0].type);
		Assert.AreEqual(2, recorder.animationRequests[0].targetCards.Count, "Should bury 2 cards");
	}

	[Test]
	public void RecorderTree_NavigatesViaTransform()
	{
		var rootGo = CreateGameObject("RootRecorder");
		var root = rootGo.AddComponent<EffectRecorder>();

		var childGo = CreateGameObject("ChildRecorder");
		var child = childGo.AddComponent<EffectRecorder>();
		child.transform.SetParent(rootGo.transform);

		Assert.AreEqual(rootGo.transform, childGo.transform.parent, "Child should be parented to root via Transform only");
	}

	[UnityTest]
	public IEnumerator AnimationPlayedFlag_SetAfterPlayback()
	{
		var rootGo = CreateGameObject("RootRecorder");
		var root = rootGo.AddComponent<EffectRecorder>();
		root.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.Attack });

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		yield return player.PlayRecorderCoroutine(root);

		Assert.IsTrue(root.animationPlayed, "Root should be marked as played after playback");
	}

	[Test]
	public void CloseOpenedChain_DoesNotTriggerPlayback()
	{
		var card = CreateCard(true, "TestCard");
		var effectObj = CreateGameObject("EffectObj");
		effectObj.transform.SetParent(card.transform);

		EffectChainManager.MakeANewEffectRecorder(card, effectObj);
		Assert.AreEqual(1, EffectChainManager.openedEffectRecorders.Count);

		EffectChainManager.CloseOpenedChain();

		Assert.AreEqual(0, EffectChainManager.openedEffectRecorders.Count);
		Assert.AreEqual(1, EffectChainManager.closedEffectRecorders.Count);

		var recorder = EffectChainManager.closedEffectRecorders[0].GetComponent<EffectRecorder>();
		Assert.IsFalse(recorder.animationPlayed, "CloseOpenedChain should NOT trigger animation playback");
	}
}
