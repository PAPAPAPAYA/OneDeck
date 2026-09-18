using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	[CreateAssetMenu(fileName = "ShopRarityWeightSO", menuName = "SORefs/ShopRarityWeightSO")]
	public class ShopRarityWeightSO : ScriptableObject
	{
		[Serializable]
		public class RarityWeightEntry
		{
			public EnumStorage.Rarity rarity;
			[Tooltip("Base weight for this rarity in shop rolls")]
			public float weight = 1f;
		}

		[Tooltip("Weight table for each rarity tier")]
		public List<RarityWeightEntry> entries = new List<RarityWeightEntry>();

		/// <summary>
		/// Gets the base weight for a given rarity. Returns 1f if not found.
		/// </summary>
		public float GetWeight(EnumStorage.Rarity rarity)
		{
			foreach (var entry in entries)
			{
				if (entry.rarity == rarity)
				{
					return entry.weight;
				}
			}
			return 1f;
		}

		/// <summary>
		/// Normalized roll odds per rarity tier (percent, 0-100) for HUD display.
		/// Tiers missing from the table count as weight 1 (matches GetWeight).
		/// An all-zero total yields 0/0/0.
		/// </summary>
		public void GetOddsPercents(out float commonPct, out float uncommonPct, out float rarePct)
		{
			float common = GetWeight(EnumStorage.Rarity.Common);
			float uncommon = GetWeight(EnumStorage.Rarity.Uncommon);
			float rare = GetWeight(EnumStorage.Rarity.Rare);
			float total = common + uncommon + rare;
			if (total <= 0f)
			{
				commonPct = uncommonPct = rarePct = 0f;
				return;
			}
			commonPct = common / total * 100f;
			uncommonPct = uncommon / total * 100f;
			rarePct = rare / total * 100f;
		}
	}
}
