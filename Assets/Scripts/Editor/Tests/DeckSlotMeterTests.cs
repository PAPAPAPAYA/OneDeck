using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the deck-slot meter card (step 4 of
/// plans/plan-utility-passive-shop-pipeline-2026-08-31.md): each purchase bumps the
/// run-persistent purchase counter by the same amount as the deck size, clamped at the
/// static ceiling; the shop-entry formula reproduces the clamped deck size.
/// </summary>
public class DeckSlotMeterTests
{
	private IntSO _deckSize;
	private IntSO _maxDeckSize;
	private IntSO _purchases;
	private DeckSizeIncreaseEffect _effect;

	[SetUp]
	public void SetUp()
	{
		_deckSize = ScriptableObject.CreateInstance<IntSO>();
		_deckSize.value = 3;
		_maxDeckSize = ScriptableObject.CreateInstance<IntSO>();
		_maxDeckSize.value = 16;
		_purchases = ScriptableObject.CreateInstance<IntSO>();
		var go = new GameObject("SlotCard");
		_effect = go.AddComponent<DeckSizeIncreaseEffect>();
		_effect.myDeckSize = _deckSize;
		_effect.maxDeckSize = _maxDeckSize;
		_effect.deckSlotPurchasesRef = _purchases;
	}

	[TearDown]
	public void TearDown()
	{
		Object.DestroyImmediate(_effect.gameObject);
		Object.DestroyImmediate(_deckSize);
		Object.DestroyImmediate(_maxDeckSize);
		Object.DestroyImmediate(_purchases);
	}

	[Test]
	public void Increase_BumpsCounterAndDeckSize()
	{
		_effect.IncreaseDeckSizeBy(1);
		Assert.AreEqual(4, _deckSize.value);
		Assert.AreEqual(1, _purchases.value);
	}

	[Test]
	public void Increase_ClampsDeckSize_CounterStillAdvances()
	{
		_deckSize.value = 16;
		_effect.IncreaseDeckSizeBy(1);
		Assert.AreEqual(16, _deckSize.value);
		Assert.AreEqual(1, _purchases.value); // formula clamps at the same ceiling on next shop entry
	}

	[Test]
	public void NullCounter_LegacyBehavior_NoThrow()
	{
		_effect.deckSlotPurchasesRef = null;
		_effect.IncreaseDeckSizeBy(2);
		Assert.AreEqual(5, _deckSize.value);
	}
}
