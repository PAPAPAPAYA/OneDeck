using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// L1 primary infinity judge (plans/plan-infinity-detection-2026-09-17.md §3, 2026-09-18
/// ruling): direct cycle detection instead of budget heuristics. Hashes the combined
/// deck arrangement (cardTypeID + side sequence, pure FNV-1a via RngDigest) once per
/// completed reveal, at the reveal-cycle boundary (CombatManager confirm paths).
/// The identical arrangement appearing for the Nth time WITHIN one round = the loop
/// body of an unbounded recursion — the flag criterion. Deliberately pure-arrangement:
/// pumping loops (lethal infinite type, power/HP change every lap) still repeat their
/// arrangement every lap, while a full-state hash would miss them.
/// CRITERION v2 (2026-09-20, plan §26): raw repetition alone is NOT the criterion any more.
/// After the once-per-round revive gate shipped, a gate-bounded oscillation (two same-type
/// cards reviving each other, one charge per instance per round) still repeats its
/// arrangement 3-4 times per round while the round boundary survives — measured: bounded
/// forms top out at 4 cycles, true unbounded loops start at 18. So a trip now requires a
/// PERIODIC run (period p in 1..maxPeriod) of >= tripCycles repetitions that the round
/// boundary did not reset: a cycle that needs the round start to continue is bounded by the
/// gate's per-round charge, which also makes "reveals > starvationFactor x round-start pool"
/// the definitional guard rather than a harm heuristic.
/// Round safety: a quiescent full round samples each arrangement exactly once
/// (sample period = deck size, samples per round = deck size - 1), and the round
/// boundary (Start Card shuffle) clears the sample history — so normal rotation can
/// never reach the trip threshold. Shuffle-dependent loops whose period crosses the
/// round boundary are a documented blind spot (the reshuffle breaks the arrangement).
/// On trip this component is observation-only by design (§3 point ②): it logs and
/// fires OnCycleTripped for the P2 attribution sim to consume; it never force-concludes
/// combat itself — the L0 CombatBudgetGuard remains the hard backstop.
/// Auto-created on the CombatManager GameObject in CombatManager.Awake, mirroring
/// CombatBudgetGuard.
/// </summary>
public class CombatArrangementCycleDetector : MonoBehaviour
{
	public static CombatArrangementCycleDetector Me { get; private set; }

	[Header("Trip criterion v2 (2026-09-20, plan §26)")]
	[Tooltip("Repetitions of a periodic arrangement run that count as an unbounded recursion. Measured separation: gate-bounded oscillations top out at 4 cycles, true loops start at 18, so the whole [5,17] range is safe.")]
	public int tripCycles = 8;

	[Tooltip("A periodic run only counts when this round has revealed more than this multiple of the round-start pool size — a cycle that needs the round boundary to continue is bounded by the gate's per-round charge.")]
	public int starvationFactor = 3;

	[Tooltip("Longest cycle period searched (measured true loops have period 1-2; a longer period only makes a run harder to detect).")]
	public int maxPeriod = 6;

	[Tooltip("Unambiguous-duration backstop: a periodic run this long trips even without the starvation test.")]
	public int tripCyclesBackstop = 24;

	[Tooltip("TELEMETRY ONLY since criterion v2: raw identical-arrangement sightings within one round, kept for logs and the trip journal (gate-bounded oscillations reach 3-4 of these).")]
	public int sightingsToTrip = 3;

	private const int HashHistoryCap = 128;
	/// <summary>Recent arrangement samples of the current round (ring, capped) — the periodic-run input.</summary>
	private readonly List<uint> _hashHistory = new List<uint>(HashHistoryCap);
	private int _revealsThisRound;
	private int _roundStartPoolSize;

	/// <summary>Sticky trip latch for the current combat; ResetState (combat cleanup) clears it.</summary>
	public bool Tripped { get; private set; }

	/// <summary>Number of trip events this combat; sticky — fires only for the first arrangement reaching the threshold (ResetState clears it).</summary>
	public int TripCount { get; private set; }

	/// <summary>Arrangement hash of the most recent trip (RngDigest hex via RngDigest.ToHex).</summary>
	public uint LastTripHash { get; private set; }

	/// <summary>Cycle period of the tripping run (criterion v2 evidence).</summary>
	public int LastTripPeriod { get; private set; }

	/// <summary>Cycle repetitions of the tripping run (criterion v2 evidence).</summary>
	public int LastTripCycles { get; private set; }

	/// <summary>Reveals sampled in the round the trip happened in.</summary>
	public int LastTripReveals { get; private set; }

	/// <summary>True when the tripping round was starved (reveals &gt; starvationFactor x round-start pool).</summary>
	public bool LastTripRoundStarved { get; private set; }

	/// <summary>Reveals sampled in the current round (criterion v2 starvation test input).</summary>
	public int RevealsThisRound { get { return _revealsThisRound; } }

	/// <summary>Combined-deck size captured at the current round's start (0 = no round-start notice seen).</summary>
	public int RoundStartPoolSize { get { return _roundStartPoolSize; } }

	/// <summary>Arrangement hash of the most recent NotifyRevealBoundary sample (diagnostics).</summary>
	public uint LastSampledHash { get; private set; }

	/// <summary>Total sightings recorded since the last round start (telemetry since criterion v2; tests).</summary>
	public int SightingsThisRound { get; private set; }

	/// <summary>P2 attribution hook (plan §3 ②): fired once per trip with the tripping arrangement hash.</summary>
	public event Action<uint> OnCycleTripped;

	private readonly Dictionary<uint, int> _sightings = new Dictionary<uint, int>();

	private void Awake()
	{
		Me = this;
	}

	public void ResetState()
	{
		Tripped = false;
		TripCount = 0;
		LastTripHash = 0;
		LastTripPeriod = 0;
		LastTripCycles = 0;
		LastTripReveals = 0;
		LastTripRoundStarved = false;
		LastSampledHash = 0;
		SightingsThisRound = 0;
		_sightings.Clear();
		_hashHistory.Clear();
		_revealsThisRound = 0;
		_roundStartPoolSize = 0;
	}

	/// <summary>
	/// Round boundary (Start Card shuffle): a fresh arrangement era begins — clear the sample
	/// history and capture the new pool size (criterion v2's starvation test compares reveals
	/// against it; a cycle that only survives because the round re-armed the gate's charge is
	/// bounded, so it must not be able to claim a periodic run across this boundary).
	/// </summary>
	public void NotifyRoundStart()
	{
		_sightings.Clear();
		SightingsThisRound = 0;
		_hashHistory.Clear();
		_revealsThisRound = 0;
		var cm = CombatManager.Me;
		_roundStartPoolSize = cm != null && cm.combinedDeckZone != null ? cm.combinedDeckZone.Count : 0;
	}

	/// <summary>
	/// Samples the current arrangement once per completed reveal (CombatManager confirm
	/// paths :952 / :915, right after the chain-generation reset). Trips when the sample
	/// sequence holds a periodic run long enough to be unbounded (criterion v2, §26).
	/// </summary>
	public void NotifyRevealBoundary()
	{
		uint hash = ComputeCurrentArrangementHash();
		LastSampledHash = hash;
		SightingsThisRound++;
		_sightings.TryGetValue(hash, out int count);
		count++;
		_sightings[hash] = count;

		_revealsThisRound++;
		_hashHistory.Add(hash);
		if (_hashHistory.Count > HashHistoryCap) _hashHistory.RemoveAt(0);

		if (Tripped) return;

		int cycles = LongestPeriodicRunCycles(out int period);
		if (cycles < tripCycles) return;
		bool roundStarved = _roundStartPoolSize > 0 && _revealsThisRound > starvationFactor * _roundStartPoolSize;
		if (cycles < tripCyclesBackstop && !roundStarved) return;

		Trip(hash, period, cycles, roundStarved);
	}

	/// <summary>
	/// Longest run of samples that repeats with a fixed period p (p in 1..maxPeriod), expressed in
	/// cycle repetitions: k consecutive period matches count as (k + p) / p cycles. Criterion v2
	/// (§26): a gate-bounded oscillation tops out at 4 cycles while true loops start at 18, so the
	/// cycle count — not the raw sighting count — is what separates the two.
	/// </summary>
	private int LongestPeriodicRunCycles(out int period)
	{
		period = 0;
		int best = 0;
		int n = _hashHistory.Count;
		for (int p = 1; p <= maxPeriod && p < n; p++)
		{
			int streak = 0;
			for (int i = p; i < n; i++)
			{
				if (_hashHistory[i] == _hashHistory[i - p])
				{
					if (streak == 0) streak = p;
					streak++;
				}
				else
				{
					streak = 0;
				}
				int cycles = (streak + p) / p;
				if (cycles > best)
				{
					best = cycles;
					period = p;
				}
			}
		}
		return best;
	}

	private void Trip(uint hash, int period, int cycles, bool roundStarved)
	{
		Tripped = true;
		TripCount++;
		LastTripHash = hash;
		LastTripPeriod = period;
		LastTripCycles = cycles;
		LastTripReveals = _revealsThisRound;
		LastTripRoundStarved = roundStarved;

		// P2 hook, deferred by user ruling 2026-09-19: record the evidence NOW so the expensive
		// attribution simulation can run out of combat. Nothing is simulated here.
		// §20: the accusation has to name a server deck row, so the live ghost's deck_id rides
		// along (0 = local default-pool enemy, which has no row and is not player content).
		var cm = CombatManager.Me;
		var ghost = OpponentDeckCache.Current;
		InfinityTripJournal.Record(hash, TripCount, SightingsThisRound, Rng.CombatSeed,
			cm != null ? cm.playerDeck : null, cm != null ? cm.enemyDeck : null,
			ghost != null ? ghost.deckId : 0,
			cycles, period, roundStarved);
		TestManager.Log("[CombatArrangementCycleDetector] Arrangement cycle tripped: " + cycles
			+ "-cycle period-" + period + " run of arrangement " + RngDigest.ToHex(hash)
			+ " within one round (revealsThisRound=" + _revealsThisRound
			+ ", roundStartPool=" + _roundStartPoolSize + ", starved=" + roundStarved
			+ ") — unbounded recursion suspected; observation-only, the P2 attribution sim gates any forced conclusion");
		OnCycleTripped?.Invoke(hash);
	}

	/// <summary>
	/// Pure hash of the combined deck arrangement: cardTypeID + side (owner/enemy/neutral)
	/// per slot, index 0 -> bottom (last revealed). Side-effect free; used by the sampler and by tests.
	/// </summary>
	public uint ComputeCurrentArrangementHash()
	{
		var cm = CombatManager.Me;
		uint hash = RngDigest.OffsetBasis;
		if (cm == null || cm.combinedDeckZone == null) return hash;

		var ownerRef = cm.ownerPlayerStatusRef;
		foreach (var card in cm.combinedDeckZone)
		{
			var cardScript = card != null ? card.GetComponent<CardScript>() : null;
			hash = RngDigest.Fnv1a(hash, cardScript != null ? cardScript.cardTypeID : null);
			int side = 0;
			if (cardScript != null && cardScript.myStatusRef != null)
			{
				side = cardScript.myStatusRef == ownerRef ? 1 : 2;
			}
			hash = RngDigest.Fnv1a(hash, side);
		}
		return hash;
	}
}
