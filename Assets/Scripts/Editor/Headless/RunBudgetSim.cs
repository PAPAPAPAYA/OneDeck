using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The infinity plan's core abstraction (plans/plan-infinity-detection-2026-09-17.md §2):
/// RunBudgetSim(deckA, deckB | dummy, seed) -> BudgetTripReport. One implementation shared by
/// combat attribution (P2), the offline batch scan (P5) and the editor tests.
/// A run is a synchronous, headless, deterministic replay of one combat through the rig's
/// reveal primitives. It is deliberately NOT a test: it lives in the Editor assembly so batch
/// tools can call it (the test fixture under Editor/Tests cannot be reused that way).
/// Determinism: the seed goes in through the PRODUCTION path — TestManager.overrideCombatSeed,
/// which CombatManager.GatherDecks reads before calling Rng.InitCombat — so the same
/// (decks, seed) pair reproduces the same combat, which is what makes the P2 attribution replay
/// ("re-run the same pairing", "enemy deck vs dummy") meaningful.
/// </summary>
public static class RunBudgetSim
{
	public const string StartCardPrefabPath = "Assets/Prefabs/Cards/System/StartCard.prefab";
	public const string FatiguePrefabPath = "Assets/Prefabs/Cards/System/Fatigue.prefab";

	/// <summary>
	/// Run knobs. Production() mirrors the shipped scene: the L0 caps at their default values
	/// and the overtime fatigue clock enabled with production thresholds. Set the guard caps to
	/// 0 to observe a loop without the L0 backstop (the arrangement-cycle verdict does not depend
	/// on the caps — that decoupling is the point of §3/§15).
	/// </summary>
	public class Options
	{
		/// <summary>Hard iteration backstop for the driver itself, independent of the L0 caps.</summary>
		public int MaxIterations = 20000;

		public int GuardPerRound = 200;
		public int GuardTotal = 1500;
		public int GuardRounds = 60;

		public int OwnerHp = 30;
		public int EnemyHp = 30;

		/// <summary>Overtime fatigue. Reveal-count fatigue is the only clock that keeps ticking
		/// inside a round-starved loop, so it stays on for the sample decks.</summary>
		public bool EnableOvertimeFatigue = true;
		public int OvertimeRoundThreshold = 2;
		public int FatigueRevealThreshold = 40;
		public int FatigueAmount = 1;

		public static Options Production()
		{
			return new Options();
		}
	}

	/// <summary>Replays deckA versus deckB. deckA is the owner (player) side.</summary>
	public static BudgetTripReport Run(DeckSO deckA, DeckSO deckB, int seed, Options options = null)
	{
		if (options == null) options = new Options();
		return Execute(deckA, deckB, seed, options, options.EnemyHp);
	}

	/// <summary>
	/// Replays deckA against an inert dummy opponent (§3 "木桩定义:敌方 = 无效果卡 × N + 大血量").
	/// This is the isolation form used by attribution: a deck that loops against a no-effect
	/// dummy loops on its OWN, so responsibility is one-sided.
	/// </summary>
	public static BudgetTripReport RunVsDummy(DeckSO deckA, int dummySize, int dummyHp, int seed, Options options = null)
	{
		if (options == null) options = new Options();
		// enemyHp travels as an argument, NOT through options: attribution reuses one Options
		// object across several runs, and mutating it here would leak the dummy's huge HP into
		// the later real-pairing replay.
		return Execute(deckA, null, seed, options, dummyHp, dummySize);
	}

	/// <summary>
	/// The inert dummy deck: plain cards with no effects and no listeners. Deliberately NOT
	/// flagged isStartCard — the driver treats isStartCard as the round boundary, so §3's
	/// "中立起手卡 × N" wording would turn every dummy reveal into a round boundary. A card with
	/// no effect container is equally inert without hijacking the round boundary.
	/// </summary>
	public static DeckSO BuildDummyDeck(HeadlessCombatRig rig, int count)
	{
		var cards = new List<GameObject>();
		for (int i = 0; i < count; i++)
		{
			cards.Add(rig.CreateCard(false, "DummyCard" + i, "DUMMY_CARD"));
		}
		return rig.CreateDeckSO(cards);
	}

	private static BudgetTripReport Execute(DeckSO deckA, DeckSO deckB, int seed, Options options,
		int enemyHp, int dummySize = 0)
	{
		var report = new BudgetTripReport();
		report.DeckA = deckA != null ? deckA.name : "null";
		report.Seed = seed;

		HeadlessCombatRig rig = HeadlessCombatRig.Create(options.GuardPerRound, options.GuardTotal, options.GuardRounds);
		// A simulation is not a live trip: suppress the journal for the whole run so the
		// attribution processor's own replays cannot queue themselves as new evidence.
		InfinityTripJournal.PushSuppression();
		try
		{
			var cm = rig.CombatManager;
			var guard = rig.Guard;
			var detector = rig.Detector;

			rig.SetCombatSeed(seed);
			rig.EnableProductionTriggerWiring();

			if (dummySize > 0)
			{
				deckB = BuildDummyDeck(rig, dummySize);
				// Label it explicitly: a runtime-created DeckSO has no asset name, and a scan
				// report is worthless if the opponent column is blank.
				report.DeckB = "dummy(" + dummySize + " cards)";
			}
			else
			{
				report.DeckB = deckB != null ? deckB.name : "null";
			}

			cm.playerDeck = deckA;
			cm.enemyDeck = deckB;
			cm.startCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartCardPrefabPath);

			cm.ownerPlayerStatusRef.hp = options.OwnerHp;
			cm.ownerPlayerStatusRef.hpMax = options.OwnerHp;
			cm.enemyPlayerStatusRef.hp = enemyHp;
			cm.enemyPlayerStatusRef.hpMax = enemyHp;

			if (options.EnableOvertimeFatigue)
			{
				cm.overtimeRoundThreshold = options.OvertimeRoundThreshold;
				cm.fatigueRevealThreshold = options.FatigueRevealThreshold;
				cm.fatigueAmount = options.FatigueAmount;
				cm.cardToAddWhenOvertime = AssetDatabase.LoadAssetAtPath<GameObject>(FatiguePrefabPath);
			}
			else
			{
				// 999 keeps the round-clock out of the way; without this a bare AddComponent
				// CombatManager would engage overtime almost immediately.
				cm.overtimeRoundThreshold = 999;
			}

			cm.GatherDecks();
			rig.BridgeAllCards();

			RunRevealLoop(rig, report, options);
		}
		finally
		{
			rig.Dispose();
			InfinityTripJournal.PopSuppression();
		}

		return report;
	}

	/// <summary>
	/// Synchronous reveal loop over the rig primitives, with the L0 guard notified at the points
	/// production notifies it, and the arrangement sampled at the reveal-cycle boundary the same
	/// way CombatManager does. Termination is by LOGIC HP, not IsDeathVisuallyLanded: the rig
	/// wires a dummy CombatInfoDisplayer, so the visual flag reads the display layer, whose HP
	/// accessors return queue-frozen values while hits are pending and never land in a tight
	/// revive loop.
	/// </summary>
	private static void RunRevealLoop(HeadlessCombatRig rig, BudgetTripReport report, Options options)
	{
		var cm = rig.CombatManager;
		var guard = rig.Guard;
		var detector = rig.Detector;

		var sightings = new Dictionary<uint, int>();
		int lastRound = cm.roundNumRef.value;
		int maxSightings = 0;
		int distinctInWorstRound = 0;
		int peakRevealsInRound = 0;
		int peakPoolSize = 0;
		bool roundForceClearSeen = false;
		int iterations = 0;

		while (iterations++ < options.MaxIterations)
		{
			if (guard.ConcludeRequested) break;
			if (cm.enemyPlayerStatusRef.hp <= 0 || cm.ownerPlayerStatusRef.hp <= 0) break;

			if (cm.revealZone == null)
			{
				if (cm.combinedDeckZone.Count == 0) break;
				rig.RevealTopCard();
				// Production fires the reveal-count fatigue check per reveal inside
				// RevealNextCardCore; the rig primitive bypasses that path.
				rig.CheckFatigueByRevealCount();
				// And production notifies the L0 guard from RevealNextCardCore.
				guard.NotifyReveal(cm.cardsRevealedThisRound, cm.totalCardsRevealed,
					cm.combinedDeckZone.Count, cm.roundNumRef.value);
			}

			var revealed = cm.revealZone.GetComponent<CardScript>();
			bool wasStartCard = revealed != null && revealed.isStartCard;

			rig.BridgeCard(cm.revealZone);

			if (wasStartCard)
			{
				// Production does not broadcast reveal events for the Start Card; it invokes the
				// shuffle container directly (CombatManager.RevealCards).
				var container = cm.revealZone.GetComponentInChildren<CostNEffectContainer>();
				container?.InvokeEffectEvent();
			}
			else if (!guard.RoundForceClearActive)
			{
				rig.TriggerRevealedCard();
			}
			rig.PutRevealedCardToBottom();

			if (wasStartCard)
			{
				cm.OnStartCardShuffleAnimationComplete();
			}

			// The real flow closes the chain and re-arms the loop guard at every reveal-cycle
			// boundary; without this the generation guard history burns every (card, effect) pair
			// on the first lap and all later reveals are silently skipped.
			rig.EffectChainManager.CloseOpenedChain();
			rig.EffectChainManager.ResetGenerationGuards();

			detector.NotifyRevealBoundary();

			// Mirror the detector's own round bookkeeping so the report can state how often an
			// arrangement repeated inside a single round.
			uint hash = detector.LastSampledHash;
			int round = cm.roundNumRef.value;
			if (round != lastRound)
			{
				int distinct = sightings.Count;
				if (maxSightings > 0 && distinct > distinctInWorstRound) distinctInWorstRound = distinct;
				sightings.Clear();
				lastRound = round;
			}
			sightings.TryGetValue(hash, out int count);
			count++;
			sightings[hash] = count;
			if (count > maxSightings) maxSightings = count;

			if (cm.cardsRevealedThisRound > peakRevealsInRound) peakRevealsInRound = cm.cardsRevealedThisRound;
			if (cm.combinedDeckZone.Count > peakPoolSize) peakPoolSize = cm.combinedDeckZone.Count;
			if (guard.RoundForceClearActive) roundForceClearSeen = true;
		}

		// Final round's stats (the loop can end mid-round).
		if (sightings.Count > distinctInWorstRound) distinctInWorstRound = sightings.Count;

		report.Iterations = iterations;
		report.TotalReveals = cm.totalCardsRevealed;
		report.Rounds = cm.roundNumRef.value;
		report.FinalDeckSize = cm.combinedDeckZone.Count;
		report.PeakRevealsInOneRound = peakRevealsInRound;
		report.PeakPoolSize = peakPoolSize;
		report.RoundForceClearUsed = roundForceClearSeen;
		report.BudgetCapConcluded = guard.ConcludeRequested;
		report.PeakCascadeDepth = guard.PeakCascadeDepth;
		report.ArrangementCycleTripped = detector.Tripped;
		report.TripHash = detector.LastTripHash;
		report.TripCount = detector.TripCount;
		report.MaxSightingsOfOneArrangement = maxSightings;
		report.DistinctArrangementsInWorstRound = distinctInWorstRound;
		report.OwnerHpFinal = cm.ownerPlayerStatusRef.hp;
		report.EnemyHpFinal = cm.enemyPlayerStatusRef.hp;
		report.OwnerDied = cm.ownerPlayerStatusRef.hp <= 0;
		report.EnemyDied = cm.enemyPlayerStatusRef.hp <= 0;
	}
}
