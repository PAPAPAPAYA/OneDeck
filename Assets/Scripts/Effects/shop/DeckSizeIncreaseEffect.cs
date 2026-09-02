using System;
using UnityEngine;

/// <summary>
/// Deck-slot meter card effect (v2, plans/plan-utility-passive-shop-pipeline-2026-08-31.md step 4).
/// Buy-time token: each purchase adds <see cref="amount"/> deck slots AND the same amount to the
/// run-persistent purchase counter, so the shop-entry formula (deckSizeOg + perSession + purchases,
/// clamped to the static maxDeckSize ceiling) reproduces the current deck size. The card itself
/// never enters the player deck (self-exile in ShopManager.BuyFunc) - this is the documented
/// single exception to shop-bonus recompute purity.
/// </summary>
public class DeckSizeIncreaseEffect : EffectScript
{
	public IntSO myDeckSize;
	public IntSO maxDeckSize;
	[Tooltip("v2 meter: run-persistent purchase counter (reset at run start), bumped by the same amount. Null = legacy behavior, no counter.")]
	public IntSO deckSlotPurchasesRef;

	public void IncreaseDeckSizeBy(int amount)
	{
		if (deckSlotPurchasesRef != null)
		{
			deckSlotPurchasesRef.value += amount;
		}
		myDeckSize.value += amount;
		myDeckSize.value = Mathf.Clamp(myDeckSize.value, 1, maxDeckSize.value);

		// Notify ShopUXManager to spawn new placeholder cards
		ShopUXManager.Instance?.SpawnAdditionalEmptySpaces();
	}
}
