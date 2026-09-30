using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the upgrade-slot meter card (plans/plan-upgrade-slot-cap-2026-09-30.md):
/// each purchase bumps the run-persistent purchase counter by the same amount as the upgrade
/// cap, clamped at the static ceiling; the shop-entry formula reproduces the clamped cap.
/// </summary>
public class UpgradeCapMeterTests
{
	private IntSO _upgradeCap;
	private IntSO _maxUpgradeSlots;
	private IntSO _purchases;
	private UpgradeCapIncreaseEffect _effect;

	[SetUp]
	public void SetUp()
	{
		_upgradeCap = ScriptableObject.CreateInstance<IntSO>();
		_upgradeCap.value = 3;
		_maxUpgradeSlots = ScriptableObject.CreateInstance<IntSO>();
		_maxUpgradeSlots.value = 12;
		_purchases = ScriptableObject.CreateInstance<IntSO>();
		var go = new GameObject("UpgradeCapCard");
		_effect = go.AddComponent<UpgradeCapIncreaseEffect>();
		_effect.myUpgradeCap = _upgradeCap;
		_effect.maxUpgradeSlots = _maxUpgradeSlots;
		_effect.upgradeSlotPurchasesRef = _purchases;
	}

	[TearDown]
	public void TearDown()
	{
		Object.DestroyImmediate(_effect.gameObject);
		Object.DestroyImmediate(_upgradeCap);
		Object.DestroyImmediate(_maxUpgradeSlots);
		Object.DestroyImmediate(_purchases);
	}

	[Test]
	public void Increase_BumpsCounterAndUpgradeCap()
	{
		_effect.IncreaseUpgradeCapBy(1);
		Assert.AreEqual(4, _upgradeCap.value);
		Assert.AreEqual(1, _purchases.value);
	}

	[Test]
	public void Increase_ClampsAtCeiling_CounterStillAdvances()
	{
		_upgradeCap.value = 12;
		_effect.IncreaseUpgradeCapBy(1);
		Assert.AreEqual(12, _upgradeCap.value);
		Assert.AreEqual(1, _purchases.value); // formula clamps at the same ceiling on next shop entry
	}

	[Test]
	public void NullCounter_LegacyBehavior_NoThrow()
	{
		_effect.upgradeSlotPurchasesRef = null;
		Assert.DoesNotThrow(() => _effect.IncreaseUpgradeCapBy(1));
		Assert.AreEqual(4, _upgradeCap.value);
	}

	[Test]
	public void ShopEntryFormula_ReproducesClampedUpgradeCap()
	{
		_effect.IncreaseUpgradeCapBy(1);
		_effect.IncreaseUpgradeCapBy(1);
		int recomputed = UtilityShopBonus.ComputeUpgradeCap(
			3, 0, 1, 1, _purchases.value, _maxUpgradeSlots.value);
		Assert.AreEqual(_upgradeCap.value, recomputed);
	}

	[Test]
	public void GetUpgradeSlotPrice_EscalatesPerPurchase()
	{
		Assert.AreEqual(4, UtilityShopBonus.GetUpgradeSlotPrice(4, 2, 0));
		Assert.AreEqual(6, UtilityShopBonus.GetUpgradeSlotPrice(4, 2, 1));
		Assert.AreEqual(8, UtilityShopBonus.GetUpgradeSlotPrice(4, 2, 2));
	}
}
