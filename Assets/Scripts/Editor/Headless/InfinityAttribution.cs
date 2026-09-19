using UnityEngine;

/// <summary>
/// Who is responsible for an unbounded recursion (plans/plan-infinity-detection-2026-09-17.md §4).
/// The three verdicts map one-to-one onto the plan's rulings:
/// - EnemyDeck -> flag the enemy deck's combo (§7.2: the ghost stops being served; its owner may
///   keep using and uploading it).
/// - OwnerDeck -> flag NOTHING that affects serving; the player-side combo is allowed (§1.2 /
///   §9) and the combat outcome is decided by the run's own result.
/// - PairOnly -> record only. Cross-side infinity is out of scope by ruling (§7.1) and the
///   entry's enemySide field stays reserved for if that is ever revisited.
/// </summary>
public enum InfinityResponsibility
{
	None,
	EnemyDeck,
	OwnerDeck,
	PairOnly,
}

[System.Serializable]
public class InfinityAttributionResult
{
	public string OwnerDeckName = "";
	public string EnemyDeckName = "";
	public int Seed;
	public InfinityResponsibility Responsibility = InfinityResponsibility.None;

	/// <summary>Step evidence, in the order the runs happened. Null when the step was not reached.</summary>
	public BudgetTripReport EnemyVsDummy;
	public BudgetTripReport OwnerVsDummy;
	public BudgetTripReport PairReplay;

	public bool ShouldFlagEnemy
	{
		get { return Responsibility == InfinityResponsibility.EnemyDeck; }
	}

	/// <summary>Worth running the minimizer on: a one-sided loop is what the combo library stores.</summary>
	public bool HasOneSidedCombo
	{
		get
		{
			return Responsibility == InfinityResponsibility.EnemyDeck
				|| Responsibility == InfinityResponsibility.OwnerDeck;
		}
	}

	public string Explain()
	{
		switch (Responsibility)
		{
			case InfinityResponsibility.EnemyDeck:
				return "enemy deck loops alone -> flag its combo (ghost stops being served; owner unaffected)";
			case InfinityResponsibility.OwnerDeck:
				return "owner deck loops alone -> flag nothing; the player side is allowed (§1.2/§9)";
			case InfinityResponsibility.PairOnly:
				return "only the pairing loops -> record, do not flag (§7.1)";
			default:
				return "no run looped (the live trip did not reproduce)";
		}
	}

	public override string ToString()
	{
		return "attribution[" + OwnerDeckName + " vs " + EnemyDeckName + " seed=" + Seed + "] "
			+ Responsibility + " — " + Explain();
	}
}

/// <summary>
/// The plan's attribution tri-run (§4): replay the pairing headlessly and decide responsibility by
/// isolating each side against an inert dummy.
/// Order matters and follows the plan: enemy-vs-dummy first (a one-sided enemy combo is both the
/// common case and the only one that changes serving), then owner-vs-dummy, then the pair replay
/// as the zero-cost pair-aware slot.
/// The dummy carries a huge HP pool on purpose: a "lethal" combo self-terminates by KILLING the
/// opponent, so against a stub with normal HP its loop looks finite and the arrangement never
/// repeats enough to trip. Only an opponent that outlives the reveal budget exposes it as
/// unbounded — which is exactly the plan's "infinite-HP dummy" framing (§3/§15).
/// </summary>
public static class InfinityAttribution
{
	public const int DefaultDummySize = 3;

	/// <summary>
	/// 1e8 HP needs on the order of 1.4e4 reveals to chew through at the observed pump rate
	/// (damage grows roughly quadratically with laps), far past the default 1500 reveal cap — so
	/// the dummy always outlives the run and the only terminator left is the L0 cap itself.
	/// </summary>
	public const int DefaultDummyHp = 100000000;

	public static InfinityAttributionResult Attribute(DeckSO ownerDeck, DeckSO enemyDeck, int seed,
		RunBudgetSim.Options options = null, int dummySize = DefaultDummySize, int dummyHp = DefaultDummyHp)
	{
		if (options == null) options = new RunBudgetSim.Options();

		var result = new InfinityAttributionResult();
		result.OwnerDeckName = ownerDeck != null ? ownerDeck.name : "null";
		result.EnemyDeckName = enemyDeck != null ? enemyDeck.name : "null";
		result.Seed = seed;

		result.EnemyVsDummy = RunBudgetSim.RunVsDummy(enemyDeck, dummySize, dummyHp, seed, options);
		if (result.EnemyVsDummy.SuspectedInfinite)
		{
			result.Responsibility = InfinityResponsibility.EnemyDeck;
			return result;
		}

		result.OwnerVsDummy = RunBudgetSim.RunVsDummy(ownerDeck, dummySize, dummyHp, seed, options);
		if (result.OwnerVsDummy.SuspectedInfinite)
		{
			result.Responsibility = InfinityResponsibility.OwnerDeck;
			return result;
		}

		result.PairReplay = RunBudgetSim.Run(ownerDeck, enemyDeck, seed, options);
		result.Responsibility = result.PairReplay.SuspectedInfinite
			? InfinityResponsibility.PairOnly
			: InfinityResponsibility.None;
		return result;
	}
}
