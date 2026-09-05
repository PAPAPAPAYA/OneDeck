using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Records one full run (plan §2.6): run_start / shop_visit / combat_end / run_end.
/// The current run is incrementally persisted to current_run.jsonl (one full-run
/// snapshot per line, newest last) so a crash only ever loses the current state;
/// an unfinished record found at next start is re-uploaded as "abandoned".
/// The finished run goes out through the outbox (RunRecord, idempotent by runId).
/// Runs with zero completed combats (quit before the first fight) are never uploaded.
/// </summary>
public static class RunRecorder
{
	public const string ResultVictory = "victory";
	public const string ResultDefeat = "defeat";
	public const string ResultAbandoned = "abandoned";

	/// <summary>Test seam: when set, overrides the persistentDataPath directory.</summary>
	public static string OverrideDirectoryForTests;

	private const string FileName = "current_run.jsonl";

	private static RunUploadRequest current;
	private static bool ended;

	// per-visit accumulators
	private static int goldEnter;
	private static int goldAfterPayday;
	private static int visitRerolls;
	private static List<string> visitOffered = new List<string>();
	private static List<string> visitUtilityOffered = new List<string>();
	private static List<string> visitBought = new List<string>();

	// per-combat reveal series (RunCombatEntry.series); captured live, attached at combat end
	private static List<RunCombatSample> combatSeries = new List<RunCombatSample>();

	// run-level seen pool (plan §2.6 seenPoolPct numerator)
	private static HashSet<string> seenCardTypeIDs = new HashSet<string>();

	private static string FilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, FileName); }
	}

	// ------------------------------------------------------------------ run lifecycle

	/// <summary>Scene start / ResetRun: upload any unfinished previous run, then open a fresh one.</summary>
	public static void StartRun()
	{
		RecoverUnfinishedRun();

		current = new RunUploadRequest
		{
			runId = Guid.NewGuid().ToString("N"),
			playerId = PlayerIdentity.HasIdentity ? PlayerIdentity.PlayerId : string.Empty,
			gameVersion = DeckNetworkClient.GameVersion,
			// empty result marks an unfinished run; CloseRun/Recovery always set a real one
			result = string.Empty,
			finalDeck = new List<string>(),
			shopVisits = new List<RunShopVisitEntry>(),
			combats = new List<RunCombatEntry>(),
			startedAt = DateTime.UtcNow.ToString("o")
		};
		ended = false;
		seenCardTypeIDs.Clear();
		combatSeries.Clear();
		ResetVisit();
		SaveSnapshot();
	}

	/// <summary>Run ended (defeat/victory decided at combat end). Uploads immediately.</summary>
	public static void CloseRun(string result, int finalSession, int heartsLeft, List<string> finalDeckCardTypeIDs)
	{
		if (current == null || ended) return;

		current.result = result;
		current.finalSession = finalSession;
		current.heartsLeft = heartsLeft;
		current.finalDeck = finalDeckCardTypeIDs != null ? new List<string>(finalDeckCardTypeIDs) : new List<string>();
		current.seenPoolPct = ComputeSeenPoolPct();
		current.endedAt = DateTime.UtcNow.ToString("o");
		ended = true;
		SaveSnapshot();
		UploadCurrent();
	}

	// ------------------------------------------------------------------ shop hooks (ShopManager)

	public static void OnShopEnter(int goldEnterPurse)
	{
		if (current == null || ended) return;
		ResetVisit();
		goldEnter = goldEnterPurse;
	}

	public static void OnPayday(int purseAfterPayday)
	{
		if (current == null || ended) return;
		goldAfterPayday = purseAfterPayday;
	}

	public static void OnCardOffered(string cardTypeID, bool utilityBoard)
	{
		if (current == null || ended) return;
		if (string.IsNullOrEmpty(cardTypeID)) return;
		if (utilityBoard) visitUtilityOffered.Add(cardTypeID);
		else visitOffered.Add(cardTypeID);
		seenCardTypeIDs.Add(cardTypeID);
	}

	public static void OnCardBought(string cardTypeID)
	{
		if (current == null || ended) return;
		if (string.IsNullOrEmpty(cardTypeID)) return;
		visitBought.Add(cardTypeID);
	}

	public static void OnReroll()
	{
		if (current == null || ended) return;
		visitRerolls++;
	}

	/// <summary>PhaseManager shop-exit trigger: closes and persists one shop_visit.</summary>
	public static void CloseShopVisit(int goldExitPurse, int sessionNum)
	{
		if (current == null || ended) return;

		current.shopVisits.Add(new RunShopVisitEntry
		{
			sessionNum = sessionNum,
			offered = new List<string>(visitOffered),
			utilityOffered = new List<string>(visitUtilityOffered),
			bought = new List<string>(visitBought),
			rerollCount = visitRerolls,
			seenPoolPct = ComputeSeenPoolPct(),
			goldEnter = goldEnter,
			goldAfterPayday = goldAfterPayday,
			goldExit = goldExitPurse,
			ts = DateTime.UtcNow.ToString("o")
		});
		SaveSnapshot();
		ResetVisit();
	}

	// ---------------------------------------------------------------- combat series hooks (CombatManager / PhaseManager)

	/// <summary>
	/// PhaseManager.EnteringCombatPhase trigger: fresh series boundary per combat.
	/// Combats that never close an entry (tutorial skips RecordCombatEnd) must not
	/// leak their samples into the next real combat.
	/// </summary>
	public static void OnCombatStart()
	{
		combatSeries.Clear();
	}

	/// <summary>
	/// Live capture: one sample per reveal, called from CombatManager.RevealNextCard.
	/// Skipped when ServerConfig.includeCombatSeries is off - no capture, no per-reveal deck scans.
	/// </summary>
	public static void OnCombatCardRevealed(CardScript cardRevealed)
	{
		if (current == null || ended) return;
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.includeCombatSeries) return;
		CombatManager cm = CombatManager.Me;
		if (cm == null) return;
		combatSeries.Add(BuildSample(cm, cardRevealed));
	}

	/// <summary>Test seam: capture one prebuilt sample (live path derives it from CombatManager).</summary>
	public static void RecordCombatSample(RunCombatSample sample)
	{
		if (current == null || ended || sample == null) return;
		combatSeries.Add(sample);
	}

	// ------------------------------------------------------------------ combat hook (PhaseManager)

	public static void RecordCombatEnd(int sessionNum, bool won, int heartsLeft, int rounds, int opponentDeckId)
	{
		if (current == null || ended) return;

		current.combats.Add(new RunCombatEntry
		{
			sessionNum = sessionNum,
			won = won,
			heartsLeft = heartsLeft,
			rounds = rounds,
			opponentDeckId = opponentDeckId,
			perCard = HarvestPerCard(),
			series = BuildSeriesSnapshot(),
			ts = DateTime.UtcNow.ToString("o")
		});
		combatSeries.Clear();
		SaveSnapshot();
	}

	// ---------------------------------------------------------------- series internals

	private static RunCombatSample BuildSample(CombatManager cm, CardScript card)
	{
		RunCombatSample sample = new RunCombatSample
		{
			revealIndex = combatSeries.Count + 1,
			roundNum = cm.roundNumRef != null ? cm.roundNumRef.value : 0,
			ownerHP = cm.ownerPlayerStatusRef != null ? cm.ownerPlayerStatusRef.hp : 0,
			enemyHP = cm.enemyPlayerStatusRef != null ? cm.enemyPlayerStatusRef.hp : 0,
			ownerShield = cm.ownerPlayerStatusRef != null ? cm.ownerPlayerStatusRef.shield : 0,
			enemyShield = cm.enemyPlayerStatusRef != null ? cm.enemyPlayerStatusRef.shield : 0,
			ownerDeckSize = CountEffectiveDeckCards(cm, cm.ownerPlayerStatusRef),
			enemyDeckSize = CountEffectiveDeckCards(cm, cm.enemyPlayerStatusRef),
			side = RunCombatSample.SideNeutral,
			cardTypeID = card != null && !string.IsNullOrEmpty(card.cardTypeID) ? card.cardTypeID : string.Empty
		};
		if (card != null && !CombatManager.ShouldSkipEffectProcessing(card))
		{
			if (cm.ownerPlayerStatusRef != null && card.myStatusRef == cm.ownerPlayerStatusRef)
			{
				sample.side = RunCombatSample.SideOwner;
			}
			else if (cm.enemyPlayerStatusRef != null && card.myStatusRef == cm.enemyPlayerStatusRef)
			{
				sample.side = RunCombatSample.SideEnemy;
			}
		}
		return sample;
	}

	/// <summary>Deck cards of one side that actually participate in effects (neutral/Start Cards excluded).</summary>
	private static int CountEffectiveDeckCards(CombatManager cm, PlayerStatusSO sideRef)
	{
		if (cm == null || cm.combinedDeckZone == null || sideRef == null) return 0;
		int count = 0;
		foreach (GameObject card in cm.combinedDeckZone)
		{
			if (card == null) continue;
			CardScript cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (!CombatManager.ShouldSkipEffectProcessing(cardScript) && cardScript.myStatusRef == sideRef)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>Null when the capture switch is off or nothing was captured - JsonUtility omits the field.</summary>
	private static List<RunCombatSample> BuildSeriesSnapshot()
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.includeCombatSeries || combatSeries.Count == 0) return null;
		return new List<RunCombatSample>(combatSeries);
	}

	// ------------------------------------------------------------------ internals

	private static void ResetVisit()
	{
		visitOffered.Clear();
		visitUtilityOffered.Clear();
		visitBought.Clear();
		visitRerolls = 0;
		goldEnter = 0;
		goldAfterPayday = 0;
	}

	private static float ComputeSeenPoolPct()
	{
		HashSet<string> pool = new HashSet<string>();
		DeckSaver saver = DeckSaver.Me;
		if (saver != null && saver.shopPoolRef != null && saver.shopPoolRef.deck != null)
		{
			foreach (GameObject prefab in saver.shopPoolRef.deck)
			{
				if (prefab == null) continue;
				CardScript cardScript = prefab.GetComponent<CardScript>();
				if (cardScript == null) continue;
				string typeID = !string.IsNullOrEmpty(cardScript.cardTypeID) ? cardScript.cardTypeID : cardScript.name;
				if (!string.IsNullOrEmpty(typeID)) pool.Add(typeID);
			}
		}
		return pool.Count > 0 ? Mathf.Clamp01(seenCardTypeIDs.Count / (float)pool.Count) : 0f;
	}

	private static List<RunCombatPerCard> HarvestPerCard()
	{
		List<RunCombatPerCard> list = new List<RunCombatPerCard>();
		if (CombatPerCardStatsTracker.Me == null) return list;
		foreach (PerCardStatRecord row in CombatPerCardStatsTracker.Me.GetSessionRows())
		{
			if (row == null || row.faction != CardFaction.Player) continue;
			list.Add(new RunCombatPerCard
			{
				cardTypeID = row.cardTypeID,
				triggers = Mathf.RoundToInt(row.GetValue(CombatStatType.TriggerCount)),
				damageToOpponent = Mathf.RoundToInt(row.GetValue(CombatStatType.DamageDealtToOpponent)),
				damageToSelf = Mathf.RoundToInt(row.GetValue(CombatStatType.DamageDealtToSelf))
			});
		}
		return list;
	}

	// ------------------------------------------------------------------ persistence & upload

	private static void SaveSnapshot()
	{
		try
		{
			File.AppendAllText(FilePath, JsonUtility.ToJson(current) + "\n", new UTF8Encoding(false));
		}
		catch (IOException e)
		{
			Debug.LogWarning("[RunRecorder] snapshot save failed: " + e.Message);
		}
	}

	/// <summary>Last parseable line wins; a torn tail line from a crash is skipped.</summary>
	private static RunUploadRequest ReadLastSnapshot()
	{
		try
		{
			if (!File.Exists(FilePath)) return null;
			string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
			for (int i = lines.Length - 1; i >= 0; i--)
			{
				string line = lines[i].Trim();
				if (line.Length == 0) continue;
				try
				{
					RunUploadRequest parsed = JsonUtility.FromJson<RunUploadRequest>(line);
					if (parsed != null && !string.IsNullOrEmpty(parsed.runId)) return parsed;
				}
				catch (ArgumentException)
				{
					// torn tail line from a crash - fall through to the previous snapshot
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[RunRecorder] journal unreadable, ignoring: " + e.Message);
		}
		return null;
	}

	private static void RecoverUnfinishedRun()
	{
		RunUploadRequest last = ReadLastSnapshot();
		try
		{
			if (File.Exists(FilePath)) File.Delete(FilePath);
		}
		catch (IOException e)
		{
			Debug.LogWarning("[RunRecorder] journal delete failed: " + e.Message);
		}
		if (last == null) return;

		// Empty result = unfinished (crash / quit mid-run): mark abandoned. A finished run
		// that never got uploaded (crash between save and enqueue) uploads as-is.
		if (string.IsNullOrEmpty(last.result)) last.result = ResultAbandoned;
		if (string.IsNullOrEmpty(last.playerId) && PlayerIdentity.HasIdentity) last.playerId = PlayerIdentity.PlayerId;
		if (string.IsNullOrEmpty(last.playerId)) return;  // no identity yet - nothing to attach it to
		if (string.IsNullOrEmpty(last.gameVersion)) last.gameVersion = DeckNetworkClient.GameVersion;
		// Zero-combat runs (quit before the first completed combat) carry no per-card
		// data - never upload them. The journal file is already gone at this point.
		if (last.combats == null || last.combats.Count == 0) return;

		UploadOutbox.Enqueue(NetUploadKind.RunRecord, last);
		UploadOutbox.Flush();
	}

	private static void UploadCurrent()
	{
		if (string.IsNullOrEmpty(current.playerId) && PlayerIdentity.HasIdentity)
		{
			current.playerId = PlayerIdentity.PlayerId;
		}
		if (string.IsNullOrEmpty(current.playerId)) return;  // keep journal; recovery uploads later
		// Defensive: CloseRun only fires after a completed combat decided the run, but a
		// zero-combat record must never reach the server even if a future caller changes that.
		if (current.combats == null || current.combats.Count == 0) return;

		UploadOutbox.Enqueue(NetUploadKind.RunRecord, current);
		UploadOutbox.Flush();
	}

	// ------------------------------------------------------------------ test seams

	/// <summary>Test seam: drops all in-memory run state (files are handled by the test).</summary>
	public static void ResetForTests()
	{
		current = null;
		ended = false;
		ResetVisit();
		combatSeries.Clear();
		seenCardTypeIDs.Clear();
	}
}
