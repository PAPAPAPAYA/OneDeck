using System;
using System.Collections.Generic;

/// <summary>
/// Wire DTOs for the OneDeck API. JsonUtility-compatible only: [Serializable] classes
/// with public fields, List&lt;T&gt; allowed, Dictionary forbidden (plan §2.2). Field names
/// must match server.js casing exactly.
/// </summary>
[Serializable]
public class RegisterRequest
{
	public string username;
}

[Serializable]
public class RegisterResponse
{
	public string playerId;
	public string username;
}

[Serializable]
public class DeckUploadRequest
{
	public string playerId;
	public string gameVersion;
	public int sessionNum;
	public int hpMax;
	public int winAmount;
	public int heartLeft;
	public List<string> cardTypeIDs;
}

[Serializable]
public class DeckUploadResponse
{
	public int deckId;
}

[Serializable]
public class OpponentDeckEntry
{
	public int deckId;
	public int sessionNum;
	public string username;
	public List<string> cardTypeIDs;
	public int hpMax;
	public int winAmount;
	public int heartLeft;
	public int defenseWins;
	public int defenseLosses;
}

[Serializable]
public class OpponentDecksResponse
{
	public List<OpponentDeckEntry> decks;
	/// <summary>
	/// Deck ids flagged as infinite inside the requested range (plan §20.3). The server cannot
	/// see the client's disk cache, so it names every flagged id in the prefetch range and
	/// OpponentDeckCache drops cached copies — otherwise a deck prefetched BEFORE it was flagged
	/// would stay fightable.
	/// </summary>
	public List<int> flaggedDeckIds;
}

[Serializable]
public class LoopReportUploadRequest
{
	public string playerId;
	public string gameVersion;
	/// <summary>Server deck row of the accused ghost deck (decks.deck_id).</summary>
	public int opponentDeckId;
	/// <summary>
	/// "EnemyDeck" = the headless re-run proved the ghost loops by itself — the ONLY verdict the
	/// server flags on. "" / "None" / "OwnerDeck" / "PairOnly" land as evidence and change no
	/// state (plan §20.2, rulings §7.1/§7.2).
	/// </summary>
	public string verdict;
	public int seed;
	/// <summary>Human-readable trip evidence, e.g. "arrangement-cycle <hex> repeats=9".</summary>
	public string signals;
	/// <summary>LoopReport JSON. A string because JsonUtility cannot nest arbitrary shapes; the server stores it verbatim.</summary>
	public string payload;
}

[Serializable]
public class MatchReportRequest
{
	public string playerId;
	public string reportId;
	public int opponentDeckId;
	public bool won;
	public int sessionNum;
	public string gameVersion;
}

[Serializable]
public class MatchReportResponse
{
	public bool ok;
	public bool deduped;
}

[Serializable]
public class StatsShopRow
{
	public string cardTypeID;
	public int sessionNum;
	public int appear;
	public int bought;
	public int utilAppear;
	public int utilBought;
}

[Serializable]
public class StatsWinrateRow
{
	public string cardTypeID;
	public int sessionNum;
	public int combats;
	public int wins;
	public int losses;
}

[Serializable]
public class StatsEnemySource
{
	public int server;
	public int local;
	public int pool;
}

[Serializable]
public class StatsMeta
{
	public int totalShopVisits;
	public int totalRerolls;
	/// <summary>Lifetime enemy-deck source counters (plan §0.1 telemetry). Null = legacy client, server keeps stored values.</summary>
	public StatsEnemySource enemySource;
}

[Serializable]
public class StatsSnapshotRequest
{
	public string playerId;
	public string gameVersion;
	public List<StatsShopRow> shop;
	public List<StatsWinrateRow> winrate;
	public StatsMeta meta;
}

[Serializable]
public class StatsSnapshotResponse
{
	public bool ok;
	public int rows;
}

[Serializable]
public class RunCombatPerCard
{
	public string cardTypeID;
	public int triggers;
	public int damageToOpponent;
	public int damageToSelf;
}

/// <summary>
/// One per-reveal combat sample: the HP/shield/deck-size curves plus the reveal
/// sequence, all aligned on revealIndex (1-based, combat-scoped).
/// </summary>
[Serializable]
public class RunCombatSample
{
	public const int SideOwner = 0;
	public const int SideEnemy = 1;
	public const int SideNeutral = 2;

	public int revealIndex;
	public int roundNum;
	public int ownerHP;
	public int enemyHP;
	public int ownerShield;
	public int enemyShield;
	public int ownerDeckSize;
	public int enemyDeckSize;
	/// <summary>SideOwner / SideEnemy / SideNeutral.</summary>
	public int side;
	public string cardTypeID;
}

[Serializable]
public class RunCombatEntry
{
	public int sessionNum;
	public bool won;
	public int heartsLeft;
	public int rounds;
	/// <summary>Ghost deck fought against; 0 = none (local fallback), server stores NULL.</summary>
	public int opponentDeckId;
	public string ts;
	public List<RunCombatPerCard> perCard;
	/// <summary>Per-reveal samples (ServerConfig.includeCombatSeries). Null = off/empty, server stores [].</summary>
	public List<RunCombatSample> series;
}

[Serializable]
public class RunShopVisitEntry
{
	public int sessionNum;
	public List<string> offered;
	public List<string> utilityOffered;
	public List<string> bought;
	public int rerollCount;
	public float seenPoolPct;
	/// <summary>Owner hpMax at shop exit (post baseline/utility recompute). 0 = legacy record.</summary>
	public int hpMax;
	public int goldEnter;
	public int goldAfterPayday;
	public int goldExit;
	public string ts;
}

[Serializable]
public class RunUploadRequest
{
	public string playerId;
	public string runId;
	public string gameVersion;
	/// <summary>One of victory / defeat / abandoned.</summary>
	public string result;
	public int finalSession;
	public int heartsLeft;
	public List<string> finalDeck;
	public float seenPoolPct;
	public string startedAt;
	public string endedAt;
	public List<RunShopVisitEntry> shopVisits;
	public List<RunCombatEntry> combats;
}

[Serializable]
public class RunUploadResponse
{
	public bool ok;
	public bool deduped;
}

[Serializable]
public class CatalogCardEntry
{
	public string cardTypeID;
	public string name;
	public List<string> tags;
	public string rarity;
	public int cost;
}

[Serializable]
public class CatalogUploadRequest
{
	public string playerId;
	public string gameVersion;
	public List<CatalogCardEntry> cards;
}
