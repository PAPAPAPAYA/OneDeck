using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Maps each hover-visible CardType to its display name and explanation text
/// (StringSO each). Only types with a configured entry show a type block in the
/// card hover tooltip: Creature (实体 / 可攻击) and None (现象 / 不可攻击) — Token
/// cards (信徒/诅咒衍生物) deliberately have no entry and show nothing, same as
/// the Start Card (2026-09-14, plans/plan-cardtype-shiti-xianxiang-2026-09-14.md).
/// Access via the static lazy-loaded singleton: CardTypeTooltipDatabaseSO.Me
/// (asset must live at Assets/Resources/CardTypeTooltipDatabase.asset).
/// Display-name and description StringSO assets must have reset = false,
/// otherwise StringSO.OnEnable wipes the text.
/// </summary>
[CreateAssetMenu(fileName = "CardTypeTooltipDatabase", menuName = "SORefs/CardTypeTooltipDatabase")]
public class CardTypeTooltipDatabaseSO : ScriptableObject
{
	private static CardTypeTooltipDatabaseSO _me;

	/// <summary>Lazy-loaded singleton. Loads "CardTypeTooltipDatabase" from Resources on first access.</summary>
	public static CardTypeTooltipDatabaseSO Me
	{
		get
		{
			if (_me == null)
			{
				_me = Resources.Load<CardTypeTooltipDatabaseSO>("CardTypeTooltipDatabase");
				if (_me == null)
				{
					Debug.LogError("[CardTypeTooltipDatabaseSO] No CardTypeTooltipDatabase asset found in Resources. Expected at Assets/Resources/CardTypeTooltipDatabase.asset");
				}
			}
			return _me;
		}
	}

	[Serializable]
	public class Entry
	{
		public EnumStorage.CardType cardType;
		public StringSO displayName;
		public StringSO description;
	}

	public List<Entry> entries = new List<Entry>();

	/// <summary>
	/// Display name for the card type: the configured StringSO value when present
	/// and non-empty, otherwise the enum name. Returns null when the type has no
	/// entry at all — the hover tooltip then shows no type block for it.
	/// </summary>
	public string GetDisplayName(EnumStorage.CardType cardType)
	{
		Entry entry = GetEntry(cardType);
		if (entry == null) return null;
		if (entry.displayName != null && !string.IsNullOrEmpty(entry.displayName.value))
		{
			return entry.displayName.value;
		}
		return cardType.ToString();
	}

	/// <summary>First matching entry's description StringSO, or null when absent.</summary>
	public StringSO GetDescription(EnumStorage.CardType cardType)
	{
		Entry entry = GetEntry(cardType);
		return entry != null ? entry.description : null;
	}

	private Entry GetEntry(EnumStorage.CardType cardType)
	{
		if (entries == null) return null;
		for (int i = 0; i < entries.Count; i++)
		{
			if (entries[i] != null && entries[i].cardType == cardType)
			{
				return entries[i];
			}
		}
		return null;
	}
}
