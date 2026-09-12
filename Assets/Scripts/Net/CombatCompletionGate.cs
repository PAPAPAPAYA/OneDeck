using UnityEngine;

/// <summary>
/// Per-run upload gate (plans/plan-combat-completion-upload-gate-2026-09-06.md §6):
/// uploads only flow once the CURRENT run has completed at least one real combat.
/// The scripted tutorial exits before the PhaseManager settlement point and never
/// counts. OnRunStarted re-closes the gate at every run start (scene start /
/// ResetRun), so the opening deck can only leave via DeckSaver's deferral (it rides
/// out with the first post-combat snapshot), never before a completed combat.
/// Pure in-memory state; draws count.
/// </summary>
public static class CombatCompletionGate
{
	private static bool completedThisRun;

	/// <summary>Run-start trigger (scene start / ResetRun): the gate closes again.</summary>
	public static void OnRunStarted()
	{
		completedThisRun = false;
	}

	/// <summary>True once the current run has seen a completed combat settlement.</summary>
	public static bool HasCompletedCombatThisRun
	{
		get { return completedThisRun; }
	}

	/// <summary>
	/// Combat-settlement trigger (PhaseManager calls this for every completed combat,
	/// draws included): opens the gate for the rest of the run, then lets the deferred
	/// card-catalog upload run - idempotent per game version, so the first settlement
	/// of a run also refreshes the catalog after an update.
	/// </summary>
	public static void MarkCompleted()
	{
		completedThisRun = true;
		CardCatalogUploader.MaybeUpload();
	}

	/// <summary>Test seam: closes the gate again (equivalent to a fresh run).</summary>
	public static void ResetForTests()
	{
		completedThisRun = false;
	}
}
