using UnityEngine;

/// <summary>
/// Upgrade-slot meter card effect (plans/plan-upgrade-slot-cap-2026-09-30.md): each purchase
/// adds <c>amount</c> upgrade slots AND the same amount to the run-persistent purchase counter,
/// so the shop-entry formula (upgradeCapOg + perSession + purchases, clamped to the static
/// maxUpgradeSlots ceiling) reproduces the current cap. The card itself never enters the player
/// deck (self-exile in ShopManager.BuyFunc), mirroring DeckSizeIncreaseEffect.
/// </summary>
public class UpgradeCapIncreaseEffect : EffectScript
{
	public IntSO myUpgradeCap;
	public IntSO maxUpgradeSlots;
	[Tooltip("Run-persistent purchase counter (reset at run start), bumped by the same amount. Null = legacy behavior, no counter.")]
	public IntSO upgradeSlotPurchasesRef;

	public void IncreaseUpgradeCapBy(int amount)
	{
		if (upgradeSlotPurchasesRef != null)
		{
			upgradeSlotPurchasesRef.value += amount;
		}
		myUpgradeCap.value += amount;
		myUpgradeCap.value = Mathf.Clamp(myUpgradeCap.value, 1, maxUpgradeSlots.value);

		// The Upgrades panel counter shows the cap as its denominator; refresh it when the
		// meter fires outside the standard buy path (BuyFunc refreshes anyway).
		ShopSectionPanels.RefreshIfActive();
	}
}
