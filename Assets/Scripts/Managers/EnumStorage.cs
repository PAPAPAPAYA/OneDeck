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
		// Deprecated 2026-08-29 (4.0 revive engine): legacy status-effect slot, no longer granted by any effect.
		// Keep the member: the enum has implicit values, removing it would shift Counter 7->6 and corrupt
		// every serialized asset that stores status effects as ints. The 4.0 revive is an effect (ReviveEffect), not a status.
		Revive,
		Counter
	}

	public enum Tag
	{
		None,
		Linger,
		ManaX,
		DeathRattle
	}

	public enum Rarity
	{
		Common,
		Uncommon,
		Rare
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

	public static int GetStatusEffectCount(List<StatusEffect> listToCheck, StatusEffect statusEffectToCheck)
	{
		var amountOfTag = 0;
		foreach (var listStatusEffect in listToCheck)
		{
			if (listStatusEffect == statusEffectToCheck)
			{
				amountOfTag++;
			}
		}
		return amountOfTag;
	}
}
