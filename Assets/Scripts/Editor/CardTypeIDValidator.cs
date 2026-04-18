using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CardTypeIDValidator
{
	[MenuItem("Tools/Validate Card Type IDs")]
	public static void Validate()
	{
		string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cards" });
		var idToPaths = new Dictionary<string, List<string>>();
		var emptyIdPaths = new List<string>();

		foreach (string guid in prefabGuids)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (prefab == null) continue;

			CardScript card = prefab.GetComponent<CardScript>();
			if (card == null) continue;

			if (string.IsNullOrWhiteSpace(card.cardTypeID))
			{
				emptyIdPaths.Add(path);
				continue;
			}

			string id = card.cardTypeID.Trim();
			if (!idToPaths.ContainsKey(id))
				idToPaths[id] = new List<string>();
			idToPaths[id].Add(path);
		}

		bool hasIssue = false;

		foreach (var kvp in idToPaths.Where(x => x.Value.Count > 1))
		{
			hasIssue = true;
			string paths = string.Join(", ", kvp.Value);
			Debug.LogWarning($"[CardTypeIDValidator] Duplicate cardTypeID \"{kvp.Key}\" found in: {paths}",
				AssetDatabase.LoadAssetAtPath<Object>(kvp.Value[0]));
		}

		foreach (string path in emptyIdPaths)
		{
			hasIssue = true;
			Debug.LogWarning($"[CardTypeIDValidator] Empty cardTypeID in: {path}",
				AssetDatabase.LoadAssetAtPath<Object>(path));
		}

		if (!hasIssue)
			Debug.Log("[CardTypeIDValidator] All cardTypeIDs are valid. No duplicates or empty IDs found.");
	}
}
