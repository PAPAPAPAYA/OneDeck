using NUnit.Framework;
using UnityEngine;

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
}
