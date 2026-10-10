using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Client-side cache of ghost decks fetched from the server (plan §2.4).
/// Disk-cached so a network-less start still has opponents to fall back on; decks
/// already matched during the current run are never reused (per-run dedup).
/// Candidates are validated by the consumer (DeckSaver): any unknown cardTypeID
/// discards the whole deck and the local fallback chain takes over.
/// Every failure is silent - the game must stay playable offline.
/// </summary>
public static class OpponentDeckCache
{
	/// <summary>The ghost deck injected into the current combat; null when fighting local decks.</summary>
	[Serializable]
	public class CurrentOpponent
	{
		public int deckId;
		public string username;
	}

	/// <summary>Lifetime counters of which source supplied the enemy deck (plan §0.1 telemetry; batch C uploads).</summary>
	[Serializable]
	public class EnemySourceCounters
	{
		public int server;
		public int local;
		public int pool;
	}

	public const int PrefetchMaxSession = 6;
	public const int PrefetchPerSession = 2;
	public const string SourceServer = "server";
	public const string SourceLocal = "local";
	public const string SourcePool = "pool";

	/// <summary>Test seam: when set, overrides the persistentDataPath directory for cache files.</summary>
	public static string OverrideDirectoryForTests;

	/// <summary>
	/// Test toggle (pushed by TestManager.fightOwnGhostsOnly): takes are restricted to the
	/// player's own decks; a session without own decks returns null (cache-dry, DeckSaver
	/// then clears the enemy deck instead of letting a foreign candidate through).
	/// </summary>
	public static bool OnlyOwnDecks;

	private const string CacheFileName = "opponent_cache.json";
	private const string CountersFileName = "enemy_source_counters.json";

	[Serializable]
	private class CacheFile
	{
		public List<OpponentDeckEntry> decks = new List<OpponentDeckEntry>();
		public List<int> usedDeckIds = new List<int>();
	}

	private static CacheFile cache;
	private static EnemySourceCounters counters;
	private static CurrentOpponent opponent;
	private static bool prefetchInFlight;

	public static CurrentOpponent Current { get { return opponent; } }

	private static string CacheFilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, CacheFileName); }
	}

	private static string CountersFilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, CountersFileName); }
	}

	/// <summary>True when ghost fetching is switched on (injection also consults this, plan §3.1).</summary>
	public static bool FetchEnabled
	{
		get
		{
			ServerConfig config = ServerConfig.Active;
			return config != null && config.enabled && config.fetchOpponentDecks;
		}
	}

	/// <summary>Test toggle: the opponents fetch also returns the requesting player's own decks.</summary>
	private static bool IncludeSelf
	{
		get
		{
			ServerConfig config = ServerConfig.Active;
			return config != null && config.opponentsIncludeSelf;
		}
	}

	// ------------------------------------------------------------------ run lifecycle

	/// <summary>
	/// Call at scene start / new run: per-run dedup resets and a batch prefetch kicks off.
	/// </summary>
	public static void OnRunStarted()
	{
		Load();
		cache.usedDeckIds.Clear();
		opponent = null;
		reservedEntry = null;
		reservedSessionNum = -1;
		Save();
		Prefetch();
	}

	/// <summary>
	/// Call when entering the shop: make sure upcoming sessions have candidates.
	/// sessionNum is the session the NEXT combat will fight (it is incremented before
	/// the shop phase opens), so this tops up that session and the one after.
	/// </summary>
	public static void EnsureStockForSession(int sessionNum)
	{
		if (!FetchEnabled || !PlayerIdentity.HasIdentity || prefetchInFlight) return;
		Load();
		if (UnusedCountForSession(sessionNum) < PrefetchPerSession
			|| UnusedCountForSession(sessionNum + 1) < PrefetchPerSession)
		{
			Prefetch();
		}
	}

	/// <summary>Fire-and-forget batch fetch; merges new deckIds into the disk cache.</summary>
	public static void Prefetch()
	{
		if (!FetchEnabled || !PlayerIdentity.HasIdentity || prefetchInFlight) return;
		prefetchInFlight = true;
		string query = "playerId=" + PlayerIdentity.PlayerId
			+ "&gameVersion=" + DeckNetworkClient.GameVersion
			+ "&maxSession=" + PrefetchMaxSession
			+ "&perSession=" + PrefetchPerSession
			+ (IncludeSelf ? "&includeSelf=1" : "");
		DeckNetworkClient.Me.GetJson("/api/decks/opponents", query,
			body =>
			{
				prefetchInFlight = false;
				OpponentDecksResponse response = JsonUtility.FromJson<OpponentDecksResponse>(body);
				if (response == null || response.decks == null) return;
				MergeResponse(response);
			},
			(error, statusCode) => { prefetchInFlight = false; });
	}

	/// <summary>
	/// Merge a fetched response into the disk cache (Prefetch callback body; public so tests
	/// can exercise the merge without a network call). While includeSelf is off, own decks
	/// must never sit in the opponent pool: legacy entries that predate the server-side
	/// exclusion are dropped, and any the response still carries are refused, so the filter
	/// self-heals on every successful prefetch.
	/// </summary>
	public static void MergeResponse(OpponentDecksResponse response)
	{
		bool excludeSelf = !IncludeSelf;
		string selfName = excludeSelf ? PlayerIdentity.Username : null;
		Load();
		if (excludeSelf && !string.IsNullOrEmpty(selfName))
		{
			int purged = cache.decks.RemoveAll(d => d != null && d.username == selfName);
			if (purged > 0) Debug.Log("[OpponentDeckCache] ownership filter: purged " + purged + " own-deck entries from the cache");
		}
		foreach (OpponentDeckEntry deck in response.decks)
		{
			if (deck == null || deck.cardTypeIDs == null || deck.cardTypeIDs.Count == 0) continue;
			if (excludeSelf && !string.IsNullOrEmpty(selfName) && deck.username == selfName) continue;
			if (cache.decks.Exists(d => d != null && d.deckId == deck.deckId)) continue;
			cache.decks.Add(deck);
		}
		// Infinity gate (plan §20.3): decks flagged since this cache was filled must not be
		// fightable. Removal — not just a take-time skip — so the file, the session stock and
		// usedDeckIds stay consistent; the next prefetch tops the stock back up if needed.
		if (response.flaggedDeckIds != null && response.flaggedDeckIds.Count > 0)
		{
			int purgedFlagged = cache.decks.RemoveAll(d => d != null && response.flaggedDeckIds.Contains(d.deckId));
			if (purgedFlagged > 0)
			{
				Debug.Log("[OpponentDeckCache] infinity gate: dropped " + purgedFlagged
					+ " flagged deck(s) from the cache");
			}
		}
		// §21: same idea for the combo library — a deck CONTAINING a proven combo is withheld by
		// the server, so a copy cached before the combo was registered has to go as well.
		if (response.blockedCombos != null && response.blockedCombos.Count > 0)
		{
			int purgedCombos = cache.decks.RemoveAll(d => d != null && ContainsBlockedCombo(d.cardTypeIDs, response.blockedCombos));
			if (purgedCombos > 0)
			{
				Debug.Log("[OpponentDeckCache] combo gate: dropped " + purgedCombos
					+ " cached deck(s) containing a blocked combo");
			}
		}
		Save();
	}

	// ------------------------------------------------------------------ consumption (DeckSaver side)

	/// <summary>
	/// Take an unused candidate for the session and mark it used. Null when the cache
	/// is dry for that session (caller falls back to the local chain).
	/// Selection is random among the unused same-session candidates so cache insertion
	/// order never decides priority. With OnlyOwnDecks (fightOwnGhostsOnly) the pool is
	/// restricted to the player's own decks first; a session without own decks returns null.
	/// </summary>
	public static OpponentDeckEntry TakeCandidate(int sessionNum)
	{
		Load();
		List<OpponentDeckEntry> candidates = cache.decks.FindAll(d =>
			d != null && d.sessionNum == sessionNum && !cache.usedDeckIds.Contains(d.deckId));
		if (OnlyOwnDecks)
		{
			string selfName = PlayerIdentity.Username;
			candidates = candidates.FindAll(d => d.username == selfName);
		}
		if (candidates.Count == 0) return null;
		OpponentDeckEntry candidate = candidates[Rng.Next(RngChannel.Setup, candidates.Count)];
		cache.usedDeckIds.Add(candidate.deckId);
		Save();
		return candidate;
	}

	/// <summary>Whole-deck discard for a candidate the consumer could not resolve.</summary>
	public static void DiscardCandidate(int deckId)
	{
		Load();
		cache.decks.RemoveAll(d => d != null && d.deckId == deckId);
		Save();
	}

	/// <summary>Stash the ghost deck now fighting; null clears it (local-deck combats).</summary>
	public static void SetCurrentOpponent(OpponentDeckEntry deck)
	{
		opponent = deck == null
			? null
			: new CurrentOpponent { deckId = deck.deckId, username = deck.username };
	}

	// ------------------------------------------------------------------ select-page flow (plan-opponent-select-page-2026-10-06 §4.1)

	/// <summary>
	/// Select-page pick reservation: ConsumeCandidate stashes the full entry so DeckSaver
	/// can populate from it without re-randomizing (plan §4.2). Memory-only like
	/// stagedSource — a crash between pick and inject leaves the deckId marked used, the
	/// same acceptable state as an app exit mid-select; cleared by OnRunStarted.
	/// </summary>
	private static OpponentDeckEntry reservedEntry;
	private static int reservedSessionNum = -1;

	/// <summary>True while a select-page pick waits for its populate (tests / diagnostics).</summary>
	public static bool HasReservedEntry { get { return reservedEntry != null; } }

	/// <summary>
	/// Unused same-session candidates for the select page, up to n, random order. Same
	/// filter as TakeCandidate (OnlyOwnDecks included) and NOT marked used — picking is
	/// the separate ConsumeCandidate step. Pure read: nothing is saved.
	/// </summary>
	public static List<OpponentDeckEntry> PeekCandidates(int sessionNum, int n)
	{
		Load();
		List<OpponentDeckEntry> candidates = cache.decks.FindAll(d =>
			d != null && d.sessionNum == sessionNum && !cache.usedDeckIds.Contains(d.deckId));
		if (OnlyOwnDecks)
		{
			string selfName = PlayerIdentity.Username;
			candidates = candidates.FindAll(d => d.username == selfName);
		}
		Rng.Shuffle(RngChannel.Setup, candidates);
		if (candidates.Count > n) candidates.RemoveRange(n, candidates.Count - n);
		return candidates;
	}

	/// <summary>
	/// Select-page pick: mark used + set Current + reserve the full entry for the DeckSaver
	/// reservation branch. Re-consuming the same deckId (double tap on the same panel) is
	/// an idempotent no-op; a deckId that left the cache (flag purge etc.) is ignored.
	/// </summary>
	public static void ConsumeCandidate(int sessionNum, int deckId)
	{
		Load();
		if (reservedEntry != null && reservedEntry.deckId == deckId) return;
		OpponentDeckEntry entry = cache.decks.Find(d =>
			d != null && d.deckId == deckId && d.sessionNum == sessionNum);
		if (entry == null) return;
		if (!cache.usedDeckIds.Contains(deckId))
		{
			cache.usedDeckIds.Add(deckId);
			Save();
		}
		reservedEntry = entry;
		reservedSessionNum = sessionNum;
		SetCurrentOpponent(entry);
	}

	/// <summary>
	/// Hand the reserved entry to the DeckSaver reservation branch and consume it. A stale
	/// session mismatch clears silently and returns null — populate then walks the normal
	/// chain. A mismatch cannot occur in the live flow (pick and populate share the
	/// session); it only guards test / editor-domain leftovers.
	/// </summary>
	public static OpponentDeckEntry TakeReservedEntry(int sessionNum)
	{
		OpponentDeckEntry entry = reservedEntry;
		bool matches = entry != null && reservedSessionNum == sessionNum;
		reservedEntry = null;
		reservedSessionNum = -1;
		return matches ? entry : null;
	}

	/// <summary>Full cached entry by deckId (null when absent) — Current-refill lookups (plan §4.2).</summary>
	public static OpponentDeckEntry FindEntry(int deckId)
	{
		Load();
		return cache.decks.Find(d => d != null && d.deckId == deckId);
	}

	// ------------------------------------------------------------------ enemy source telemetry

	// Staged source for the combat being entered (upload gate): DeckSaver stages at
	// populate time and the PhaseManager settlement point commits, so a combat
	// abandoned mid-fight never reaches the lifetime counters. Memory-only.
	private static string stagedSource;

	/// <summary>Stage the enemy deck source at populate time; committed on combat settlement.</summary>
	public static void StageEnemySource(string source)
	{
		stagedSource = source;
	}

	/// <summary>Settlement trigger: counts the staged source (if any) and consumes it.</summary>
	public static void CommitStagedEnemySource()
	{
		if (string.IsNullOrEmpty(stagedSource)) return;
		string source = stagedSource;
		stagedSource = null;
		RecordEnemySource(source);
	}

	public static EnemySourceCounters SourceCounters
	{
		get { LoadCounters(); return counters; }
	}

	public static void RecordEnemySource(string source)
	{
		LoadCounters();
		if (source == SourceServer) counters.server++;
		else if (source == SourceLocal) counters.local++;
		else if (source == SourcePool) counters.pool++;
		SaveCounters();
	}

	/// <summary>
	/// True when the deck holds every card of any active combo, with multiplicity (plan §21).
	/// Multiset, not set: a combo needing two copies is not satisfied by one. Counts are built
	/// per deck (card lists are tens of entries, combos are typically 1-3 cards).
	/// </summary>
	private static bool ContainsBlockedCombo(List<string> cardTypeIDs, List<OpponentBlockedCombo> combos)
	{
		if (cardTypeIDs == null || cardTypeIDs.Count == 0) return false;

		Dictionary<string, int> deckCounts = null;
		foreach (OpponentBlockedCombo combo in combos)
		{
			if (combo == null || combo.cards == null || combo.cards.Count == 0) continue;
			if (deckCounts == null)
			{
				deckCounts = new Dictionary<string, int>();
				foreach (string id in cardTypeIDs)
				{
					deckCounts.TryGetValue(id, out int n);
					deckCounts[id] = n + 1;
				}
			}

			var needed = new Dictionary<string, int>();
			foreach (string id in combo.cards)
			{
				needed.TryGetValue(id, out int n);
				needed[id] = n + 1;
			}
			bool contained = true;
			foreach (KeyValuePair<string, int> pair in needed)
			{
				deckCounts.TryGetValue(pair.Key, out int have);
				if (have < pair.Value) { contained = false; break; }
			}
			if (contained) return true;
		}
		return false;
	}

	// ------------------------------------------------------------------ persistence

	private static int UnusedCountForSession(int sessionNum)
	{
		return cache.decks.FindAll(d =>
			d != null && d.sessionNum == sessionNum && !cache.usedDeckIds.Contains(d.deckId)).Count;
	}

	private static CacheFile Load()
	{
		if (cache != null) return cache;
		cache = new CacheFile();
		try
		{
			if (File.Exists(CacheFilePath))
			{
				CacheFile file = JsonUtility.FromJson<CacheFile>(File.ReadAllText(CacheFilePath, Encoding.UTF8));
				if (file != null)
				{
					if (file.decks != null) cache.decks = file.decks;
					if (file.usedDeckIds != null) cache.usedDeckIds = file.usedDeckIds;
				}
			}
		}
		catch (Exception e)
		{
			// A corrupt cache must never break the game; worst case we fall back locally.
			Debug.LogWarning("[OpponentDeckCache] cache file unreadable, starting empty: " + e.Message);
			cache = new CacheFile();
		}
		return cache;
	}

	private static void Save()
	{
		try
		{
			string tmp = CacheFilePath + ".tmp";
			File.WriteAllText(tmp, JsonUtility.ToJson(cache), new UTF8Encoding(false));
			if (File.Exists(CacheFilePath)) File.Replace(tmp, CacheFilePath, null);
			else File.Move(tmp, CacheFilePath);
		}
		catch (IOException e)
		{
			Debug.LogWarning("[OpponentDeckCache] cache save failed: " + e.Message);
		}
	}

	private static void LoadCounters()
	{
		if (counters != null) return;
		counters = new EnemySourceCounters();
		try
		{
			if (File.Exists(CountersFilePath))
			{
				EnemySourceCounters file = JsonUtility.FromJson<EnemySourceCounters>(
					File.ReadAllText(CountersFilePath, Encoding.UTF8));
				if (file != null) counters = file;
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[OpponentDeckCache] counters file unreadable, starting empty: " + e.Message);
			counters = new EnemySourceCounters();
		}
	}

	private static void SaveCounters()
	{
		try
		{
			string tmp = CountersFilePath + ".tmp";
			File.WriteAllText(tmp, JsonUtility.ToJson(counters), new UTF8Encoding(false));
			if (File.Exists(CountersFilePath)) File.Replace(tmp, CountersFilePath, null);
			else File.Move(tmp, CountersFilePath);
		}
		catch (IOException e)
		{
			Debug.LogWarning("[OpponentDeckCache] counters save failed: " + e.Message);
		}
	}

	// ------------------------------------------------------------------ test seams

	/// <summary>Test seam: drops in-memory state so the next read reloads from disk.</summary>
	public static void ResetCacheForTests()
	{
		cache = null;
		opponent = null;
		counters = null;
		stagedSource = null;
		reservedEntry = null;
		reservedSessionNum = -1;
		OnlyOwnDecks = false;
	}

	/// <summary>Test seam: injects a deck straight into the cache (no network).</summary>
	public static void InjectForTests(OpponentDeckEntry deck)
	{
		Load();
		cache.decks.Add(deck);
		Save();
	}
}
