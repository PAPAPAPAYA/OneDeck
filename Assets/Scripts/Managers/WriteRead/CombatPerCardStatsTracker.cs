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
/// Which side of the combat CREATED a card. Rows are keyed by (cardTypeID, creatorSide),
/// so the same card type created by both sides produces two separate rows.
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

	/// <summary>
	/// When true, the Result panel appends the value's share of the column total
	/// (e.g. "12 (34%)") — the card's contribution to this half's sum of this stat.
	/// </summary>
	public bool showPercentageOfTotal;

	/// <summary>When false, the stat is still recorded but hidden from the Result panel columns.</summary>
	public bool showInResultPanel = true;

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
		new CombatStatDef { type = CombatStatType.DamageDealtToOpponent, columnHeader = "Dmg>Opp", columnSortPriority = 0, showPercentageOfTotal = true },
		new CombatStatDef { type = CombatStatType.DamageDealtToSelf, columnHeader = "Dmg>Self", columnSortPriority = 1, showPercentageOfTotal = true, showInResultPanel = false },
		new CombatStatDef { type = CombatStatType.TriggerCount, columnHeader = "Trig", columnSortPriority = 2 },
		new CombatStatDef { type = CombatStatType.PowerGiven, columnHeader = "PowGive", columnSortPriority = 3 },
		new CombatStatDef { type = CombatStatType.PowerReceived, columnHeader = "PowRecv", columnSortPriority = 4 }
	};

	public static List<CombatStatDef> GetColumnsSorted()
	{
		return Stats.Where(d => d.showInResultPanel).OrderBy(d => d.columnSortPriority).ToList();
	}
}

/// <summary>
/// One result-screen row: all stats of one card type created by one faction, aggregated over the combat.
/// faction = the CREATING side (initial deck cards: their deck's owner; mid-combat cards: the effect
/// source's faction), so a player-generated enemy-owned curse lands on the Player row.
/// instanceCount = copies of this card type in that faction's initial deck snapshot (display only).
/// </summary>
public class PerCardStatRecord
{
	public string cardTypeID;
	public string displayName;
	public CardFaction faction;
	public int instanceCount = 1;
	public readonly Dictionary<CombatStatType, float> values = new Dictionary<CombatStatType, float>();

	public float GetValue(CombatStatType stat)
	{
		return values.TryGetValue(stat, out var v) ? v : 0f;
	}
}

/// <summary>
/// Tracks per-card statistics (damage, triggers, power) for the current combat only.
/// Session-scoped: BeginSession() wipes the store at every combat start; no persistence.
/// Survives card destruction because it is a plain C# store keyed by (cardTypeID, creatorSide).
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

	/// <summary>Copy count per (cardTypeID, faction) in the initial merged deck, captured at GatherDecks.</summary>
	private readonly Dictionary<string, int> _deckCounts = new Dictionary<string, int>();

	/// <summary>Which faction created each card instance (initial deck = deck owner; mid-combat = effect source's faction).</summary>
	private readonly Dictionary<CardScript, CardFaction> _creatorSides = new Dictionary<CardScript, CardFaction>();

	/// <summary>Called at combat start (CombatManager.GatherDecks). Clears all records from the previous combat.</summary>
	public void BeginSession()
	{
		_records.Clear();
		_deckCounts.Clear();
		_creatorSides.Clear();
	}

	/// <summary>
	/// Snapshot of the initial merged deck, called once from CombatManager.GatherDecks after BeginSession.
	/// Registers each card's creator side (= its deck's owner) and counts copies per (cardTypeID, faction).
	/// Neutral/start cards are excluded. Cards created mid-combat are not part of this snapshot.
	/// </summary>
	public void RegisterDeckComposition(IEnumerable<GameObject> cards)
	{
		if (cards == null) return;
		foreach (var cardGo in cards)
		{
			if (cardGo == null) continue;
			var card = cardGo.GetComponent<CardScript>();
			if (card == null || card.IsNeutralCard) continue;

			var faction = ResolveFaction(card);
			_creatorSides[card] = faction;

			string key = ResolveTypeID(card) + "|" + faction;
			_deckCounts.TryGetValue(key, out var count);
			_deckCounts[key] = count + 1;
		}
	}

	/// <summary>
	/// Register a card created mid-combat (AddTempCard/CurseEffect) with the faction of the effect's
	/// source card as its creator side, so its damage is attributed to the side that generated it.
	/// </summary>
	public void RegisterGeneratedCard(CardScript newCard, CardScript creator)
	{
		if (newCard == null || creator == null) return;
		if (newCard.IsNeutralCard) return;
		_creatorSides[newCard] = ResolveFaction(creator);
	}

	/// <summary>
	/// Record raw pre-shield damage dealt by a card. victimSide is the faction that LOST the HP.
	/// Damage to the side opposing the card's CREATOR counts as DamageDealtToOpponent (e.g. a
	/// player-generated enemy-owned curse hurting the enemy counts as player-side damage to opponent);
	/// damage to the creator's own side counts as DamageDealtToSelf.
	/// </summary>
	public void RecordDamage(CardScript source, float amount, CardFaction victimSide)
	{
		if (amount <= 0f) return;
		var creatorSide = ResolveCreatorSide(source);
		var stat = victimSide != creatorSide ? CombatStatType.DamageDealtToOpponent : CombatStatType.DamageDealtToSelf;
		Add(source, stat, amount);
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
	/// Central add: resolves the (cardTypeID, creatorSide) key and aggregates the amount.
	/// Neutral/start cards are excluded here — the single exclusion point.
	/// </summary>
	public void Add(CardScript card, CombatStatType stat, float amount)
	{
		if (card == null) return;
		if (card.IsNeutralCard) return;

		var faction = ResolveCreatorSide(card);
		string typeID = ResolveTypeID(card);
		string key = typeID + "|" + faction;

		if (!_records.TryGetValue(key, out var record))
		{
			record = new PerCardStatRecord
			{
				cardTypeID = typeID,
				displayName = card.GetDisplayName(),
				faction = faction,
				instanceCount = _deckCounts.TryGetValue(key, out var count) ? count : 1
			};
			_records[key] = record;
		}

		record.values.TryGetValue(stat, out var current);
		record.values[stat] = current + amount;
	}

	private static string ResolveTypeID(CardScript card)
	{
		return string.IsNullOrEmpty(card.cardTypeID) ? card.gameObject.name : card.cardTypeID;
	}

	/// <summary>Creator side of a card: the registered value if known, otherwise its current owner faction.</summary>
	private CardFaction ResolveCreatorSide(CardScript card)
	{
		if (card != null && _creatorSides.TryGetValue(card, out var side)) return side;
		return ResolveFaction(card);
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
