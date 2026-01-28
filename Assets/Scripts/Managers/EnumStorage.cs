using System.Collections.Generic;
using UnityEngine;

public class EnumStorage : MonoBehaviour
{
	public enum GamePhase
	{
		Combat,
		Shop,
		Result
	}

	public enum CombatState
	{
		GatherDeckLists,
		ShuffleDeck,
		Reveal
	}

	public enum TargetType
	{
		Me,
		Them,
		Random
	}

	public enum StatusEffect
	{
		None,
		Infected,
		Mana,
		HeartChanged,
		Power
	}

	public static bool DoesListContainAmountOfTag(List<StatusEffect> listToCheck, int amount, StatusEffect statusEffectToCheck)
	{
		var amountOfTag = 0;
		foreach (var listStatusEffect in listToCheck)
		{
			if (listStatusEffect == statusEffectToCheck)
			{
				amountOfTag++;
			}
		}
		return amountOfTag >= amount;
	}
}