using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression for the plan's specimen #1 (plan-infinity-detection §6): the 09-13 three-card ring
/// RELIC_CHAIN_BURIAL (buried-friendly -> bury deck top) + DEATHBED_GRANT (buried-friendly ->
/// that creature attacks) + SOLDIER_SKELETON_4.0 (buried -> revive self). Before the 2026-09-17
/// same-card-different-object fix it recursed 213 chain levels deep and crashed the editor with a
/// StackOverflow, because each lap's chain switch re-opened the chain and washed the same-instance
/// loop guard and chainDepth away.
/// Measured 2026-09-19 with RunBudgetSim: the ring is DEFUSED — 67 reveals / 7 rounds against a
/// normal dummy (enemy dies), 100 reveals / 9 rounds against a 1e8-HP dummy (the owner dies),
/// cascade depth peaks at 1, and no arrangement-cycle trip either way.
/// So this is a "must terminate" case, NOT an unbounded-loop specimen: do not add it to
/// ComboMinimizer's demos, and keep the cascade-depth bound as the tripwire — the failure this
/// guards against is depth, not termination.
/// </summary>
public class InfinityRingReproducerTests
{
	private const string BurialPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/RELIC_CHAIN_BURIAL.prefab";
	private const string GrantPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/DEATHBED_GRANT.prefab";
	private const string SkeletonPath = "Assets/Prefabs/Cards/4.0/0_Common/SOLDIER_SKELETON_4.0.prefab";

	/// <summary>Pre-P0 this reached 213. Anything near that means the guard fix regressed.</summary>
	private const int CascadeDepthCeiling = 5;

	private readonly List<Object> _tempObjects = new List<Object>();

	[TearDown]
	public void TearDown()
	{
		foreach (var obj in _tempObjects)
		{
			if (obj != null) Object.DestroyImmediate(obj);
		}
		_tempObjects.Clear();
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		HeadlessCombatRig.DestroySharedResources();
	}

	[Test]
	public void Ring0913_TerminatesShallowInsteadOfOverflowing()
	{
		var deck = BuildRingDeck();
		var report = RunBudgetSim.RunVsDummy(deck, dummySize: 3, dummyHp: 30, seed: 4242);

		Debug.Log("[Ring0913] " + report.Summary);

		Assert.IsFalse(report.SuspectedInfinite,
			"the 09-13 ring must no longer be an unbounded recursion: " + report.Summary);
		Assert.IsTrue(report.EnemyDied || report.OwnerDied,
			"the ring must end by a death, not by patience: " + report.Summary);
		Assert.LessOrEqual(report.PeakCascadeDepth, CascadeDepthCeiling,
			"cascade depth must stay shallow — the pre-P0 pathology reached 213 nested levels: " + report.Summary);
		Assert.IsTrue(report.FinishedNaturally,
			"the L0 caps must not be what ends it: " + report.Summary);
	}

	[Test]
	public void Ring0913_SurvivesAnUnsinkableOpponentToo()
	{
		// Same deck against an opponent that cannot die: the loop must still not form (the ring is
		// broken, not merely out-raced by a kill).
		var deck = BuildRingDeck();
		var report = RunBudgetSim.RunVsDummy(deck, dummySize: 3, dummyHp: 100000000, seed: 4242);

		Debug.Log("[Ring0913] unsinkable: " + report.Summary);

		Assert.IsFalse(report.SuspectedInfinite,
			"no arrangement cycle may appear even when the opponent cannot die: " + report.Summary);
		Assert.LessOrEqual(report.PeakCascadeDepth, CascadeDepthCeiling,
			"cascade depth must stay shallow: " + report.Summary);
	}

	private DeckSO BuildRingDeck()
	{
		var cards = new List<GameObject>();
		foreach (var path in new[] { BurialPath, GrantPath, SkeletonPath })
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			Assert.IsNotNull(prefab, "ring card prefab missing: " + path);
			cards.Add(prefab);
		}

		var deck = ScriptableObject.CreateInstance<DeckSO>();
		_tempObjects.Add(deck);
		deck.name = "ring-0913(3 cards)";
		deck.deck = cards;
		return deck;
	}
}
