using DefaultNamespace.Managers;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for ShopRarityWeightSO.GetOddsPercents — the normalized per-tier
/// roll odds shown on the shop chrome rarity chips (plans/plan-shop-panels-port-2026-09-18.md, Task 2).
/// </summary>
public class ShopRarityWeightSOTests
{
	private readonly System.Collections.Generic.List<ShopRarityWeightSO> _created =
		new System.Collections.Generic.List<ShopRarityWeightSO>();

	[TearDown]
	public void TearDownCreated()
	{
		foreach (ShopRarityWeightSO so in _created)
		{
			if (so != null) Object.DestroyImmediate(so);
		}
		_created.Clear();
	}

	private ShopRarityWeightSO MakeWeights(float common, float uncommon, float rare)
	{
		ShopRarityWeightSO so = ScriptableObject.CreateInstance<ShopRarityWeightSO>();
		so.entries.Add(new ShopRarityWeightSO.RarityWeightEntry { rarity = EnumStorage.Rarity.Common, weight = common });
		so.entries.Add(new ShopRarityWeightSO.RarityWeightEntry { rarity = EnumStorage.Rarity.Uncommon, weight = uncommon });
		so.entries.Add(new ShopRarityWeightSO.RarityWeightEntry { rarity = EnumStorage.Rarity.Rare, weight = rare });
		_created.Add(so);
		return so;
	}

	[Test]
	public void GetOddsPercents_WeightsNormalizeToHundred()
	{
		ShopRarityWeightSO so = MakeWeights(90f, 9f, 1f);
		so.GetOddsPercents(out float c, out float u, out float r);
		Assert.AreEqual(90f, c, 0.01f);
		Assert.AreEqual(9f, u, 0.01f);
		Assert.AreEqual(1f, r, 0.01f);
	}

	[Test]
	public void GetOddsPercents_MissingTierCountsAsWeightOne()
	{
		ShopRarityWeightSO so = MakeWeights(1f, 1f, 0f);
		so.entries.RemoveAll(e => e.rarity == EnumStorage.Rarity.Rare);
		so.GetOddsPercents(out float c, out float u, out float r);
		Assert.AreEqual(33.3333f, c, 0.01f);
		Assert.AreEqual(33.3333f, u, 0.01f);
		Assert.AreEqual(33.3333f, r, 0.01f);
	}

	[Test]
	public void GetOddsPercents_ZeroTotalReturnsZeros()
	{
		ShopRarityWeightSO so = MakeWeights(0f, 0f, 0f);
		so.GetOddsPercents(out float c, out float u, out float r);
		Assert.AreEqual(0f, c);
		Assert.AreEqual(0f, u);
		Assert.AreEqual(0f, r);
	}
}
