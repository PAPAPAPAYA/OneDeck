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
	}
}
