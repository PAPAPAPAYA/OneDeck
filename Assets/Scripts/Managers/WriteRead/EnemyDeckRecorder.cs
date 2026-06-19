using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif
using DefaultNamespace.Managers;

namespace TestWriteRead
{
	/// <summary>
	/// Manual tool that records the current player deck as a reusable DeckSO asset.
	/// Intended for designer workflows: run Play Mode, click "Record Now", then drag
	/// the generated asset into CombatManager.enemyDeck or DeckSaver.defaultEnemyDeckPool.
	/// </summary>
	public class EnemyDeckRecorder : MonoBehaviour
	{
		[Header("Output")]
		[Tooltip("Output folder relative to Assets/")]
		public string outputFolder = "SORefs/Decks/Recorded";

		[Tooltip("Filename prefix")]
		public string namingPrefix = "RecordedDeck_";

		[Tooltip("Append timestamp to filename")]
		public bool useTimestamp = true;

		[Header("Hotkey")]
		[Tooltip("Hotkey to trigger Record Now while in Play Mode")]
		public KeyCode recordHotkey = KeyCode.F12;

		private void Update()
		{
			if (!Application.isPlaying) return;
			if (Input.GetKeyDown(recordHotkey))
			{
				print("?");
				RecordDeck();
			}
		}

		/// <summary>
		/// Main record entry point. Called via Inspector context menu or hotkey.
		/// </summary>
		[ContextMenu("Record Now")]
		public void RecordDeck()
		{
			if (!Application.isPlaying)
			{
				TestManager.LogWarning("[EnemyDeckRecorder] Recording is only available in Play Mode.");
				return;
			}

			var deckSaver = DeckSaver.Me;
			if (deckSaver == null)
			{
				TestManager.LogWarning("[EnemyDeckRecorder] DeckSaver singleton is null.");
				return;
			}

			var sourceDeck = deckSaver.playerDeck;
			if (sourceDeck == null)
			{
				TestManager.LogWarning("[EnemyDeckRecorder] DeckSaver.playerDeck is null.");
				return;
			}

			if (sourceDeck.deck == null || sourceDeck.deck.Count == 0)
			{
				TestManager.LogWarning("[EnemyDeckRecorder] Player deck is empty.");
				return;
			}

			var resolvedPrefabs = new List<GameObject>();
			var missingCards = new List<string>();

			foreach (var cardPrefab in sourceDeck.deck)
			{
				if (cardPrefab == null) continue;

				var cardScript = cardPrefab.GetComponent<CardScript>();
				if (cardScript == null) continue;

				// Skip Start Card — it's auto-added by CombatManager
				if (cardScript.isStartCard) continue;

				string typeID = GetCardTypeID(cardScript);
				if (string.IsNullOrEmpty(typeID))
				{
					missingCards.Add(cardPrefab.name);
					continue;
				}

				var prefab = deckSaver.GetCardPrefabByTypeID(typeID);
				if (prefab != null)
				{
					resolvedPrefabs.Add(prefab);
				}
				else
				{
					missingCards.Add(typeID);
				}
			}

			if (missingCards.Count > 0)
			{
				TestManager.LogWarning("[EnemyDeckRecorder] Could not resolve " + missingCards.Count +
					" card(s): " + string.Join(", ", missingCards));
			}

			if (resolvedPrefabs.Count == 0)
			{
				TestManager.LogError("[EnemyDeckRecorder] No cards could be resolved. Aborting.");
				return;
			}

#if UNITY_EDITOR
			CreateDeckAsset(resolvedPrefabs, missingCards);
#else
			TestManager.Log("[EnemyDeckRecorder] Asset creation is only available in Editor.");
#endif
		}

		private string GetCardTypeID(CardScript cardScript)
		{
			if (!string.IsNullOrEmpty(cardScript.cardTypeID))
				return cardScript.cardTypeID;

			TestManager.LogWarning("[EnemyDeckRecorder] Card " + cardScript.name +
				" has no cardTypeID, falling back to GameObject name.");
			return cardScript.name;
		}

#if UNITY_EDITOR

		#region Asset Creation (Editor Only)

		private void CreateDeckAsset(List<GameObject> resolvedPrefabs, List<string> missingCards)
		{
			var deckSO = ScriptableObject.CreateInstance<DeckSO>();
			deckSO.deck = new List<GameObject>(resolvedPrefabs);
			deckSO.resetOnStart = false;
			deckSO.description = GenerateDescription(resolvedPrefabs.Count, missingCards.Count);

			string sessionFolder = GetSessionSubfolder();
			string filename = GenerateFilename();
			string assetPath = ResolveUniqueAssetPath(sessionFolder, filename);

			string fullFolder = Path.Combine(Application.dataPath, sessionFolder);
			if (!Directory.Exists(fullFolder))
				Directory.CreateDirectory(fullFolder);

			AssetDatabase.CreateAsset(deckSO, assetPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			EditorUtility.FocusProjectWindow();
			Selection.activeObject = deckSO;

			TestManager.Log("[EnemyDeckRecorder] Recorded enemy deck: " + assetPath +
				" (" + resolvedPrefabs.Count + " cards)");
		}

		private string GetSessionSubfolder()
		{
			var deckSaver = DeckSaver.Me;
			int sessionNum = deckSaver != null && deckSaver.sessionNumber != null
				? deckSaver.sessionNumber.value
				: 0;
			return Path.Combine(outputFolder, "Session" + sessionNum);
		}

		private string GenerateFilename()
		{
			var parts = new List<string> { namingPrefix };

			var deckSaver = DeckSaver.Me;
			int sessionNum = deckSaver != null && deckSaver.sessionNumber != null
				? deckSaver.sessionNumber.value
				: 0;
			parts.Add("Session" + sessionNum + "_");

			if (useTimestamp)
			{
				parts.Add(DateTime.Now.ToString("yyyyMMdd_HHmmss"));
			}

			return string.Join("", parts).TrimEnd('_');
		}

		private string GenerateDescription(int cardCount, int missingCount)
		{
			var deckSaver = DeckSaver.Me;
			int sessionNum = deckSaver != null && deckSaver.sessionNumber != null
				? deckSaver.sessionNumber.value
				: 0;

			var lines = new List<string>
			{
				"Auto-recorded player deck.",
				"Cards: " + cardCount,
				"Missing: " + missingCount,
				"Session: " + sessionNum,
				"Recorded: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
			};

			return string.Join("\n", lines);
		}

		private string ResolveUniqueAssetPath(string folder, string baseName)
		{
			string relativePath = "Assets/" + folder + "/" + baseName + ".asset";
			string fullPath = Path.Combine(Application.dataPath, folder, baseName + ".asset");

			if (!File.Exists(fullPath))
				return relativePath;

			int suffix = 1;
			while (true)
			{
				string candidateRelative = "Assets/" + folder + "/" + baseName + "_" + suffix + ".asset";
				string candidateFull = Path.Combine(Application.dataPath, folder, baseName + "_" + suffix + ".asset");
				if (!File.Exists(candidateFull))
					return candidateRelative;
				suffix++;
			}
		}

		#endregion

#endif
	}
}
