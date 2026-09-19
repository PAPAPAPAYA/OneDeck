using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Termination regressions for the canonical infinite-combo sample decks
/// (Assets/SORefs/Decks/test decks/chain tests/4.0/). Both decks loop forever on their
/// own; the tests pin down HOW each one must terminate under the current engine:
/// - lethal infinite test: the loop deals damage every cycle and must KILL the stub
///   opponent within the iteration bound, under production budget caps (the caps are
///   not supposed to be needed).
/// - non-lethal infinite test: GRAVE_HEXER's EnhanceCurse arms the stub opponent's
///   JU_ON curses, whose attack-self bites kill the ENEMY side within a few rounds —
///   faster than fatigue. The test pins that the combat still converges by DEATH with
///   the overtime machinery engaged (fatigue cards enter the deck) and the budget caps
///   (per-round clear, 60-round global cap) unused.
/// Driver fidelity notes: this is a synchronous logic-level loop over the fixture's
/// reveal primitives (RevealTopCard / TriggerRevealedCard / PutRevealedCardToBottom).
/// They bypass CombatManager.RevealNextCardCore / TriggerRevealedCardEffect, so the L0
/// guard is notified manually and the round force-clear skip is replicated here; the
/// fixture's PutRevealedCardToBottom also omits the R2 life-bounce. Round boundary =
/// Start Card reveal: fatigue check, round increment (real: StartCardShuffleEffect),
/// then OnStartCardShuffleAnimationComplete.
/// TRIGGER WIRING (2026-09-19, plan §15): these tests run the PRODUCTION trigger bridge —
/// BridgeCard maps each listener from the real event asset it was serialized against to the
/// fixture's matching instance, and curseCardTypeID is set, mirroring GameScene.unity. The
/// legacy bridge pointed every listener at onMeRevealed, which rewrote the lethal deck's
/// RELIC_CURSE_REVIVAL (real trigger OnHostileCurseRevealed) into "fires on my own reveal" and
/// made this file exercise a different loop than production.
/// </summary>
public class InfiniteDeckTerminationTests : HeadlessCombatTestFixture
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";
	private const string FatiguePrefabPath = "Assets/Prefabs/Cards/System/Fatigue.prefab";
	private const string StartCardPrefabPath = "Assets/Prefabs/Cards/System/StartCard.prefab";
	private const string JuOnPrefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab";
	private const int MaxIterations = 5000;

	/// <summary>
	/// The REAL Start Card prefab: its listener -> StartCardShuffleEffect.ExecuteShuffleEffect
	/// carries the whole round-boundary semantics (round increment, overtime fatigue check,
	/// Rng deck reshuffle, AlwaysBottom re-placement). A bare stub fires the boundary once and
	/// then sinks out of the revive-perturbed rotation forever, so no further round can start.
	/// </summary>
	private GameObject LoadStartCardPrefab()
	{
		var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartCardPrefabPath);
		Assert.IsNotNull(prefab, "Start Card prefab missing: " + StartCardPrefabPath);
		return prefab;
	}

	/// <summary>
	/// The combo decks' fuel: enemy-side JU_ON curses. Both loops are revive-axis
	/// (ReviveMyCards / ReviveTheirCards typeIDFilter=JU_ON + EnhanceCurse) — without
	/// curses in the enemy deck the "add curse" leg fizzles forever and nothing churns.
	/// Curses enter the grave by normal consumption, so the revivers have pool from
	/// the first consumed card onward.
	/// </summary>
	private DeckSO CreateCurseStubDeck()
	{
		var juOn = AssetDatabase.LoadAssetAtPath<GameObject>(JuOnPrefabPath);
		Assert.IsNotNull(juOn, "JU_ON curse token prefab missing: " + JuOnPrefabPath);
		return CreateDeckSO(new List<GameObject> { juOn, juOn });
	}

	/// <summary>
	/// Mirrors the production wiring of GameScene.unity's GameEventStorage: the curse type id
	/// gates the onEnemyCurseCardRevealed broadcast, so it must be set or the lethal deck's
	/// relic (RELIC_CURSE_REVIVAL) can never fire. Must run BEFORE GatherDecks.
	/// </summary>
	private void EnableProductionTriggerWiring()
	{
		Assert.IsNotNull(GameEventStorage.curseCardTypeID, "fixture must expose a curseCardTypeID StringSO");
		GameEventStorage.curseCardTypeID.value = "JU_ON";
	}

	[Test]
	public void LethalInfiniteDeck_KillsStubOpponent()
	{
		CombatManager.playerDeck = LoadSampleDeck("lethal infinite test");
		CombatManager.enemyDeck = CreateCurseStubDeck();
		CombatManager.startCardPrefab = LoadStartCardPrefab();
		EnableProductionTriggerWiring();
		CombatManager.GatherDecks();
		BridgeAllCards();

		CombatManager.ownerPlayerStatusRef.hp = 30;
		CombatManager.enemyPlayerStatusRef.hp = 30;
		// Isolate the kill from overtime fatigue: the loop must lethal on its own.
		CombatManager.overtimeRoundThreshold = 999;

		// Production caps: the kill is expected to happen well inside them.
		var guard = CreateGuard(200, 1500, 60);
		int iterations;
		try
		{
			iterations = RunRevealDriver(guard, MaxIterations);
		}
		catch (System.Exception ex)
		{
			Assert.Fail("lethal driver crashed: " + ex);
			return;
		}

		Assert.Less(iterations, MaxIterations, "driver must terminate within the bound");
		Assert.GreaterOrEqual(CombatManager.enemyPlayerStatusRef.hp, 0, "hp is clamped at zero");
		Assert.LessOrEqual(CombatManager.enemyPlayerStatusRef.hp, 0,
			"DIAG reveals=" + CombatManager.totalCardsRevealed
			+ " iters=" + iterations
			+ " revivedO=" + ValueTrackerManager.ownerRevivedCountRef.value
			+ " revivedE=" + ValueTrackerManager.enemyRevivedCountRef.value
			+ " stagedO=" + (ValueTrackerManager.stagedOwnerRef != null ? ValueTrackerManager.stagedOwnerRef.value : -1)
			+ " deck=" + CombatManager.combinedDeckZone.Count
			+ " forceClear=" + guard.RoundForceClearActive
			+ " conclude=" + guard.ConcludeRequested
			+ " enemyHp=" + CombatManager.enemyPlayerStatusRef.hp);
	}

	[Test]
	public void NonLethalInfiniteDeck_FatigueConverges()
	{
		CombatManager.playerDeck = LoadSampleDeck("non-lethal infinite test");
		CombatManager.enemyDeck = CreateCurseStubDeck();
		CombatManager.startCardPrefab = LoadStartCardPrefab();
		EnableProductionTriggerWiring();
		CombatManager.GatherDecks();
		BridgeAllCards();

		CombatManager.ownerPlayerStatusRef.hp = 30;
		CombatManager.enemyPlayerStatusRef.hp = 30;
		// Fatigue engages fast: overtime from round 3 exercises AddFatigueCards inside the
		// real round boundary. Reveal-count fatigue uses the production scene value (40) —
		// unlike the round-clock overtime fatigue it keeps ticking inside round-boundary-
		// starved loops like this one.
		CombatManager.overtimeRoundThreshold = 2;
		CombatManager.fatigueRevealThreshold = 40;
		// Bare AddComponent instance: fatigueAmount defaults to 0, which makes
		// AddFatigueCards a no-op — pin the per-overtime-round fatigue card count.
		CombatManager.fatigueAmount = 1;
		if (CombatManager.cardToAddWhenOvertime == null)
		{
			CombatManager.cardToAddWhenOvertime = AssetDatabase.LoadAssetAtPath<GameObject>(FatiguePrefabPath);
		}
		Assert.IsNotNull(CombatManager.cardToAddWhenOvertime, "fatigue card prefab must be resolvable");

		// Production caps as backstop; fatigue convergence must beat the 60-round cap.
		var guard = CreateGuard(200, 1500, 60);
		int iterations;
		try
		{
			iterations = RunRevealDriver(guard, MaxIterations);
		}
		catch (System.Exception ex)
		{
			Assert.Fail("non-lethal driver crashed: " + ex);
			return;
		}

		Assert.Less(iterations, MaxIterations, "driver must terminate within the bound");
		// Overtime machinery must have engaged: rounds past the threshold add fatigue cards.
		Assert.Greater(CombatManager.roundNumRef.value, CombatManager.overtimeRoundThreshold,
			"rounds must have crossed the overtime threshold");
		Assert.Greater(CountFatigueCards(), 0,
			"overtime fatigue must have been added to the deck before the combat ended");
		Assert.LessOrEqual(CombatManager.enemyPlayerStatusRef.hp, 0,
			"combat must end by death, not by running out of patience"
			+ " DIAG iters=" + iterations
			+ " rounds=" + CombatManager.roundNumRef.value
			+ " reveals=" + CombatManager.totalCardsRevealed
			+ " deck=" + CombatManager.combinedDeckZone.Count
			+ " fatigueInDeck=" + CountFatigueCards()
			+ " ownerHp=" + CombatManager.ownerPlayerStatusRef.hp
			+ " enemyHp=" + CombatManager.enemyPlayerStatusRef.hp);
		Assert.IsFalse(guard.ConcludeRequested, "the 60-round global cap must stay unused — death converges first");
	}

	private int CountFatigueCards()
	{
		int count = 0;
		foreach (var card in CombatManager.combinedDeckZone)
		{
			if (card != null && card.name.Contains("Fatigue")) count++;
		}
		return count;
	}

	private DeckSO LoadSampleDeck(string assetName)
	{
		var deck = AssetDatabase.LoadAssetAtPath<DeckSO>($"{DeckFolder}/{assetName}.asset");
		Assert.IsNotNull(deck, $"sample deck missing: {DeckFolder}/{assetName}.asset");
		Assert.GreaterOrEqual(deck.deck.Count, 2, "sample deck should carry its combo cards");
		return deck;
	}

	private CombatBudgetGuard CreateGuard(int perRound, int total, int rounds)
	{
		var guard = CreateGameObject("BudgetGuard").AddComponent<CombatBudgetGuard>();
		guard.maxRevealsPerRound = perRound;
		guard.maxTotalReveals = total;
		guard.maxRounds = rounds;
		// Edit Mode never runs Awake, so the static Me stays null and
		// CombatManager.HandleNewRoundStart's NotifyRoundStart would hit nothing — this
		// guard's RoundForceClearActive would then never reset at round boundaries.
		typeof(CombatBudgetGuard)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?.SetValue(null, guard);
		return guard;
	}

	private readonly HashSet<GameObject> _bridged = new HashSet<GameObject>();

	/// <summary>
	/// Edit Mode never runs Awake/OnEnable and skips RuntimeOnly persistent UnityEvent
	/// calls (callState=2), and CardFactory.CreateLogicalCard wires only the status refs —
	/// the EffectScript/container back-references the game resolves at spawn are null.
	/// This is the CurseSummonerPrefabSmokeTests bridge (runtime-ref injection + callState
	/// flip + listener registration) with one difference: listeners are re-pointed to the
	/// fixture event matching the REAL asset they were serialized against, preserving
	/// production trigger semantics, instead of everything landing on onMeRevealed. Made
	/// idempotent per card instance so the driver can re-bridge freshly spawned cards
	/// (fatigue cards, curse tokens) right before they are triggered.
	/// </summary>
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

	/// <summary>
	/// Re-points a listener from the REAL event asset it was serialized against to the
	/// fixture's equivalent instance, so cross-card triggers keep their production semantics
	/// (the fixture builds its own GameEvent objects, so identity matching is impossible and
	/// asset-name matching is the contract). Unknown events fall back to onMeRevealed, which is
	/// the legacy bridge behavior and keeps cards outside these combos working as before.
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

	private void BridgeAllCards()
	{
		foreach (var card in CombatManager.combinedDeckZone)
		{
			BridgeCard(card);
		}
	}

	/// <summary>Flip one UnityEvent field's persistent calls from RuntimeOnly to EditorAndRuntime (instance copy only).</summary>
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

	/// <summary>
	/// Synchronous headless reveal loop over the fixture primitives, with the L0 guard
	/// wired at the same points the real CombatManager flow would hit it. Returns the
	/// iteration count at termination (conclude requested / death / deck exhausted).
	/// </summary>
	private int RunRevealDriver(CombatBudgetGuard guard, int maxIterations)
	{
		int iterations = 0;
		while (iterations++ < maxIterations)
		{
			if (guard.ConcludeRequested) break;                        // global cap terminator
			// Headless terminator is LOGIC HP, not IsDeathVisuallyLanded: this fixture wires a
			// dummy CombatInfoDisplayer, so the visual flag reads the DISPLAY layer, whose
			// enemy-HP accessor returns a queue-frozen value while hits are pending — in a tight
			// revive loop it never lands, even after the killing blow.
			if (CombatManager.enemyPlayerStatusRef.hp <= 0 || CombatManager.ownerPlayerStatusRef.hp <= 0) break;

			if (CombatManager.revealZone == null)
			{
				if (CombatManager.combinedDeckZone.Count == 0) break;
				RevealTopCard();
				// Production fires the reveal-count fatigue check per reveal inside
				// RevealNextCardCore (:1077) — the fixture primitive bypasses that path,
				// so mirror the call here (private, hence reflection).
				typeof(CombatManager)
					.GetMethod("CheckFatigueByRevealCount", BindingFlags.NonPublic | BindingFlags.Instance)
					?.Invoke(CombatManager.Me, null);
				// Fixture RevealTopCard bypasses RevealNextCardCore — notify the L0 guard manually.
				guard.NotifyReveal(CombatManager.cardsRevealedThisRound, CombatManager.totalCardsRevealed,
					CombatManager.combinedDeckZone.Count, CombatManager.roundNumRef.value);
			}

			var revealed = CombatManager.revealZone.GetComponent<CardScript>();
			bool wasStartCard = revealed != null && revealed.isStartCard;

			// Mid-combat spawns (fatigue cards, curse tokens) enter the deck unbridged —
			// bridge before triggering so their containers carry the runtime refs.
			BridgeCard(CombatManager.revealZone);

			// Production trigger split (CombatManager.RevealCards): the Start Card does NOT
			// broadcast reveal events — its shuffle container is invoked directly — while
			// normal cards trigger through the reveal-event broadcast, which the per-round
			// force-clear skips.
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
				// The Start Card's own StartCardShuffleEffect (fired by the trigger above)
				// already did the round increment, the overtime fatigue check and the deck
				// reshuffle. OnStartCardShuffleAnimationComplete is the shuffle animation's
				// onComplete in production; it applies the per-round resets.
				CombatManager.Me.OnStartCardShuffleAnimationComplete();
			}

			// Real flow closes the chain and re-arms the loop guard at every reveal-cycle
			// boundary (confirm path / start-card playback end). Without this the
			// generation guard history burns every card+effect pair on the first lap and
			// all later reveals are silently skipped.
			EffectChainManager.Me.CloseOpenedChain();
			EffectChainManager.Me.ResetGenerationGuards();
		}
		return iterations;
	}
}
