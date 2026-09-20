using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tests for the L1 arrangement-cycle infinity detector
/// (plans/plan-infinity-detection-2026-09-17.md §3/§14, §15 corrections, §26 criterion v2).
/// The rule under test (v2, 2026-09-20): a PERIODIC run of identical arrangements — the same
/// arrangement sequence repeating for >= tripCycles cycles with a fixed period — inside a round
/// that the run starved (reveals > 3 x round-start pool), or a run long enough to be unambiguous
/// (backstop). Raw repetition is telemetry only: after the once-per-round revive gate shipped, a
/// gate-bounded oscillation still repeats 3-4 times per round while rounds advance normally
/// (measured: bounded forms top out at 4 cycles, true loops start at 18).
/// TRIGGER FIDELITY (2026-09-19, plan §15): HeadlessCombatTestFixture builds its own
/// GameEventStorage event instances, so prefab listeners — which are serialized against the
/// REAL event assets — can never hear them. The legacy bridge worked around that by pointing
/// every listener at onMeRevealed, which silently rewrites cross-card triggers: the relic
/// behind the lethal sample listens to OnHostileCurseRevealed, so it degenerated into "fires
/// on my own reveal" and the sample measured a DIFFERENT loop than production. These tests
/// therefore wire listeners to the fixture event matching their real asset (see
/// MapRealEventToFixtureEvent) and set curseCardTypeID, mirroring GameScene.unity.
/// Specimen roles after the 2026-09-20 revive gate (plan-revive-loop-mitigation §8):
///   lethal infinite test — STILL unbounded (round-starved, ~100 cycles) → positive specimen;
///   non-lethal infinite test — GRAVE_HEXER is gated now, so it no longer loops → inverted into
///   the gate's regression guard (must NOT trip; weaken the gate and this test goes red).
/// </summary>
public class ArrangementCycleDetectorTests : HeadlessCombatTestFixture
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";
	private const string StartCardPrefabPath = "Assets/Prefabs/Cards/System/StartCard.prefab";
	private const string FatiguePrefabPath = "Assets/Prefabs/Cards/System/Fatigue.prefab";
	private const int MaxIterations = 5000;

	[Test]
	public void PeriodicRunInStarvedRoundTrips()
	{
		var detector = CreateDetector();
		foreach (var card in BuildDeck("CYC_A", "CYC_B", "CYC_C"))
		{
			CombatManager.combinedDeckZone.Add(card);
		}
		detector.NotifyRoundStart(); // pool = 3, so starvation needs more than 9 reveals

		// Samples 1..9: an identical-arrangement run of 9 cycles that has NOT starved the round
		// (9 reveals <= 3 x pool) — below BOTH v2 conditions, so it must not trip.
		for (int i = 0; i < 9; i++)
		{
			detector.NotifyRevealBoundary();
		}
		Assert.IsFalse(detector.Tripped,
			"9 periodic cycles in a round that is not starved must not trip: a cycle the round boundary can re-arm is gate-bounded");
		Assert.AreEqual(9, detector.SightingsThisRound, "raw sightings stay telemetry");

		// Sample 10 pushes the round past 3 x pool: now the periodic run is unbounded evidence.
		detector.NotifyRevealBoundary();
		Assert.IsTrue(detector.Tripped,
			"a periodic run past tripCycles in a starved round must trip");
		Assert.AreEqual(1, detector.TripCount);
		Assert.AreNotEqual(0u, detector.LastTripHash);
		Assert.AreEqual(1, detector.LastTripPeriod, "a constant arrangement sequence is period 1");
		Assert.GreaterOrEqual(detector.LastTripCycles, 8);
		Assert.IsTrue(detector.LastTripRoundStarved);
	}

	[Test]
	public void BoundedRepetitionDoesNotTrip()
	{
		// Criterion v2 pin (2026-09-20 §26): the gate-bounded oscillation shape — a handful of
		// identical arrangements inside a normal-length round — is exactly what the live
		// GRAVE_HEXER x2 family does now (measured: 4 cycles max, rounds advancing).
		var detector = CreateDetector();
		foreach (var card in BuildDeck("CYC_A", "CYC_B", "CYC_C", "CYC_D", "CYC_E", "CYC_F"))
		{
			CombatManager.combinedDeckZone.Add(card);
		}
		detector.NotifyRoundStart(); // pool = 6

		for (int i = 0; i < 4; i++)
		{
			detector.NotifyRevealBoundary();
		}

		Assert.IsFalse(detector.Tripped,
			"4 identical samples are far below tripCycles and the round is nowhere near starved — the exact false positive v2 removes");
		Assert.AreEqual(4, detector.SightingsThisRound, "the raw sighting telemetry still counts them");
		Assert.AreEqual(4, detector.RevealsThisRound);
	}

	[Test]
	public void RoundStartResetClearsTheRun()
	{
		var detector = CreateDetector();
		foreach (var card in BuildDeck("CYC_A", "CYC_B", "CYC_C"))
		{
			CombatManager.combinedDeckZone.Add(card);
		}
		detector.NotifyRoundStart();

		for (int i = 0; i < 9; i++)
		{
			detector.NotifyRevealBoundary();
		}
		Assert.IsFalse(detector.Tripped, "9 cycles without starvation are not yet a trip");

		detector.NotifyRoundStart();
		for (int i = 0; i < 9; i++)
		{
			detector.NotifyRevealBoundary();
		}

		Assert.IsFalse(detector.Tripped,
			"the round boundary cleared the run, so 9 fresh samples cannot reach the cycle threshold again");
		Assert.AreEqual(9, detector.SightingsThisRound, "telemetry restarts at the round boundary");
	}

	[Test]
	public void ResetStateClearsTripLatchForNewCombat()
	{
		var detector = CreateDetector();
		foreach (var card in BuildDeck("CYC_A", "CYC_B", "CYC_C"))
		{
			CombatManager.combinedDeckZone.Add(card);
		}
		detector.NotifyRoundStart();
		for (int i = 0; i < 12; i++)
		{
			detector.NotifyRevealBoundary();
		}
		Assert.IsTrue(detector.Tripped);

		detector.ResetState();
		Assert.IsFalse(detector.Tripped, "combat cleanup (ResetState) must clear the trip latch");
		Assert.AreEqual(0, detector.SightingsThisRound);
		Assert.AreEqual(0, detector.TripCount);
		Assert.AreEqual(0, detector.RevealsThisRound);
		Assert.AreEqual(0, detector.RoundStartPoolSize);
	}

	[Test]
	public void QuiescentFullRoundRotationDoesNotTrip()
	{
		var detector = CreateDetector();
		foreach (var card in BuildDeck("CYC_A", "CYC_B", "CYC_C", "CYC_D"))
		{
			CombatManager.combinedDeckZone.Add(card);
		}

		// One full quiescent round: each iteration moves the top card to the bottom
		// (reveal + confirm with no effects) and samples once — deck size - 1 samples,
		// all rotations distinct within the round.
		for (int i = 0; i < 3; i++)
		{
			int topIndex = CombatManager.combinedDeckZone.Count - 1;
			var top = CombatManager.combinedDeckZone[topIndex];
			CombatManager.combinedDeckZone.RemoveAt(topIndex);
			CombatManager.combinedDeckZone.Insert(0, top);
			detector.NotifyRevealBoundary();
		}

		Assert.IsFalse(detector.Tripped,
			"a full quiescent round must never trip: the repeat only exists ACROSS round boundaries (period = deck size), which the round reset excludes");
	}

	[Test]
	public void SideAndOrderBothFeedTheHash()
	{
		var detector = CreateDetector();
		var ownerCopy = CreateCard(true, "OwnerCopy", "CYC_DUP");
		var enemyCopy = CreateCard(false, "EnemyCopy", "CYC_DUP");
		CombatManager.combinedDeckZone.Add(ownerCopy);
		CombatManager.combinedDeckZone.Add(enemyCopy);

		uint forward = detector.ComputeCurrentArrangementHash();
		CombatManager.combinedDeckZone.Clear();
		CombatManager.combinedDeckZone.Add(enemyCopy);
		CombatManager.combinedDeckZone.Add(ownerCopy);
		uint swapped = detector.ComputeCurrentArrangementHash();

		Assert.AreNotEqual(forward, swapped, "same cardTypeIDs in a different order/side mix must hash differently");
		Assert.AreEqual(0, detector.SightingsThisRound, "hashing must be side-effect free");
	}

	[Test]
	public void TripRaisesOnCycleTrippedHook()
	{
		var detector = CreateDetector();
		foreach (var card in BuildDeck("CYC_A", "CYC_B", "CYC_C"))
		{
			CombatManager.combinedDeckZone.Add(card);
		}
		detector.NotifyRoundStart();

		uint? firedHash = null;
		detector.OnCycleTripped += hash => firedHash = hash;

		for (int i = 0; i < 9; i++)
		{
			detector.NotifyRevealBoundary();
		}
		Assert.IsFalse(firedHash.HasValue, "the hook must not fire before the criterion is met (no starvation yet)");
		detector.NotifyRevealBoundary();

		Assert.IsTrue(firedHash.HasValue, "the P2 attribution hook must fire exactly on trip");
		Assert.AreEqual(detector.LastTripHash, firedHash.Value);
	}

	[Test]
	public void CrossRoundPeriodicArrangement_DoesNotTrip()
	{
		var detector = CreateDetector();
		// Pure rule-boundary unit test: the sighting set is cleared at every round start, so a
		// sequence whose period is exactly one round can never reach the 3rd sighting. This
		// pins the rule's scope — it does NOT model the lethal sample deck. (Plan §14 asserted
		// the lethal sample behaved this way; §15 disproved that — it was an artifact of the
		// legacy listener re-pointing, and under production wiring the deck starves the round
		// boundary and trips within round 1. See LethalInfiniteDeck_TripsCycleDetector.)
		var roundPattern = BuildDeck("CYC_P1", "CYC_P2", "CYC_P3", "CYC_P4", "CYC_P5");
		for (int round = 0; round < 3; round++)
		{
			CombatManager.combinedDeckZone.Clear();
			foreach (var card in roundPattern)
			{
				CombatManager.combinedDeckZone.Add(card);
			}
			for (int sample = 0; sample < 4; sample++)
			{
				int topIndex = CombatManager.combinedDeckZone.Count - 1;
				var top = CombatManager.combinedDeckZone[topIndex];
				CombatManager.combinedDeckZone.RemoveAt(topIndex);
				CombatManager.combinedDeckZone.Insert(0, top);
				detector.NotifyRevealBoundary();
			}
			detector.NotifyRoundStart();
		}

		Assert.IsFalse(detector.Tripped,
			"a cross-round-periodic sequence must not trip the within-round rule; the P2 sim owns that shape");
		Assert.AreEqual(0, detector.SightingsThisRound, "NotifyRoundStart clears the sighting set");
	}

	[Test]
	public void LethalInfiniteDeck_TripsCycleDetector()
	{
		// Plan §4 acceptance specimen, restored under PRODUCTION trigger wiring (§15):
		// CURSE_GARDENER (OnMeRevealed -> ReviveTheirCards(1, typeIDFilter=JU_ON), plus
		// CurseEffect.EnhanceCurse each activation) and RELIC_CURSE_REVIVAL
		// (OnHostileCurseRevealed -> ReviveMyCards(1, excludeSelf)) form a tight within-round
		// loop: the revived curse is revealed, which revives the gardener, whose reveal revives
		// the curse again. The loop's own pump eventually kills the enemy, so it is unbounded
		// but self-terminating — and it must trip the detector well before that.
		CombatManager.playerDeck = LoadSampleDeck("lethal infinite test");
		CombatManager.enemyDeck = CreateInertStubDeck(3);
		CombatManager.startCardPrefab = LoadStartCardPrefab();
		EnableProductionTriggerWiring();
		CombatManager.GatherDecks();
		BridgeAllCards();

		// BOTH sides unkillable, so the only terminator left is the L0 cap (this is what the §16.3
		// split means in practice): a 30-HP enemy dies to the loop's own pump in ~18 reveals
		// (round 1, not yet starved), and a 30-HP owner dies to fatigue's self-damage around reveal
		// 350. Either death ends the combat before the caps and makes the flag run order/seed
		// dependent — measured, and the reason this test flapped. hpMax must move too: the damage
		// path clamps hp to hpMax.
		CombatManager.ownerPlayerStatusRef.hpMax = 100000000;
		CombatManager.ownerPlayerStatusRef.hp = 100000000;
		CombatManager.enemyPlayerStatusRef.hpMax = 100000000;
		CombatManager.enemyPlayerStatusRef.hp = 100000000;
		PrepareOvertimeFatigue();

		var guard = CreateGuard(200, 1500, 60);
		var detector = CreateDetector();

		// §20.3: the live ghost's deck row has to ride along with the trip — it is what the
		// loop-report upload accuses (DeckSaver sets this when it injects a server ghost).
		InfinityTripJournal.Clear();
		OpponentDeckCache.SetCurrentOpponent(new OpponentDeckEntry { deckId = 505, username = "ghost" });

		int iterations = RunDriverWithDetector(detector, guard, MaxIterations);

		Assert.Less(iterations, MaxIterations, "driver must terminate within the bound" + Diag(detector, guard, iterations));
		Assert.IsTrue(detector.Tripped,
			"the lethal revive loop repeats its arrangement inside round 1 and must trip the detector"
			+ Diag(detector, guard, iterations));
		Assert.GreaterOrEqual(detector.LastTripCycles, 8,
			"criterion v2: the trip must carry a periodic run well past the cycle threshold"
			+ Diag(detector, guard, iterations));
		Assert.IsTrue(detector.LastTripRoundStarved,
			"criterion v2: the lethal loop starves the round boundary (reveals >> 3 x round-start pool)"
			+ Diag(detector, guard, iterations));
		Assert.Greater(InfinityTripJournal.Count, 0, "a trip must be journaled for the P2/P3 pipeline");
		Assert.AreEqual(505, InfinityTripJournal.Pending[0].EnemyDeckId,
			"the journaled trip must name the server deck row the report accuses (§20.3)");
		OpponentDeckCache.SetCurrentOpponent(null);
		Assert.IsTrue(guard.ConcludeRequested,
			"with an unkillable opponent nothing else ends the loop, so the L0 caps must conclude it — "
			+ "the trip is the flag criterion, the conclusion is the harm (§16.3)" + Diag(detector, guard, iterations));
		Debug.Log("[CycleDetector] lethal specimen" + Diag(detector, guard, iterations));
	}

	[Test]
	public void NonLethalInfiniteDeck_NoLongerLoopsAndMustNotTrip()
	{
		// INVERTED SPECIMEN (2026-09-20, plan §26 with plan-revive-loop-mitigation §8): GRAVE_HEXER
		// is one of the eight hub revive effects the once-per-round gate now caps, so this deck no
		// longer starves the round — measured: 5-6 rounds, ~50 reveals, ending by death, one
		// arrangement reaching only 2 sightings. The detector must NOT flag it: flagging a
		// gate-bounded repetition is exactly the v2 false positive §26 removes. Kept as the GATE's
		// regression guard — weaken or remove the gate and the loop (and this red) returns.
		CombatManager.playerDeck = LoadSampleDeck("non-lethal infinite test");
		CombatManager.enemyDeck = CreateInertStubDeck(3);
		CombatManager.startCardPrefab = LoadStartCardPrefab();
		EnableProductionTriggerWiring();
		CombatManager.GatherDecks();
		BridgeAllCards();

		CombatManager.ownerPlayerStatusRef.hp = 30;
		CombatManager.enemyPlayerStatusRef.hp = 30;
		PrepareOvertimeFatigue();

		var guard = CreateGuard(200, 1500, 60);
		var detector = CreateDetector();
		int iterations = RunDriverWithDetector(detector, guard, MaxIterations);

		Assert.Less(iterations, MaxIterations, "driver must terminate within the bound" + Diag(detector, guard, iterations));
		Assert.IsFalse(detector.Tripped,
			"the once-per-round revive gate bounded this loop, so it must NOT be flagged as unbounded"
			+ Diag(detector, guard, iterations));
		Assert.IsTrue(CombatManager.Me.IsDeathVisuallyLanded || CombatManager.enemyPlayerStatusRef.hp <= 0
			|| CombatManager.ownerPlayerStatusRef.hp <= 0,
			"the combat must still converge by death — the detector is observation-only"
			+ Diag(detector, guard, iterations));
		Assert.IsFalse(guard.ConcludeRequested,
			"the gated loop converges naturally; the L0 caps must stay unused" + Diag(detector, guard, iterations));
		Debug.Log("[CycleDetector] non-lethal specimen (must NOT trip)" + Diag(detector, guard, iterations));
	}

	private string Diag(CombatArrangementCycleDetector detector, CombatBudgetGuard guard, int iterations)
	{
		return " DIAG iters=" + iterations
			+ " reveals=" + CombatManager.totalCardsRevealed
			+ " round=" + CombatManager.roundNumRef.value
			+ " deck=" + CombatManager.combinedDeckZone.Count
			+ " sightingsThisRound=" + detector.SightingsThisRound
			+ " tripCount=" + detector.TripCount
			+ " oHp=" + CombatManager.ownerPlayerStatusRef.hp
			+ " eHp=" + CombatManager.enemyPlayerStatusRef.hp
			+ " forceClear=" + guard.RoundForceClearActive
			+ " conclude=" + guard.ConcludeRequested;
	}

	// ---- test wiring ----

	private CombatArrangementCycleDetector CreateDetector()
	{
		var detector = CreateGameObject("ArrangementCycleDetector").AddComponent<CombatArrangementCycleDetector>();
		// Edit Mode never runs Awake, so the static Me stays null and the CombatManager
		// hooks would hit nothing — same reflection fix CreateGuard uses in the
		// termination tests.
		typeof(CombatArrangementCycleDetector)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?.SetValue(null, detector);
		return detector;
	}

	private CombatBudgetGuard CreateGuard(int perRound, int total, int rounds)
	{
		var guard = CreateGameObject("BudgetGuard").AddComponent<CombatBudgetGuard>();
		guard.maxRevealsPerRound = perRound;
		guard.maxTotalReveals = total;
		guard.maxRounds = rounds;
		typeof(CombatBudgetGuard)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?.SetValue(null, guard);
		return guard;
	}

	/// <summary>
	/// Mirrors the production wiring of GameScene.unity's GameEventStorage: the curse type id
	/// gates the onEnemyCurseCardRevealed broadcast, so it must be set or the relic's passive
	/// (the lethal loop's second leg) can never fire. Must run BEFORE GatherDecks.
	/// </summary>
	private void EnableProductionTriggerWiring()
	{
		Assert.IsNotNull(GameEventStorage.curseCardTypeID, "fixture must expose a curseCardTypeID StringSO");
		GameEventStorage.curseCardTypeID.value = "JU_ON";
	}

	/// <summary>
	/// Production overtime fatigue setup (values mirror InfiniteDeckTerminationTests, which
	/// uses the production reveal-count threshold). Reveal-count fatigue is the only clock
	/// that keeps ticking inside a round-starved loop.
	/// </summary>
	private void PrepareOvertimeFatigue()
	{
		CombatManager.overtimeRoundThreshold = 2;
		CombatManager.fatigueRevealThreshold = 40;
		// Bare AddComponent instance: fatigueAmount defaults to 0, which makes
		// AddFatigueCards a no-op.
		CombatManager.fatigueAmount = 1;
		if (CombatManager.cardToAddWhenOvertime == null)
		{
			CombatManager.cardToAddWhenOvertime = AssetDatabase.LoadAssetAtPath<GameObject>(FatiguePrefabPath);
		}
		Assert.IsNotNull(CombatManager.cardToAddWhenOvertime, "fatigue card prefab must be resolvable");
	}

	private List<GameObject> BuildDeck(params string[] cardTypeIDs)
	{
		var deck = new List<GameObject>();
		foreach (var id in cardTypeIDs)
		{
			deck.Add(CreateCard(true, id, id));
		}
		return deck;
	}

	private GameObject LoadStartCardPrefab()
	{
		var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartCardPrefabPath);
		Assert.IsNotNull(prefab, "Start Card prefab missing: " + StartCardPrefabPath);
		return prefab;
	}

	/// <summary>
	/// Inert opponent (plan §3 木桩: no-effect cards, no pre-seeded curse). The combo decks
	/// seed their OWN curse - CurseEffect.EnhanceCurse spawns one whenever none exists - and the
	/// "at most one JU_ON" invariant is emergent (only EnhanceCurse / EnhanceFriendlyCurse create
	/// curses, and only from zero; ReviveEffect moves an existing one back), so a pre-seeded
	/// curse - let alone two - is a state the game cannot reach (plan §17). Measured 0/1/2 seeded
	/// curses: identical verdict, so zero is both canonical and sufficient.
	/// </summary>
	private DeckSO CreateInertStubDeck(int count)
	{
		var cards = new List<GameObject>();
		for (int i = 0; i < count; i++)
		{
			cards.Add(CreateCard(false, "InertStub" + i, "INERT_STUB"));
		}
		return CreateDeckSO(cards);
	}

	private DeckSO LoadSampleDeck(string assetName)
	{
		var deck = AssetDatabase.LoadAssetAtPath<DeckSO>($"{DeckFolder}/{assetName}.asset");
		Assert.IsNotNull(deck, $"sample deck missing: {DeckFolder}/{assetName}.asset");
		Assert.GreaterOrEqual(deck.deck.Count, 2, "sample deck should carry its combo cards");
		return deck;
	}

	/// <summary>
	/// Synchronous headless reveal loop over the fixture primitives, with the L0 guard wired
	/// at the same points the real CombatManager flow would hit it, sampling the arrangement at
	/// the reveal-cycle boundary (the same hook CombatManager calls at its confirm paths).
	/// Termination is by LOGIC HP, not IsDeathVisuallyLanded: this fixture wires a dummy
	/// CombatInfoDisplayer, so the visual flag reads the DISPLAY layer, whose enemy-HP accessor
	/// returns a queue-frozen value while hits are pending — in a tight revive loop that never
	/// lands even after the killing blow.
	/// </summary>
	private int RunDriverWithDetector(CombatArrangementCycleDetector detector, CombatBudgetGuard guard, int maxIterations)
	{
		int iterations = 0;
		while (iterations++ < maxIterations)
		{
			if (guard.ConcludeRequested) break;
			if (CombatManager.enemyPlayerStatusRef.hp <= 0 || CombatManager.ownerPlayerStatusRef.hp <= 0) break;

			if (CombatManager.revealZone == null)
			{
				if (CombatManager.combinedDeckZone.Count == 0) break;
				RevealTopCard();
				// Production fires the reveal-count fatigue check per reveal inside
				// RevealNextCardCore (:1077) — the fixture primitive bypasses that path.
				typeof(CombatManager)
					.GetMethod("CheckFatigueByRevealCount", BindingFlags.NonPublic | BindingFlags.Instance)
					?.Invoke(CombatManager.Me, null);
				// Fixture RevealTopCard bypasses RevealNextCardCore — notify the L0 guard manually.
				guard.NotifyReveal(CombatManager.cardsRevealedThisRound, CombatManager.totalCardsRevealed,
					CombatManager.combinedDeckZone.Count, CombatManager.roundNumRef.value);
			}

			var revealed = CombatManager.revealZone.GetComponent<CardScript>();
			bool wasStartCard = revealed != null && revealed.isStartCard;

			BridgeCard(CombatManager.revealZone);

			if (wasStartCard)
			{
				var container = CombatManager.revealZone.GetComponentInChildren<CostNEffectContainer>();
				container?.InvokeEffectEvent();
			}
			else if (!guard.RoundForceClearActive)
			{
				TriggerRevealedCard();
			}
			PutRevealedCardToBottom();

			if (wasStartCard)
			{
				CombatManager.Me.OnStartCardShuffleAnimationComplete();
			}

			EffectChainManager.Me.CloseOpenedChain();
			EffectChainManager.Me.ResetGenerationGuards();

			detector.NotifyRevealBoundary();
		}
		return iterations;
	}

	// ---- Edit Mode bridge helpers ----

	private readonly HashSet<GameObject> _bridged = new HashSet<GameObject>();

	private void BridgeCard(GameObject card)
	{
		if (card == null || !_bridged.Add(card)) return;

		var cardScript = card.GetComponent<CardScript>();
		var myCardField = typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance);
		var myCardScriptField = typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
		var combatManagerField = typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		var containerField = typeof(CostNEffectContainer).GetField("_myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);

		foreach (var effect in card.GetComponentsInChildren<EffectScript>(true))
		{
			myCardField?.SetValue(effect, card);
			myCardScriptField?.SetValue(effect, cardScript);
			combatManagerField?.SetValue(effect, CombatManager);
		}
		foreach (var container in card.GetComponentsInChildren<CostNEffectContainer>(true))
		{
			containerField?.SetValue(container, cardScript);
			ForceEditorCallState(container, "effectEvent");
			ForceEditorCallState(container, "checkCostEvent");
		}
		foreach (var listener in card.GetComponentsInChildren<GameEventListener>(true))
		{
			ForceEditorCallState(listener, "response");
			GameEvent target = MapRealEventToFixtureEvent(listener.@event);
			listener.@event = target;
			target.RegisterListener(listener);
		}
	}

	private void BridgeAllCards()
	{
		foreach (var card in CombatManager.combinedDeckZone)
		{
			BridgeCard(card);
		}
	}

	/// <summary>
	/// Re-points a listener from the REAL event asset it was serialized against to the
	/// fixture's equivalent instance, so cross-card triggers keep their production semantics.
	/// Names come from Assets/SORefs/GameEvents/; the fixture builds its own GameEvent
	/// instances, so identity matching is impossible and name matching is the contract.
	/// Unknown events fall back to onMeRevealed, which is the legacy bridge behavior and keeps
	/// cards outside this combo working as before.
	/// </summary>
	private GameEvent MapRealEventToFixtureEvent(GameEvent real)
	{
		if (real == null) return GameEventStorage.onMeRevealed;
		switch (real.name)
		{
			case "OnMeRevealed": return GameEventStorage.onMeRevealed;
			case "OnHostileCurseRevealed": return GameEventStorage.onEnemyCurseCardRevealed;
			case "OnAnyCardRevealed": return GameEventStorage.onAnyCardRevealed;
			case "OnHostileCardRevealed": return GameEventStorage.onHostileCardRevealed;
			case "OnMeRevived": return GameEventStorage.onMeRevived;
			case "OnAnyCardRevived": return GameEventStorage.onAnyCardRevived;
			case "OnFriendlyCardRevived": return GameEventStorage.onFriendlyCardRevived;
			case "OnEnemyCardRevived": return GameEventStorage.onEnemyCardRevived;
			case "OnMeBuried": return GameEventStorage.onMeBuried;
			case "OnAnyCardBuried": return GameEventStorage.onAnyCardBuried;
			case "OnFriendlyCardBuried": return GameEventStorage.onFriendlyCardBuried;
			default: return GameEventStorage.onMeRevealed;
		}
	}

	private void ForceEditorCallState(Object owner, string fieldPath)
	{
		var so = new UnityEditor.SerializedObject(owner);
		var calls = so.FindProperty(fieldPath + ".m_PersistentCalls.m_Calls");
		if (calls == null) return;
		for (int i = 0; i < calls.arraySize; i++)
		{
			var state = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_CallState");
			if (state != null) state.intValue = 1;
		}
		so.ApplyModifiedPropertiesWithoutUndo();
	}
}
