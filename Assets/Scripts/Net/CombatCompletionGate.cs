using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Lifetime upload gate (plans/plan-combat-completion-upload-gate-2026-09-06.md):
/// true once the player has completed at least one real combat. The scripted tutorial
/// exits before the PhaseManager settlement point and never counts. Upload/record
/// paths that must only carry data from players who finished a fight check
/// HasCompletedCombat. The flag is a one-way fact orthogonal to the stats reset
/// switches; a deleted file self-heals by re-arming at the next completed combat.
/// </summary>
public static class CombatCompletionGate
{
	/// <summary>Test seam: when set, overrides the persistentDataPath directory.</summary>
	public static string OverrideDirectoryForTests;

	private const string FlagFileName = "has_completed_combat.flag";

	private static bool? hasCompleted;

	private static string FlagFilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, FlagFileName); }
	}

	public static bool HasCompletedCombat
	{
		get
		{
			if (hasCompleted.HasValue) return hasCompleted.Value;
			hasCompleted = File.Exists(FlagFilePath);
			return hasCompleted.Value;
		}
	}

	/// <summary>
	/// Combat-settlement trigger (PhaseManager calls this for every completed combat,
	/// draws included): arms the flag once, then lets the deferred card-catalog upload
	/// run - idempotent per game version, so the first combat after an update also
	/// refreshes the catalog for that version.
	/// </summary>
	public static void MarkCompleted()
	{
		if (!HasCompletedCombat)
		{
			hasCompleted = true;
			try
			{
				File.WriteAllText(FlagFilePath, DateTime.UtcNow.ToString("o"), new UTF8Encoding(false));
			}
			catch (IOException e)
			{
				// The in-memory flag is already armed; a failed write just re-arms on the
				// next completed combat once the filesystem cooperates.
				Debug.LogWarning("[CombatCompletionGate] flag write failed: " + e.Message);
			}
		}
		CardCatalogUploader.MaybeUpload();
	}

	/// <summary>Test seam: drops in-memory state so the next read reloads from disk.</summary>
	public static void ResetForTests()
	{
		hasCompleted = null;
	}
}
