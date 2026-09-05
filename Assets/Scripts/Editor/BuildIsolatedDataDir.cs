using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds a standalone Windows player under a temporarily modified productName so its
/// persistentDataPath (LocalLow/<company>/<product>) is isolated from the editor's.
/// Local async-PvP dual-end testing needs this: editor Play Mode and a same-name build
/// would otherwise share one player_identity.json and end up as the same account.
/// The productName is restored in finally, so ProjectSettings.asset (and therefore the
/// editor end's data directory) is never left changed. Menu: Tools/Build/Windows
/// Standalone (Isolated Data Dir) - outputs to Builds/IsolatedB/<productName>-B.exe.
/// </summary>
public static class BuildIsolatedDataDir
{
	private const string ProductNameSuffix = "-B";
	private const string OutputDir = "Builds/IsolatedB";

	[MenuItem("Tools/Build/Windows Standalone (Isolated Data Dir)")]
	public static void BuildWindowsIsolated()
	{
		string originalName = PlayerSettings.productName;
		PlayerSettings.productName = originalName + ProductNameSuffix;
		try
		{
			BuildPlayerOptions options = new BuildPlayerOptions
			{
				scenes = EnabledScenePaths(),
				target = BuildTarget.StandaloneWindows64,
				locationPathName = Path.Combine(OutputDir, PlayerSettings.productName + ".exe"),
			};
			BuildReport report = BuildPipeline.BuildPlayer(options);
			if (report.summary.result == BuildResult.Succeeded)
			{
				Debug.Log("[BuildIsolatedDataDir] built " + options.locationPathName
					+ " (persistentDataPath ends in \"" + PlayerSettings.productName + "\")");
			}
			else
			{
				Debug.LogError("[BuildIsolatedDataDir] build failed: " + report.summary.result
					+ ", errors: " + report.summary.totalErrors);
			}
		}
		finally
		{
			PlayerSettings.productName = originalName;
			AssetDatabase.SaveAssets();
		}
	}

	private static string[] EnabledScenePaths()
	{
		List<string> scenes = new List<string>();
		foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
		{
			if (scene.enabled) scenes.Add(scene.path);
		}
		return scenes.ToArray();
	}
}
