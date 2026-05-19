using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEngine;

public class ShieldAlterTests : HeadlessCombatTestFixture
{
	[Test]
	public void UpMyShield_IncreasesShield()
	{
		var card = CreateCard(true, "ShieldGiver");
		var shield = CreateEffect<ShieldAlterEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, shield.gameObject);
		shield.UpMyShield(5);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(5, OwnerStatus.shield, "Should increase shield by 5");
	}

	[Test]
	public void UpMyShield_RaisesShieldUppedEvent()
	{
		var card = CreateCard(true, "ShieldGiver");
		var shield = CreateEffect<ShieldAlterEffect>(card);

		bool eventRaised = false;
		RegisterEventCallback(GameEventStorage.onMyPlayerShieldUpped, () => eventRaised = true);

		EffectChainManager.MakeANewEffectRecorder(card, shield.gameObject);
		shield.UpMyShield(3);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsTrue(eventRaised, "onMyPlayerShieldUpped should be raised");
	}

	[Test]
	public void UpMyShield_EnemyCard_RaisesEnemyShieldEvent()
	{
		var card = CreateCard(false, "EnemyShieldGiver");
		var shield = CreateEffect<ShieldAlterEffect>(card);

		bool enemyShieldRaised = false;
		// Enemy card upping self shield triggers: onMyPlayerShieldUpped.RaiseOpponent() + onTheirPlayerShieldUpped.RaiseOwner()
		// RaiseOpponent triggers listeners with enemy status ref
		RegisterEventCallback(GameEventStorage.onMyPlayerShieldUpped, () => enemyShieldRaised = true, EnemyStatus);

		EffectChainManager.MakeANewEffectRecorder(card, shield.gameObject);
		shield.UpMyShield(4);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsTrue(enemyShieldRaised, "onMyPlayerShieldUpped should be raised for enemy shield via RaiseOpponent");
		Assert.AreEqual(4, EnemyStatus.shield, "Enemy shield should increase by 4");
	}
}
