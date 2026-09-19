using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deferred attribution queue for the infinity plan's P2 hook
/// (plans/plan-infinity-detection-2026-09-17.md §19.5; user ruling 2026-09-19: attribution must
/// NOT run inside the combat frame).
/// A trip is recorded the moment the arrangement-cycle detector fires — cheap, no simulation — and
/// the expensive headless attribution is run later, out of combat, by the editor-side processor.
/// Lives in the RUNTIME assembly on purpose: capturing evidence has to work in a shipped build.
/// Running the attribution does NOT: RunBudgetSim needs SerializedObject + AssetDatabase, which are
/// editor-only, so in a build the queue is what gets uploaded and the attribution happens offline
/// (P5 batch scan / server side). That split is deliberate, not a gap to paper over.
/// Bounded: a runaway combat cannot grow the queue without bound; the oldest entry is dropped.
/// </summary>
public static class InfinityTripJournal
{
	public const int MaxEntries = 32;

	public struct Entry
	{
		/// <summary>Arrangement hash at the moment of the live trip.</summary>
		public uint TripHash;
		public int TripCount;
		public int SightingsThisRound;
		/// <summary>The combat's seed — the key that makes a headless re-run reproduce the combat.</summary>
		public int CombatSeed;
		public string PlayerDeckName;
		public string EnemyDeckName;
		public DeckSO PlayerDeck;
		public DeckSO EnemyDeck;
		public string CapturedUtc;
	}

	private static readonly List<Entry> _entries = new List<Entry>();

	/// <summary>
	/// Depth of "this combat is a headless SIMULATION, do not journal" scopes. The attribution
	/// processor runs RunBudgetSim, whose combats trip the very same detector — without this the
	/// reproductions would queue themselves as fresh live trips and the queue would never drain
	/// (measured 2026-09-19: ProcessPending left 2 entries behind after consuming one).
	/// </summary>
	private static int _suppressionDepth;

	public static bool IsRecordingEnabled { get { return _suppressionDepth == 0; } }

	public static void PushSuppression() { _suppressionDepth++; }

	public static void PopSuppression()
	{
		if (_suppressionDepth > 0) _suppressionDepth--;
	}

	public static int Count { get { return _entries.Count; } }

	/// <summary>Queued trips, oldest first. Read-only view — drain it to consume.</summary>
	public static IReadOnlyList<Entry> Pending { get { return _entries; } }

	public static void Record(uint tripHash, int tripCount, int sightingsThisRound,
		int combatSeed, DeckSO playerDeck, DeckSO enemyDeck)
	{
		if (!IsRecordingEnabled) return;

		if (_entries.Count >= MaxEntries) _entries.RemoveAt(0);

		_entries.Add(new Entry
		{
			TripHash = tripHash,
			TripCount = tripCount,
			SightingsThisRound = sightingsThisRound,
			CombatSeed = combatSeed,
			PlayerDeckName = playerDeck != null ? playerDeck.name : "null",
			EnemyDeckName = enemyDeck != null ? enemyDeck.name : "null",
			PlayerDeck = playerDeck,
			EnemyDeck = enemyDeck,
			CapturedUtc = System.DateTime.UtcNow.ToString("o"),
		});
	}

	/// <summary>Takes everything queued so far (the processor's hand-off) and clears the queue.</summary>
	public static List<Entry> Drain()
	{
		var drained = new List<Entry>(_entries);
		_entries.Clear();
		return drained;
	}

	public static void Clear()
	{
		_entries.Clear();
	}
}
