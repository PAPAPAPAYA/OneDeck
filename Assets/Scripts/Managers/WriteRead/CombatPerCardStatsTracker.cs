using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Stat types tracked per card during one combat, shown on the Result screen.
/// Adding a new stat = one enum entry + one CombatStatRegistry entry + one Record*() call at the hook site.
/// </summary>
public enum CombatStatType
{
	DamageDealtToOpponent,
	DamageDealtToSelf,
	TriggerCount,
	PowerGiven,
	PowerReceived
}

/// <summary>
/// Which side of the combat a card belongs to. Rows are keyed by (cardTypeID, faction),
/// so the same card type on both sides produces two separate rows.
/// </summary>
public enum CardFaction
{
	Player,
	Enemy
}

/// <summary>
/// Column definition for one stat. columnSortPriority controls COLUMN order only;
/// row sorting is defined separately in CombatPerCardStatsTracker.GetSessionRows().
/// </summary>
public class CombatStatDef
{
	public CombatStatType type;
	public string columnHeader;
	public int columnSortPriority;

	/// <summary>Rich-text hex color for this column, sourced from the central palette. Falls back to white.</summary>
	public string ColorHex
	{
		get
		{
			var palette = GameColorPalette.Me;
			if (palette == null) return "#FFFFFF";
			ColorSO so = type switch
			{
				CombatStatType.DamageDealtToOpponent => palette.damage,
				CombatStatType.DamageDealtToSelf => palette.damage,
				CombatStatType.PowerGiven => palette.powerTint,
				CombatStatType.PowerReceived => palette.powerTint,
				_ => null
			};
			return so != null ? so.Hex : "#FFFFFF";
		}
	}
}

/// <summary>
/// Static registry of all per-card combat stats. Data, report, and UI columns all derive from this list.
/// </summary>
public static class CombatStatRegistry
{
	public static readonly List<CombatStatDef> Stats = new List<CombatStatDef>
	{
		new CombatStatDef { type = CombatStatType.DamageDealtToOpponent, columnHeader = "Dmg>Opp", columnSortPriority = 0 },
		new CombatStatDef { type = CombatStatType.DamageDealtToSelf, columnHeader = "Dmg>Self", columnSortPriority = 1 },
		new CombatStatDef { type = CombatStatType.TriggerCount, columnHeader = "Trig", columnSortPriority = 2 },
		new CombatStatDef { type = CombatStatType.PowerGiven, columnHeader = "PowGive", columnSortPriority = 3 },
		new CombatStatDef { type = CombatStatType.PowerReceived, columnHeader = "PowRecv", columnSortPriority = 4 }
	};

	public static List<CombatStatDef> GetColumnsSorted()
	{
		return Stats.OrderBy(d => d.columnSortPriority).ToList();
	}
}

/// <summary>
/// One result-screen row: all stats of one card type on one faction, aggregated over the combat.
/// </summary>
public class PerCardStatRecord
{
	public string cardTypeID;
	public string displayName;
	public CardFaction faction;
	public readonly Dictionary<CombatStatType, float> values = new Dictionary<CombatStatType, float>();

	public float GetValue(CombatStatType stat)
	{
		return values.TryGetValue(stat, out var v) ? v : 0f;
	}
}

/// <summary>
/// Tracks per-card statistics (damage, triggers, power) for the current combat only.
/// Session-scoped: BeginSession() wipes the store at every combat start; no persistence.
/// Survives card destruction because it is a plain C# store keyed by (cardTypeID, faction).
/// Singleton is auto-created by CombatManager.Awake() if missing from the scene.
/// </summary>
public class CombatPerCardStatsTracker : MonoBehaviour
{
	#region SINGLETON
	public static CombatPerCardStatsTracker Me;

	private void Awake()
	{
		Me = this;
	}
	#endregion

	private readonly Dictionary<string, PerCardStatRecord> _records = new Dictionary<string, PerCardStatRecord>();

	/// <summary>Called at combat start (CombatManager.GatherDecks). Clears all records from the previous combat.</summary>
	public void BeginSession()
	{
		_records.Clear();
	}

	public void RecordDamageToOpponent(CardScript source, float amount)
	{
		if (amount <= 0f) return;
		Add(source, CombatStatType.DamageDealtToOpponent, amount);
	}

	public void RecordDamageToSelf(CardScript source, float amount)
	{
		if (amount <= 0f) return;
		Add(source, CombatStatType.DamageDealtToSelf, amount);
	}

	public void RecordTrigger(CardScript source)
	{
		Add(source, CombatStatType.TriggerCount, 1f);
	}

	public void RecordPowerGiven(CardScript giver, int amount)
	{
		if (amount <= 0) return;
		Add(giver, CombatStatType.PowerGiven, amount);
	}

	public void RecordPowerReceived(CardScript receiver, int amount)
	{
		if (amount <= 0) return;
		Add(receiver, CombatStatType.PowerReceived, amount);
	}

	/// <summary>
	/// Central add: resolves the (cardTypeID, faction) key and aggregates the amount.
	/// Neutral/start cards are excluded here — the single exclusion point.
	/// </summary>
	public void Add(CardScript card, CombatStatType stat, float amount)
	{
		if (card == null) return;
		if (card.IsNeutralCard) return;

		var faction = ResolveFaction(card);
		string typeID = string.IsNullOrEmpty(card.cardTypeID) ? card.gameObject.name : card.cardTypeID;
		string key = typeID + "|" + faction;

		if (!_records.TryGetValue(key, out var record))
		{
			record = new PerCardStatRecord
			{
				cardTypeID = typeID,
				displayName = card.GetDisplayName(),
				faction = faction
			};
			_records[key] = record;
		}

		record.values.TryGetValue(stat, out var current);
		record.values[stat] = current + amount;
	}

	private static CardFaction ResolveFaction(CardScript card)
	{
		var cm = CombatManager.Me;
		if (cm != null && card.myStatusRef == cm.ownerPlayerStatusRef)
		{
			return CardFaction.Player;
		}
		return CardFaction.Enemy;
	}

	/// <summary>
	/// Rows for the Result screen, sorted by DamageDealtToOpponent desc, then faction (Player first).
	/// Row sort is independent of the registry's columnSortPriority (which orders columns).
	/// </summary>
	public List<PerCardStatRecord> GetSessionRows()
	{
		return _records.Values
			.OrderByDescending(r => r.GetValue(CombatStatType.DamageDealtToOpponent))
			.ThenBy(r => r.faction)
			.ToList();
	}
}
