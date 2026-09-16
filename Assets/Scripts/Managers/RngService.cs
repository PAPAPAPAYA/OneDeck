using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// Deterministic RNG channels. Combat logic reads from these instead of UnityEngine.Random
/// so a fixed seed reproduces a combat exactly; UnityEngine.Random stays visual-only
/// (deck jitter / floater jitter / animation stagger) and can never consume from a logic
/// stream. Plan: plans/plan-deterministic-rng-seed-2026-09-12.md.
/// </summary>
public enum RngChannel
{
	Deck = 0,   // combat deck order (StartCardShuffleEffect shuffle + Start Card placement)
	Target = 1, // effect target picks (ShuffleList default + scattered target rolls)
	Shop = 2,   // shop board generation / half-price discount rolls
	Setup = 3,  // out-of-combat picks: starting card / combat reward / default enemy pool
}

/// <summary>
/// Seeded RNG service: one master seed, named sub-streams.
/// Deck/Target/Shop re-seed at every combat start from the combat seed; Setup seeds from the
/// run seed (stable within a run, re-rolls on Rng.NewRun). Streams are separate System.Random
/// instances, so extra consumption in one channel never shifts another channel's sequence.
/// </summary>
public static class Rng
{
	private static readonly System.Random[] _channels = new System.Random[4];
	private static bool _warnedUninitialized;

	/// <summary>Seed of the current run. 0 = not generated yet (auto-generates lazily).</summary>
	public static int RunSeed { get; private set; }

	/// <summary>Seed the combat channels were last initialized with.</summary>
	public static int CombatSeed { get; private set; }

	/// <summary>
	/// Start a new run: fresh run seed + Setup channel re-seed. Called by PhaseManager.ResetRun;
	/// the first combat of a session also lands here lazily via ComputeCombatSeed.
	/// </summary>
	public static void NewRun()
	{
		RunSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
		if (RunSeed == 0) RunSeed = 1; // 0 is the "unset" sentinel
		SeedChannel(RngChannel.Setup, DeriveSubSeed(RunSeed, (int)RngChannel.Setup));
	}

	/// <summary>
	/// Combat seed for a session: hash(runSeed, sessionNumber). Lazily generates the run seed
	/// on first use so a fresh scene start without ResetRun still yields a stable per-session seed.
	/// </summary>
	public static int ComputeCombatSeed(int sessionNumber)
	{
		if (RunSeed == 0) NewRun();
		return StableHash(RunSeed, sessionNumber);
	}

	/// <summary>Re-seed Deck/Target/Shop from the combat seed (called at combat start).</summary>
	public static void InitCombat(int combatSeed)
	{
		CombatSeed = combatSeed;
		SeedChannel(RngChannel.Deck, DeriveSubSeed(combatSeed, (int)RngChannel.Deck));
		SeedChannel(RngChannel.Target, DeriveSubSeed(combatSeed, (int)RngChannel.Target));
		SeedChannel(RngChannel.Shop, DeriveSubSeed(combatSeed, (int)RngChannel.Shop));
	}

	private static void SeedChannel(RngChannel channel, int seed)
	{
		_channels[(int)channel] = new System.Random(seed);
	}

	/// <summary>
	/// The System.Random instance of a channel. Cold-start path (logic before any explicit init,
	/// e.g. the first shop board of a fresh scene) derives everything from a fresh run seed:
	/// Setup seeds inside NewRun, combat channels seed from ComputeCombatSeed(0) — session 0 is
	/// the pre-first-combat window and the first GatherDecks later re-seeds the same value, so
	/// cold-start consumption stays reproducible. The warning only flags unreachable fallbacks.
	/// </summary>
	public static System.Random Channel(RngChannel channel)
	{
		int i = (int)channel;
		if (_channels[i] == null)
		{
			if (RunSeed == 0) NewRun();
			if (_channels[i] == null) InitCombat(ComputeCombatSeed(0));
			if (_channels[i] == null)
			{
				// Unreachable in practice; guard so the getter can never return null.
				SeedChannel(channel, Environment.TickCount * 7919 + i);
				if (!_warnedUninitialized)
				{
					_warnedUninitialized = true;
					Debug.LogWarning("[Rng] Channel " + channel + " could not be seeded from the run seed — fell back to auto-seed (not reproducible).");
				}
			}
		}
		return _channels[i];
	}

	/// <summary>In-place Fisher-Yates; returns the same list instance.</summary>
	public static List<T> Shuffle<T>(RngChannel channel, List<T> list)
	{
		var rng = Channel(channel);
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
		return list;
	}

	/// <summary>Uniform int in [0, maxExclusive). Drop-in for UnityEngine.Random.Range(0, n) in logic paths.</summary>
	public static int Next(RngChannel channel, int maxExclusive)
	{
		return Channel(channel).Next(maxExclusive);
	}

	/// <summary>Uniform float in [0,1). Replacement for Random.value in logic paths.</summary>
	public static float NextFloat(RngChannel channel)
	{
		return (float)Channel(channel).NextDouble();
	}

	/// <summary>Gaussian (Box-Muller) around mean; channel-scoped.</summary>
	public static float Gaussian(RngChannel channel, float mean, float stdDev)
	{
		float u1 = 1f - NextFloat(channel);
		float u2 = 1f - NextFloat(channel);
		float randStdNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
		return mean + stdDev * randStdNormal;
	}

	/// <summary>Order-sensitive 32-bit combine of two ints (FNV-1a over their bytes).</summary>
	public static int StableHash(int a, int b)
	{
		uint h = RngDigest.Fnv1a(RngDigest.Fnv1a(RngDigest.OffsetBasis, a), b);
		return (int)h;
	}

	/// <summary>SplitMix32 finalizer: spreads small seeds across sub-seeds.</summary>
	private static int DeriveSubSeed(int seed, int channelIndex)
	{
		uint z = (uint)seed ^ ((uint)channelIndex * 0x9E3779B9u + 0x9E3779B9u);
		z = (z ^ (z >> 16)) * 0x21F0AAADu;
		z = (z ^ (z >> 15)) * 0x735A2D97u;
		z = z ^ (z >> 15);
		return (int)z;
	}
}

/// <summary>FNV-1a 32-bit hashing used by the determinism digest.</summary>
public static class RngDigest
{
	public const uint OffsetBasis = 2166136261u;

	public static uint Fnv1a(uint hash, int value)
	{
		unchecked
		{
			hash = (hash ^ (byte)value) * 16777619u;
			hash = (hash ^ (byte)(value >> 8)) * 16777619u;
			hash = (hash ^ (byte)(value >> 16)) * 16777619u;
			hash = (hash ^ (byte)(value >> 24)) * 16777619u;
			return hash;
		}
	}

	public static uint Fnv1a(uint hash, string text)
	{
		if (text == null) text = string.Empty;
		unchecked
		{
			foreach (char c in text)
			{
				hash = (hash ^ (byte)(c & 0xFF)) * 16777619u;
				hash = (hash ^ (byte)((c >> 8) & 0xFF)) * 16777619u;
			}
			return hash;
		}
	}

	public static string ToHex(uint hash)
	{
		return hash.ToString("x8");
	}
}

/// <summary>
/// Per-combat determinism ledger: writes the seed alongside the CombatLogs CSVs at combat start
/// and folds the reveal sequence + per-card damage into a digest written at combat end.
/// Same seed + same decks must produce byte-identical files; any logic-path RNG leak (or
/// listener-order change) shows up as a digest mismatch.
/// </summary>
public static class DeterminismTracker
{
	private static int _combatSeed;
	private static int _sessionNumber;
	private static uint _revealHash;
	private static int _revealCount;
	private static bool _active;

	/// <summary>Called at combat start (CombatManager.GatherDecks), after Rng.InitCombat.</summary>
	public static void BeginCombat(int combatSeed, int sessionNumber, string playerDeckName, string enemyDeckName)
	{
		_combatSeed = combatSeed;
		_sessionNumber = sessionNumber;
		_revealHash = RngDigest.OffsetBasis;
		_revealCount = 0;
		_active = true;

		string content =
			"seed=" + combatSeed + "\r\n" +
			"session=" + sessionNumber + "\r\n" +
			"version=" + Application.version + "\r\n" +
			"playerDeck=" + (playerDeckName ?? "null") + "\r\n" +
			"enemyDeck=" + (enemyDeckName ?? "null");
		WriteFile("Seed_Session" + sessionNumber + "_" + Timestamp() + ".txt", content);
	}

	/// <summary>Fold one reveal into the digest; called from RevealNextCardCore.</summary>
	public static void RecordReveal(string cardTypeID, bool isOwner)
	{
		if (!_active) return;
		_revealHash = RngDigest.Fnv1a(_revealHash, cardTypeID ?? string.Empty);
		_revealHash = RngDigest.Fnv1a(_revealHash, isOwner ? 1 : 0);
		_revealCount++;
	}

	/// <summary>Called when combatFinished is set. Writes the digest file and closes the ledger.</summary>
	public static void EndCombat()
	{
		if (!_active) return;
		_active = false;

		uint damageHash = RngDigest.OffsetBasis;
		var tracker = CombatPerCardStatsTracker.Me;
		if (tracker != null)
		{
			var rows = tracker.GetSessionRows();
			if (rows != null)
			{
				// Sort rows so dictionary iteration order can never leak into the digest.
				var sorted = new List<PerCardStatRecord>(rows);
				sorted.Sort((a, b) =>
				{
					int byType = string.CompareOrdinal(a.cardTypeID, b.cardTypeID);
					return byType != 0 ? byType : ((int)a.faction).CompareTo((int)b.faction);
				});
				foreach (var row in sorted)
				{
					damageHash = RngDigest.Fnv1a(damageHash, row.cardTypeID ?? string.Empty);
					damageHash = RngDigest.Fnv1a(damageHash, (int)row.faction);
					damageHash = RngDigest.Fnv1a(damageHash, Mathf.RoundToInt(row.GetValue(CombatStatType.DamageDealtToOpponent)));
					damageHash = RngDigest.Fnv1a(damageHash, Mathf.RoundToInt(row.GetValue(CombatStatType.DamageDealtToSelf)));
				}
			}
		}

		string content =
			"seed=" + _combatSeed + "\r\n" +
			"session=" + _sessionNumber + "\r\n" +
			"version=" + Application.version + "\r\n" +
			"reveals=" + _revealCount + "\r\n" +
			"revealDigest=" + RngDigest.ToHex(_revealHash) + "\r\n" +
			"damageDigest=" + RngDigest.ToHex(damageHash);
		WriteFile("DeterminismDigest_Session" + _sessionNumber + "_" + Timestamp() + ".txt", content);
	}

	private static string Timestamp()
	{
		return DateTime.Now.ToString("yyyyMMdd_HHmmss");
	}

	private static void WriteFile(string fileName, string content)
	{
		// Play mode only: EditMode tests call GatherDecks constantly and must not spam files.
		if (!Application.isPlaying) return;
		try
		{
			// Same folder as the CombatStatsLogger CSVs (project root in editor, next to <game>_Data in builds).
			string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CombatLogs");
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(Path.Combine(dir, fileName), content + "\r\n", Encoding.UTF8);
		}
		catch (Exception ex)
		{
			TestManager.LogWarning("[Seed] Failed to write determinism file '" + fileName + "': " + ex.Message);
		}
	}
}
