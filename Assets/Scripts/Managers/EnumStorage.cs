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
		Me, // card owner's deck
		Them, // opponent's deck
		Random
	}

	public enum StatusEffect
	{
		None,
		Infected,
		Mana,
		HeartChanged,
		Power,
		Rest,
		Revive
	}

	public enum Tag
	{
		
	}

	public static bool DoesListContainAmountOfStatusEffect(List<StatusEffect> listToCheck, int amount, StatusEffect statusEffectToCheck)
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