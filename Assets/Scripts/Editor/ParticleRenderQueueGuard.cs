using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps particle VFX materials above the background contour canvas (render queue 3000).
/// Mat_Wisp / Mat_Trail are hand-written URP/Lit with Surface=Opaque + Alpha Clip, whose
/// automatic queue is AlphaTest (2450). URP's material GUI recomputes the queue from the
/// blend mode on every revalidation and discards manual queue values, so these materials
/// carry _QueueOffset = 600 and revalidation lands on 2450 + 600 = 3050 (> 3000 contour
/// canvas). This guard re-stamps both values if they drift (e.g. the offset gets zeroed
/// by hand in the Inspector).
/// History: docs/RegressionChecklist.md, UI &amp; HUD section.
/// </summary>
class ParticleRenderQueueGuard : AssetPostprocessor
{
	const int RequiredQueue = 3050;
	const float RequiredQueueOffset = 600f;

	/// <summary>Protected material names (without extension).</summary>
	static readonly HashSet<string> GuardedMaterials = new HashSet<string>
	{
		"Mat_Wisp",
		"Mat_Trail",
	};

	static bool needsSave;

	[InitializeOnLoadMethod]
	static void RegisterDeferredSave()
	{
		// Saving inside OnPostprocessAllAssets can re-trigger imports; defer like URP's MaterialPostprocessor.
		EditorApplication.update += DeferredSave;
	}

	static void DeferredSave()
	{
		if (!needsSave)
		{
			return;
		}
		needsSave = false;
		AssetDatabase.SaveAssets();
	}

	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
	{
		foreach (string path in importedAssets)
		{
			if (!path.EndsWith(".mat"))
			{
				continue;
			}
			if (!GuardedMaterials.Contains(System.IO.Path.GetFileNameWithoutExtension(path)))
			{
				continue;
			}
			var material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				continue;
			}
			int oldQueue = material.renderQueue;
			float oldOffset = material.GetFloat("_QueueOffset");
			if (oldQueue == RequiredQueue && Mathf.Approximately(oldOffset, RequiredQueueOffset))
			{
				continue;
			}
			// SerializedObject writes survive URP GUI revalidation; the renderQueue setter does not.
			var so = new SerializedObject(material);
			so.FindProperty("m_CustomRenderQueue").intValue = RequiredQueue;
			so.ApplyModifiedPropertiesWithoutUndo();
			material.SetFloat("_QueueOffset", RequiredQueueOffset);
			EditorUtility.SetDirty(material);
			needsSave = true;
			Debug.LogWarning($"[ParticleRenderQueueGuard] {path}: drifted to queue={oldQueue} offset={oldOffset}; re-stamped to queue={RequiredQueue} offset={RequiredQueueOffset}. 600 is intentional, see docs/RegressionChecklist.md.", material);
		}
	}
}
