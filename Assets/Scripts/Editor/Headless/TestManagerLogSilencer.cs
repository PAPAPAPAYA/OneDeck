using System.Reflection;
using DefaultNamespace.Managers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mutes TestManager logging for the whole duration of a test run. Test bodies run
/// inside a single editor tick, so pinning per-instance log switches (the
/// 2026-10-01 approach) could never catch rig-created TestManager instances; the
/// static TestManager.TestRunSilence, checked first in TestManager.LogInternal,
/// closes that gap and the Me==null ungated fallback. Touching no instance fields
/// also removes the old "SaveScene bakes pinned switches into the scene" residue
/// trap. Run detection polls TestJobDataHolder.TestRuns (non-empty from run
/// initialization, long before the first test body; entries are removed again on
/// run completion), because TestRunnerApi callback registrations made outside the
/// test-framework package proved unreliable here (registered but never
/// dispatched, 2026-10-01). Re-enable switches in the Inspector when debugging.
/// </summary>
[InitializeOnLoad]
class TestManagerLogSilencer
{
	private const string JobDataHolderType = "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder, UnityEditor.TestRunner";

	private static ScriptableObject _jobDataHolder;
	private static FieldInfo _testRunsField;
	private static bool _loggedSilenceThisRun;

	static TestManagerLogSilencer()
	{
		EditorApplication.update -= Poll;
		EditorApplication.update += Poll;
	}

	private static void Poll()
	{
		int activeRuns = GetActiveTestRunCount();
		if (activeRuns == 0)
		{
			if (TestManager.TestRunSilence)
			{
				TestManager.TestRunSilence = false;
				Debug.Log("[TestManagerLogSilencer] Test run ended; TestManager.TestRunSilence released.");
			}
			_loggedSilenceThisRun = false;
			return;
		}

		TestManager.TestRunSilence = true;
		if (!_loggedSilenceThisRun)
		{
			_loggedSilenceThisRun = true;
			Debug.Log("[TestManagerLogSilencer] Test run active; TestManager.TestRunSilence engaged (TestManager log output muted until the run ends).");
		}
	}

	private static int GetActiveTestRunCount()
	{
		if (_testRunsField == null)
		{
			var holderType = System.Type.GetType(JobDataHolderType);
			if (holderType == null)
			{
				return 0;
			}

			var holders = Resources.FindObjectsOfTypeAll(holderType);
			if (holders == null || holders.Length == 0)
			{
				return 0;
			}

			_jobDataHolder = (ScriptableObject)holders[0];
			_testRunsField = holderType.GetField("TestRuns", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (_testRunsField == null)
			{
				return 0;
			}
		}

		var runs = _testRunsField.GetValue(_jobDataHolder) as System.Collections.ICollection;
		return runs != null ? runs.Count : 0;
	}
}
