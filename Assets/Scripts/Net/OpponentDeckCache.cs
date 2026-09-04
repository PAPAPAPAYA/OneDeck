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

	// ------------------------------------------------------------------ run lifecycle

	/// <summary>
	/// Call at scene start / new run: per-run dedup resets and a batch prefetch kicks off.
	/// </summary>
	public static void OnRunStarted()
	{
		Load();
		cache.usedDeckIds.Clear();
		opponent = null;
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
			+ "&perSession=" + PrefetchPerSession;
		DeckNetworkClient.Me.GetJson("/api/decks/opponents", query,
			body =>
			{
				prefetchInFlight = false;
				OpponentDecksResponse response = JsonUtility.FromJson<OpponentDecksResponse>(body);
				if (response == null || response.decks == null) return;
				Load();
				foreach (OpponentDeckEntry deck in response.decks)
				{
					if (deck == null || deck.cardTypeIDs == null || deck.cardTypeIDs.Count == 0) continue;
					if (cache.decks.Exists(d => d != null && d.deckId == deck.deckId)) continue;
					cache.decks.Add(deck);
				}
				Save();
			},
			(error, statusCode) => { prefetchInFlight = false; });
	}

	// ------------------------------------------------------------------ consumption (DeckSaver side)

	/// <summary>
	/// Take an unused candidate for the session and mark it used. Null when the cache
	/// is dry for that session (caller falls back to the local chain).
	/// </summary>
	public static OpponentDeckEntry TakeCandidate(int sessionNum)
	{
		Load();
		OpponentDeckEntry candidate = cache.decks.Find(d =>
			d != null && d.sessionNum == sessionNum && !cache.usedDeckIds.Contains(d.deckId));
		if (candidate == null) return null;
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

	// ------------------------------------------------------------------ enemy source telemetry

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
	}

	/// <summary>Test seam: injects a deck straight into the cache (no network).</summary>
	public static void InjectForTests(OpponentDeckEntry deck)
	{
		Load();
		cache.decks.Add(deck);
		Save();
	}
}
