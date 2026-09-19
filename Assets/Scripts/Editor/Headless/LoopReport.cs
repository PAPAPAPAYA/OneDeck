using System.Collections.Generic;
using System.Text;
using DefaultNamespace;
using UnityEngine;

/// <summary>
/// §4 chain roles. The classification is a HEURISTIC over the card's own bindings (see
/// ComboRoleClassifier) — it is recorded next to the raw evidence so it can be falsified, and it
/// is NOT treated as authoritative: the combo library keys on the card set, not on the roles.
/// </summary>
public enum ComboRole
{
	Unknown,
	/// <summary>Moves cards back into the deck (bury / stage / revive), closing the loop.</summary>
	Engine,
	/// <summary>Reacts to the same trigger by attacking, which re-raises the trigger.</summary>
	ChainSwitcher,
	/// <summary>Refuels itself (self-revive), so the loop never runs out of material.</summary>
	Pump,
}

/// <summary>
/// Combo-library entry (plan §4 schema): mySide / enemySide / roles / tripSignals / reproSeeds /
/// status. Serialized with Unity's JsonUtility so the P5 batch scan and the P3 upload can consume
/// the same shape.
/// enemySide stays empty on purpose: cross-side infinity is out of scope by ruling (§7.1) and the
/// field is only reserved so the pipeline is pair-aware from the start.
/// status lifecycle per §4: candidate -> active (after the sim gate) -> retired (a card changed).
/// </summary>
[System.Serializable]
public class LoopReport
{
	public string responsibility = "None";
	public string ownerDeck = "";
	public string enemyDeck = "";

	/// <summary>Card type ids of the minimized responsible set (creator-relative side).</summary>
	public string[] mySide = new string[0];
	/// <summary>Reserved for the future cross-side work (§7.1). Always empty today.</summary>
	public string[] enemySide = new string[0];

	/// <summary>One entry per mySide card: raw binding evidence, e.g. "X [OnFriendlyCardBuried -> BuryNextXCards]".</summary>
	public string[] roles = new string[0];

	public string tripSignal = "";
	public int[] reproSeeds = new int[0];

	public bool oneMinimal;
	public bool truncated;

	public string status = "candidate";

	public string ToJson()
	{
		return JsonUtility.ToJson(this, true);
	}

	public string Summary()
	{
		var sb = new StringBuilder();
		sb.Append("loop-report[").Append(responsibility).Append("] ").Append(ownerDeck).Append(" vs ").Append(enemyDeck);
		sb.Append(" mySide=").Append(string.Join(",", mySide));
		sb.Append(" ").Append(tripSignal);
		sb.Append(" seeds=").Append(string.Join(",", System.Array.ConvertAll(reproSeeds, i => i.ToString())));
		sb.Append(" oneMinimal=").Append(oneMinimal);
		if (truncated) sb.Append(" TRUNCATED(not a proven minimum)");
		return sb.ToString();
	}
}

/// <summary>
/// Builds a LoopReport from an attribution verdict plus the ddmin result. The per-card evidence
/// comes from the prefab's own listener bindings (GameEventListener.@event + the UnityEvent's
/// persistent method names), i.e. from configuration, not from a hand-written table — the plan
/// insists the library be sim-driven because card descriptions lie (§4).
/// </summary>
public static class LoopReportBuilder
{
	public static LoopReport Build(InfinityAttributionResult attribution,
		MinimizeResult minimized, IList<int> seeds, BudgetTripReport tripEvidence)
	{
		var report = new LoopReport();
		report.responsibility = attribution != null ? attribution.Responsibility.ToString() : "None";
		report.ownerDeck = attribution != null ? attribution.OwnerDeckName : "";
		report.enemyDeck = attribution != null ? attribution.EnemyDeckName : "";

		var ids = new List<string>();
		var roles = new List<string>();
		if (minimized != null)
		{
			foreach (var card in minimized.Cards)
			{
				var cs = card != null ? card.GetComponent<CardScript>() : null;
				string id = cs != null ? cs.cardTypeID : "?";
				ids.Add(id);
				roles.Add(DescribeCard(card, id));
			}
			report.oneMinimal = minimized.IsOneMinimal;
			report.truncated = minimized.Truncated;
		}

		report.mySide = ids.ToArray();
		report.roles = roles.ToArray();
		report.enemySide = new string[0];

		if (tripEvidence != null)
		{
			report.tripSignal = "arrangement-cycle " + RngDigest.ToHex(tripEvidence.TripHash)
				+ " repeats=" + tripEvidence.MaxSightingsOfOneArrangement
				+ " reveals=" + tripEvidence.TotalReveals;
		}

		if (seeds != null)
		{
			var arr = new int[seeds.Count];
			for (int i = 0; i < seeds.Count; i++) arr[i] = seeds[i];
			report.reproSeeds = arr;
		}

		return report;
	}

	/// <summary>
	/// Raw evidence plus the heuristic role, e.g.
	/// "RELIC_CHAIN_BURIAL <Engine> [OnFriendlyCardBuried -> BuryNextXCards]".
	/// </summary>
	public static string DescribeCard(GameObject card, string cardTypeID)
	{
		var triggers = new List<string>();
		var methods = new List<string>();
		if (card != null)
		{
			foreach (var listener in card.GetComponentsInChildren<GameEventListener>(true))
			{
				if (listener.@event != null) triggers.Add(listener.@event.name);
				if (listener.response == null) continue;
				int count = listener.response.GetPersistentEventCount();
				for (int i = 0; i < count; i++)
				{
					string m = listener.response.GetPersistentMethodName(i);
					if (!string.IsNullOrEmpty(m)) methods.Add(m);
				}
			}
		}

		var role = ComboRoleClassifier.Classify(methods);
		return cardTypeID + " <" + role + "> [" + string.Join(",", triggers.ToArray())
			+ " -> " + string.Join(",", methods.ToArray()) + "]";
	}
}

/// <summary>
/// Heuristic role classifier over a card's effect METHOD NAMES. Deliberately narrow and explicit
/// so a wrong answer is easy to spot against the recorded evidence:
///   Pump          — any method mentions ReviveSelf (the card refuels itself from the grave).
///   Engine        — any method mentions Bury / Stage (it moves cards back into the deck) and it
///                   is not a Pump.
///   ChainSwitcher — any method mentions Attack (it re-raises the same trigger by attacking) and
///                   it is neither of the above.
///   Unknown       — everything else.
/// Checked against the §6 ring it reproduces the hand-made mapping: SOLDIER_SKELETON_4.0
/// (ReviveSelf) = Pump, RELIC_CHAIN_BURIAL (BuryNextXCards) = Engine, DEATHBED_GRANT
/// (AttackLastBuriedFriendlyCreature) = ChainSwitcher. It is still a guess for cards outside that
/// shape — hence the raw evidence in every entry.
/// </summary>
public static class ComboRoleClassifier
{
	public static ComboRole Classify(List<string> effectMethods)
	{
		if (effectMethods == null || effectMethods.Count == 0) return ComboRole.Unknown;

		bool hasReviveSelf = false, hasDeckMove = false, hasAttack = false;
		foreach (var m in effectMethods)
		{
			if (string.IsNullOrEmpty(m)) continue;
			if (m.Contains("ReviveSelf")) hasReviveSelf = true;
			if (m.Contains("Bury") || m.Contains("Stage")) hasDeckMove = true;
			if (m.Contains("Attack")) hasAttack = true;
		}

		if (hasReviveSelf) return ComboRole.Pump;
		if (hasDeckMove) return ComboRole.Engine;
		if (hasAttack) return ComboRole.ChainSwitcher;
		return ComboRole.Unknown;
	}
}
