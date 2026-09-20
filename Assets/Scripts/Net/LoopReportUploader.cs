using UnityEngine;

/// <summary>
/// Evidence upload for the infinity gate (plan §20.3, plans/plan-infinity-detection-2026-09-17.md).
/// Takes an already-attributed verdict and queues it for POST /api/loop-reports. Enqueue-only on
/// purpose: flushing rides the existing outbox triggers (game start / shop exit / combat entry),
/// so this stays callable from contexts where no coroutine can run (an editor-side attribution
/// pass runs in edit mode, where StartCoroutine is not available).
///
/// Runtime assembly deliberately: capturing evidence has to work in a shipped build. The
/// attribution itself cannot run there (RunBudgetSim needs SerializedObject + AssetDatabase,
/// plan §19.5), so a build files the same request shape with an empty verdict — evidence only —
/// and the confirmation happens offline. The server flags on verdict "EnemyDeck" alone.
/// </summary>
public static class LoopReportUploader
{
	/// <summary>Verdict meaning "the headless re-run proved the ghost deck loops by itself" — the server's flag trigger.</summary>
	public const string VerdictEnemyDeck = "EnemyDeck";

	/// <summary>
	/// Queue one report. False when nothing was queued: no server deck row behind the enemy
	/// (0 = local default pool), server disabled, switch off, or no player identity yet.
	/// Never throws — a failed evidence upload must not disturb the game.
	/// </summary>
	public static bool EnqueueEvidence(int opponentDeckId, string verdict, int combatSeed,
		string signals, string payloadJson)
	{
		if (opponentDeckId <= 0) return false;
		if (!UploadOutbox.IsUploadEnabled(NetUploadKind.LoopReport)) return false;
		if (!PlayerIdentity.HasIdentity) return false;

		UploadOutbox.Enqueue(NetUploadKind.LoopReport, new LoopReportUploadRequest
		{
			playerId = PlayerIdentity.PlayerId,
			gameVersion = DeckNetworkClient.GameVersion,
			opponentDeckId = opponentDeckId,
			verdict = verdict != null ? verdict : string.Empty,
			seed = combatSeed,
			signals = signals != null ? signals : string.Empty,
			payload = payloadJson != null ? payloadJson : string.Empty,
		});
		return true;
	}
}
