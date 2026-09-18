using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DefaultNamespace.Managers;

/// <summary>
/// Pre-build configuration gate. Verifies that test toggles, server environment and
/// deck reference assets are in their release-expected state before a player build.
/// Manual run: menu OneDeck > Build Config Check. Automatic run: IPreprocessBuildWithReport
/// hook aborts the build on any ERROR-level failure (bypass: ONEDECK_SKIP_CONFIG_CHECK=1).
///
/// Scene values (TestManager / DeckTester) are read from the in-memory loaded scene —
/// the same state Unity packages once the scene is saved. An unsaved dirty scene is
/// surfaced as WARN because the build uses the last saved state on disk.
/// </summary>
public static class BuildConfigValidator
{
	public enum Severity
	{
		Error,
		Warn
	}

	public class CheckResult
	{
		public Severity severity;
		public string label;
		public string detail;
		public bool passed;
		public UnityEngine.Object context;
	}

	public const string GameScenePath = "Assets/Scenes/GameScene.unity";
	public const string ServerConfigPath = "Assets/Resources/ServerConfig.asset";
	public const string PlayerDeckRefPath = "Assets/SORefs/PlayerRefs/PlayerDeckRef.asset";
	public const string PlayerDefaultDeckRefPath = "Assets/SORefs/PlayerRefs/PlayerDefaultDeckRef.asset";
	public const string SkipEnvVar = "ONEDECK_SKIP_CONFIG_CHECK";

	/// <summary>
	/// Runs every check and returns the full result list (passing and failing).
	/// </summary>
	public static List<CheckResult> RunAllChecks()
	{
		List<CheckResult> results = new List<CheckResult>();
		CheckSceneToggles(results);
		CheckServerConfigAsset(results);
		CheckPlayerDeckRefAsset(results);
		return results;
	}

	/// <summary>
	/// Filters the ERROR-severity failures, for the build hook.
	/// </summary>
	public static List<CheckResult> GetErrors(List<CheckResult> results)
	{
		List<CheckResult> errors = new List<CheckResult>();
		foreach (CheckResult result in results)
		{
			if (!result.passed && result.severity == Severity.Error)
			{
				errors.Add(result);
			}
		}
		return errors;
	}

	#region Scene toggles (TestManager / DeckTester)

	private static void CheckSceneToggles(List<CheckResult> results)
	{
		Scene gameScene = FindLoadedScene(GameScenePath);
		bool openedHere = false;
		if (!gameScene.IsValid())
		{
			gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
			openedHere = true;
		}

		try
		{
			TestManager testManager = UnityEngine.Object.FindFirstObjectByType<TestManager>(FindObjectsInactive.Include);
			if (testManager == null)
			{
				Add(results, Severity.Error, "TestManager (GameScene)", "component not found in " + GameScenePath, false, null);
				return;
			}

			CheckBool(results, testManager.overrideShuffleOrder, false, "TestManager.overrideShuffleOrder", testManager);
			CheckBool(results, testManager.useTestEnemyDeck, false, "TestManager.useTestEnemyDeck", testManager);
			CheckBool(results, testManager.includeSelfOpponentDecks, false, "TestManager.includeSelfOpponentDecks", testManager);
			CheckBool(results, testManager.fightOwnGhostsOnly, false, "TestManager.fightOwnGhostsOnly", testManager);
			CheckInt(results, testManager.overrideCombatSeed, 0, "TestManager.overrideCombatSeed", testManager);
			CheckBool(results, testManager.autoReveal, true, "TestManager.autoReveal", testManager);
			CheckBool(results, testManager.recordShopStats, true, "TestManager.recordShopStats", testManager);
			CheckBool(results, testManager.recordWinRate, true, "TestManager.recordWinRate", testManager);
			CheckBool(results, testManager.recordCombatCSV, true, "TestManager.recordCombatCSV", testManager);
			CheckBool(results, testManager.uploadServerData, true, "TestManager.uploadServerData", testManager);
			CheckLogSwitches(results, testManager);

			DeckTester deckTester = UnityEngine.Object.FindFirstObjectByType<DeckTester>(FindObjectsInactive.Include);
			if (deckTester == null)
			{
				Add(results, Severity.Error, "DeckTester (GameScene)", "component not found in " + GameScenePath, false, null);
			}
			else
			{
				CheckBool(results, deckTester.autoSpace, false, "DeckTester.autoSpace", deckTester);
			}

			if (gameScene.isDirty)
			{
				Add(results, Severity.Warn, "GameScene has unsaved changes",
					"the build packages the last saved state on disk - save the scene before building", false, null);
			}
		}
		finally
		{
			if (openedHere && gameScene.IsValid())
			{
				EditorSceneManager.CloseScene(gameScene, true);
			}
		}
	}

	private static void CheckLogSwitches(List<CheckResult> results, TestManager testManager)
	{
		List<string> onSwitches = new List<string>();
		if (testManager.logCombatFlow) onSwitches.Add("logCombatFlow");
		if (testManager.logEffectChains) onSwitches.Add("logEffectChains");
		if (testManager.logAnimationPlayback) onSwitches.Add("logAnimationPlayback");
		if (testManager.logVisualSync) onSwitches.Add("logVisualSync");
		if (testManager.logEditorTools) onSwitches.Add("logEditorTools");
		if (testManager.logTestManager) onSwitches.Add("logTestManager");
		if (testManager.logDynamicDamageDisplay) onSwitches.Add("logDynamicDamageDisplay");
		if (testManager.logStatusEffectDisplay) onSwitches.Add("logStatusEffectDisplay");
		if (testManager.logDamageFloater) onSwitches.Add("logDamageFloater");
		if (testManager.logShopFlow) onSwitches.Add("logShopFlow");
		if (testManager.logUncategorized) onSwitches.Add("logUncategorized");

		string detail = onSwitches.Count == 0
			? "all OFF (expected for release)"
			: onSwitches.Count + " ON (expected OFF): " + string.Join(", ", onSwitches.ToArray());
		Add(results, Severity.Warn, "TestManager log switches", detail, onSwitches.Count == 0, testManager);
	}

	#endregion

	#region Asset checks

	private static void CheckServerConfigAsset(List<CheckResult> results)
	{
		ServerConfig config = AssetDatabase.LoadAssetAtPath<ServerConfig>(ServerConfigPath);
		if (config == null)
		{
			Add(results, Severity.Error, "ServerConfig.asset", "missing at " + ServerConfigPath
				+ " (runtime silently falls back to a disabled in-memory default)", false, null);
			return;
		}

		CheckEnum(results, config.environment, ServerEnvironment.Production, "ServerConfig.environment", config);
		CheckBool(results, config.markAsTest, false, "ServerConfig.markAsTest", config);
		CheckBool(results, config.opponentsIncludeSelf, false, "ServerConfig.opponentsIncludeSelf", config);
	}

	private static void CheckPlayerDeckRefAsset(List<CheckResult> results)
	{
		DeckSO playerDeckRef = AssetDatabase.LoadAssetAtPath<DeckSO>(PlayerDeckRefPath);
		if (playerDeckRef == null)
		{
			Add(results, Severity.Error, "PlayerDeckRef.asset", "missing at " + PlayerDeckRefPath, false, null);
			return;
		}

		CheckBool(results, playerDeckRef.resetOnStart, true, "PlayerDeckRef.resetOnStart", playerDeckRef);

		DeckSO expectedDefault = AssetDatabase.LoadAssetAtPath<DeckSO>(PlayerDefaultDeckRefPath);
		if (expectedDefault == null)
		{
			Add(results, Severity.Error, "PlayerDefaultDeckRef.asset", "missing at " + PlayerDefaultDeckRefPath
				+ " (cannot validate PlayerDeckRef.defaultDeck)", false, null);
			return;
		}

		CheckReference(results, playerDeckRef.defaultDeck, expectedDefault, "PlayerDeckRef.defaultDeck", playerDeckRef);
	}

	#endregion

	#region Check helpers

	private static void CheckBool(List<CheckResult> results, bool actual, bool expected, string label, UnityEngine.Object context)
	{
		Add(results, Severity.Error, label,
			"actual=" + FormatBool(actual) + ", expected=" + FormatBool(expected),
			actual == expected, context);
	}

	private static void CheckInt(List<CheckResult> results, int actual, int expected, string label, UnityEngine.Object context)
	{
		Add(results, Severity.Error, label,
			"actual=" + actual + ", expected=" + expected,
			actual == expected, context);
	}

	private static void CheckEnum(List<CheckResult> results, ServerEnvironment actual, ServerEnvironment expected, string label, UnityEngine.Object context)
	{
		Add(results, Severity.Error, label,
			"actual=" + actual + ", expected=" + expected,
			actual == expected, context);
	}

	private static void CheckReference(List<CheckResult> results, UnityEngine.Object actual, UnityEngine.Object expected, string label, UnityEngine.Object context)
	{
		Add(results, Severity.Error, label,
			"actual=" + DescribeAsset(actual) + ", expected=" + DescribeAsset(expected),
			actual == expected, context);
	}

	private static string FormatBool(bool value)
	{
		return value ? "ON (1)" : "OFF (0)";
	}

	private static string DescribeAsset(UnityEngine.Object asset)
	{
		if (asset == null) return "null";
		string path = AssetDatabase.GetAssetPath(asset);
		return string.IsNullOrEmpty(path) ? asset.name : path;
	}

	private static void Add(List<CheckResult> results, Severity severity, string label, string detail, bool passed, UnityEngine.Object context)
	{
		results.Add(new CheckResult
		{
			severity = severity,
			label = label,
			detail = detail,
			passed = passed,
			context = context
		});
	}

	private static Scene FindLoadedScene(string scenePath)
	{
		for (int i = 0; i < SceneManager.loadedSceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
			{
				return scene;
			}
		}
		return default(Scene);
	}

	#endregion
}

/// <summary>
/// OneDeck > Build Config Check. Lists every check with its actual vs expected value;
/// failing rows with a context object get a Ping button that locates the offender.
/// </summary>
public class BuildConfigValidatorWindow : EditorWindow
{
	private List<BuildConfigValidator.CheckResult> results;
	private Vector2 scrollPosition;
	private GUIStyle resultLabel;

	[MenuItem("OneDeck/Build Config Check")]
	public static void Open()
	{
		BuildConfigValidatorWindow window = GetWindow<BuildConfigValidatorWindow>("Build Config Check");
		window.minSize = new Vector2(420f, 260f);
	}

	private void OnEnable()
	{
		RunChecks();
	}

	private void OnFocus()
	{
		RunChecks();
	}

	private void RunChecks()
	{
		results = BuildConfigValidator.RunAllChecks();
		Repaint();
	}

	private void OnGUI()
	{
		if (results == null)
		{
			RunChecks();
		}

		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
		{
			RunChecks();
		}
		EditorGUILayout.EndHorizontal();

		int errors = 0;
		int warnings = 0;
		foreach (BuildConfigValidator.CheckResult result in results)
		{
			if (result.passed) continue;
			if (result.severity == BuildConfigValidator.Severity.Error) errors++;
			else warnings++;
		}

		string summary;
		if (errors == 0 && warnings == 0)
		{
			summary = "<color=#4CAF50>All " + results.Count + " checks passed.</color>";
		}
		else
		{
			summary = "<color=#F44336>" + errors + " error(s)</color>, <color=#FFC107>" + warnings + " warning(s)</color> out of " + results.Count + " checks.";
		}
		GUILayout.Label(summary, GetResultLabel());
		EditorGUILayout.Separator();

		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
		foreach (BuildConfigValidator.CheckResult result in results)
		{
			DrawResultRow(result);
		}
		EditorGUILayout.EndScrollView();
	}

	private void DrawResultRow(BuildConfigValidator.CheckResult result)
	{
		string marker = result.passed
			? "<color=#4CAF50>PASS</color>"
			: (result.severity == BuildConfigValidator.Severity.Error ? "<color=#F44336>FAIL</color>" : "<color=#FFC107>WARN</color>");
		string severityText = result.severity == BuildConfigValidator.Severity.Error ? "ERROR" : "WARN";
		string rowText = marker + "  [" + severityText + "] " + result.label + "\n    " + result.detail;

		EditorGUILayout.BeginHorizontal();
		GUILayout.Label(rowText, GetResultLabel());
		if (!result.passed && result.context != null)
		{
			if (GUILayout.Button("Ping", GUILayout.Width(48f), GUILayout.Height(30f)))
			{
				Selection.activeObject = result.context;
				EditorGUIUtility.PingObject(result.context);
			}
		}
		EditorGUILayout.EndHorizontal();
	}

	private GUIStyle GetResultLabel()
	{
		if (resultLabel == null)
		{
			resultLabel = new GUIStyle(EditorStyles.label);
			resultLabel.richText = true;
			resultLabel.wordWrap = true;
			resultLabel.margin = new RectOffset(4, 4, 4, 6);
		}
		return resultLabel;
	}
}

/// <summary>
/// Aborts the player build when any ERROR-level check fails. Bypass with
/// ONEDECK_SKIP_CONFIG_CHECK=1 (documented in BuildConfigValidator.SkipEnvVar).
/// </summary>
class BuildConfigCheckHook : IPreprocessBuildWithReport
{
	public int callbackOrder { get { return 0; } }

	public void OnPreprocessBuild(BuildReport report)
	{
		if (string.Equals(Environment.GetEnvironmentVariable(BuildConfigValidator.SkipEnvVar), "1", StringComparison.Ordinal))
		{
			Debug.LogWarning("[BuildConfigValidator] skipped via environment variable " + BuildConfigValidator.SkipEnvVar + "=1");
			return;
		}

		List<BuildConfigValidator.CheckResult> errors = BuildConfigValidator.GetErrors(BuildConfigValidator.RunAllChecks());
		if (errors.Count == 0)
		{
			Debug.Log("[BuildConfigValidator] all ERROR-level config checks passed.");
			return;
		}

		StringBuilder summary = new StringBuilder();
		summary.AppendLine("[BuildConfigValidator] build blocked by " + errors.Count + " config error(s):");
		foreach (BuildConfigValidator.CheckResult error in errors)
		{
			summary.AppendLine("  " + error.label + " - " + error.detail);
		}
		summary.Append("Open OneDeck > Build Config Check for details, or set " + BuildConfigValidator.SkipEnvVar + "=1 to bypass.");
		Debug.LogError(summary.ToString());
		throw new BuildFailedException("BuildConfigValidator: " + errors.Count + " config error(s) - see console for the full list.");
	}
}
