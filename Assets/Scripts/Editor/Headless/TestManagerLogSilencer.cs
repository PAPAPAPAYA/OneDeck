using System.Reflection;
using DefaultNamespace.Managers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Zeros every TestManager log switch once a test run starts. The switches are
/// Play-mode debugging state (field defaults are all true); when they are left on,
/// test runs flood the console with combat-sim logs (~6M lines per full suite).
/// Runs are detected by polling TestJobDataHolder.TestRuns (non-empty from run
/// initialization, long before the first test body), because TestRunnerApi
/// callback registrations made outside the test-framework package proved
/// unreliable here (registered but never dispatched, 2026-10-01). Re-enable
/// switches in the Inspector when debugging a test; the next run re-zeros them.
/// </summary>
[InitializeOnLoad]
class TestManagerLogSilencer
{
	private const string JobDataHolderType = "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder, UnityEditor.TestRunner";

	private static ScriptableObject _jobDataHolder;
	private static FieldInfo _testRunsField;
	private static bool _loggedZeroingThisRun;

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
			_loggedZeroingThisRun = false;
			return;
		}

		// Pin the switches for the whole run: mid-run scene reloads (StoreSceneSetup /
		// RestoreSceneSetup) bring the on-disk state back, so zero once is not enough.
		if (PinSwitchesOff() && !_loggedZeroingThisRun)
		{
			_loggedZeroingThisRun = true;
			Debug.Log("[TestManagerLogSilencer] TestManager log switches pinned off for this run; re-enable in the Inspector for test debugging.");
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

	/// <summary>Returns true when at least one switch had to be turned off.</summary>
	private static bool PinSwitchesOff()
	{
		var managers = Object.FindObjectsByType<TestManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		if (managers.Length == 0)
		{
			return false;
		}

		bool anyWasOn = false;
		foreach (var field in typeof(TestManager).GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			if (field.FieldType != typeof(bool) || !field.Name.StartsWith("log"))
			{
				continue;
			}

			foreach (var manager in managers)
			{
				if ((bool)field.GetValue(manager))
				{
					anyWasOn = true;
					field.SetValue(manager, false);
				}
			}
		}

		return anyWasOn;
	}
}
