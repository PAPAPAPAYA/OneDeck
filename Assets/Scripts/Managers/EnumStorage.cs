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
		DeathRattle,
		// 4.0 keyword tags (Notion "4.0 card database" tag column, synced 2026-09-01).
		// Append-only: prefabs serialize tags as ints — inserting or reordering corrupts existing assets.
		Bury,
		Enhance,
		Believer,
		Exile,
		Curse,
		Awaken,
		Passive,
		Revive,
		EnhanceReaction,
		MultiAttack
	}

	// Card type taxonomy (2026-09-02, plans/plan-card-type-status-2026-09-02.md).
	// Creature = attack-bearing creature (4.0 生物, ATK column non-empty).
	// Status = curse-type tokens (诅咒, e.g. JU_ON) that grow attack via EnhanceCurse but are not creatures.
	// Append-only: prefabs serialize this as ints - inserting or reordering corrupts existing assets.
	public enum CardType
	{
		None,
		Creature,
		Status
	}

	public enum Rarity
	{
		Common,
		Uncommon,
		Rare
	}

	// Utility card classification (shop utility passives, plans/plan-utility-passive-shop-pipeline-2026-08-31).
	// Append-only: prefabs serialize this as ints - inserting or reordering corrupts existing assets.
	public enum UtilityKind
	{
		None,
		HpMax,
		Income,
		ShopOption,
		FreeReroll,
		RaritySlotU,
		RaritySlotR,
		RarityWeight,
		RerollDiscount,
		OddsUtility,
		ReservedTag,
		RerollCreatureWave,
		RerollSpellWave
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
