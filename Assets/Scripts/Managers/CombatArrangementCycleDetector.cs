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
/// Round safety: a quiescent full round samples each arrangement exactly once
/// (sample period = deck size, samples per round = deck size - 1), and the round
/// boundary (Start Card shuffle) clears the sighting set — so normal rotation can
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

	[Tooltip("Identical-arrangement sightings within one round that count as an unbounded recursion (2026-09-18 design: the 3rd sighting, so finite counter-limited loops stay below the threshold).")]
	public int sightingsToTrip = 3;

	/// <summary>Sticky trip latch for the current combat; ResetState (combat cleanup) clears it.</summary>
	public bool Tripped { get; private set; }

	/// <summary>Number of distinct arrangements that have reached the trip threshold this combat.</summary>
	public int TripCount { get; private set; }

	/// <summary>Arrangement hash of the most recent trip (RngDigest hex via RngDigest.ToHex).</summary>
	public uint LastTripHash { get; private set; }

	/// <summary>Arrangement hash of the most recent NotifyRevealBoundary sample (diagnostics).</summary>
	public uint LastSampledHash { get; private set; }

	/// <summary>Total sightings recorded since the last round start (diagnostics/tests).</summary>
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
		LastSampledHash = 0;
		SightingsThisRound = 0;
		_sightings.Clear();
	}

	/// <summary>Round boundary (Start Card shuffle): a fresh arrangement era begins — clear sightings.</summary>
	public void NotifyRoundStart()
	{
		_sightings.Clear();
		SightingsThisRound = 0;
	}

	/// <summary>
	/// Samples the current arrangement once per completed reveal (CombatManager confirm
	/// paths :952 / :915, right after the chain-generation reset). Trips on the Nth
	/// sighting of an identical arrangement within the current round.
	/// </summary>
	public void NotifyRevealBoundary()
	{
		uint hash = ComputeCurrentArrangementHash();
		LastSampledHash = hash;
		_sightings.TryGetValue(hash, out int count);
		count++;
		_sightings[hash] = count;
		SightingsThisRound++;

		if (Tripped || count != sightingsToTrip) return;

		Tripped = true;
		TripCount++;
		LastTripHash = hash;
		// P2 hook, deferred by user ruling 2026-09-19: record the evidence NOW so the expensive
		// attribution simulation can run out of combat. Nothing is simulated here.
		var cm = CombatManager.Me;
		InfinityTripJournal.Record(hash, TripCount, SightingsThisRound, Rng.CombatSeed,
			cm != null ? cm.playerDeck : null, cm != null ? cm.enemyDeck : null);
		TestManager.Log("[CombatArrangementCycleDetector] Arrangement cycle tripped: sighting #" + count
			+ " of arrangement " + RngDigest.ToHex(hash) + " within one round (sightingsThisRound=" + SightingsThisRound
			+ ") — unbounded recursion suspected; observation-only, the P2 attribution sim gates any forced conclusion");
		OnCycleTripped?.Invoke(hash);
	}

	/// <summary>
	/// Pure hash of the combined deck arrangement: cardTypeID + side (owner/enemy/neutral)
	/// per slot, index 0 -> top. Side-effect free; used by the sampler and by tests.
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
