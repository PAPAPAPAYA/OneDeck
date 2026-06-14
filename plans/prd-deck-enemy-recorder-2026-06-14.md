# OneDeck — Enemy Deck Recorder Product Requirements Document (PRD)

> **Version:** v2.0  
> **Date:** 2026-06-14  
> **Status:** Implemented  
> **Replaces:** `plans/prd-deck-enemy-recorder-2026-05-27.md` v1.1

---

## 1. Background

The project already has a **DeckSaver** system that persists player decks to JSON (`deckdata.json`) using `cardTypeID` strings. It also has a **DeckSO** ScriptableObject system with default enemy decks.

There is no bridge between these two systems:
- DeckSaver writes to an opaque JSON file outside version control.
- DeckSOs are manually authored and static.
- There is no automated way to **turn a proven player deck (from PlayMode) into a reusable enemy DeckSO asset**.

The Enemy Deck Recorder closes this gap as a **manual designer tool**. It sits in the scene as a MonoBehaviour with an Inspector context-menu item. When invoked during Play Mode, it reads the current `DeckSaver.playerDeck` and produces a new `.asset` file that can be assigned to `CombatManager.enemyDeck` or `DeckSaver.defaultEnemyDecks` by the designer.

---

## 2. Functional Goals

| Goal | Description |
|---|---|
| **Manual capture player deck** | On context-menu invocation during Play Mode, read the current `DeckSaver.playerDeck` and persist it as a DeckSO asset. |
| **Produce reusable DeckSO assets** | Generate a new `DeckSO` `.asset` file under `Assets/SORefs/Decks/Recorded/` with all card prefabs resolved. |
| **Stable card matching** | Use `CardScript.cardTypeID` to look up the original prefab from the global card database (same cache used by `DeckSaver`). |
| **Non-destructive naming** | Each recording gets a unique filename; never overwrites an existing asset silently. |
| **Zero combat flow interference** | Recorder is a pure tool. It does not subscribe to phase events and does not modify `CombatManager.enemyDeck` at runtime. |

---

## 3. Changes from v1.1

| Topic | v1.1 | v2.0 (this revision) |
|---|---|---|
| **Auto Record** | Supported an `enableAutoRecord` toggle that fired on Shop → Combat transition. | **Removed.** Only manual recording is provided, preventing asset spam and avoiding timing conflicts with `DeckSaver.LoadJsonToEnemyDeckSo`. |
| **PhaseManager dependency** | Required a `PhaseManager` reference to subscribe to `onEnterCombatPhase`. | **Removed.** Recorder no longer subscribes to phase events. |
| **Runtime assignment** | Optional `autoAssignToEnemyDeck` branch in flowchart (contradicted Section 6.4). | **Removed.** Recorder only creates assets; designers assign them manually. |
| **Manual mode in Edit Mode** | Claimed "Record Now" worked without entering Play Mode. | **Corrected.** Context menu only works in Play Mode because it depends on `DeckSaver.Me`. |
| **Source Deck** | Allowed Inspector override (implied by removed fields). | **Fixed source:** always reads `DeckSaver.Me.playerDeck`. |
| **Inspector trigger** | Sketch showed a `[Record Now]` button. | Uses `[ContextMenu("Record Now")]` for minimal implementation. |

---

## 4. Existing System Analysis

### 4.1 Code Scan Results

| Component | File | Relevant Facts |
|---|---|---|
| `DeckSO` | `Assets/Scripts/SOScripts/DeckSO.cs` | `List<GameObject> deck`, `defaultDeck`, `resetOnStart`, `description`. Already a `[CreateAssetMenu]` ScriptableObject. |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` | Has `public string cardTypeID` (stable ID). Not guaranteed to be populated; `DeckSaver` falls back to `cardScript.name`. |
| `DeckSaver` | `Assets/Scripts/Managers/WriteRead/DeckSaver.cs` | Singleton set in `Awake()`. JSON persistence. Builds `_cardTypeToPrefabCache` from `shopPoolRef` + `additionalCardPrefabs`. Has `public DeckSO playerDeck`. Exposes `GetCardPrefabByTypeID(string)` (added for this feature). |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Singleton. `playerDeck` and `enemyDeck` are both `DeckSO`. `GatherDecks()` iterates `deck` lists and calls `CardFactory` to instantiate. |

### 4.2 Why Manual-Only?

The original auto-record approach had a timing conflict:
- `DeckSaver.LoadJsonToEnemyDeckSo` (which populates `enemyDeck`) is already wired as a **persistent listener** on `PhaseManager.onEnterCombatPhase` in `GameScene.unity`.
- UnityEvents execute persistent calls before runtime `AddListener` calls.
- Therefore any runtime subscription by the Recorder would run **after** `enemyDeck` is already determined for the current combat.

To avoid side effects and keep the Recorder purely additive, v2.0 removes all event subscriptions and phase dependencies.

---

## 5. Recorder Design

### 5.1 Component Specification

| Property | Value |
|---|---|
| **Script Name** | `EnemyDeckRecorder` |
| **Type** | `MonoBehaviour` |
| **Namespace** | `TestWriteRead` (same as `DeckSaver` and `DeckData`) |
| **Lifecycle** | Runtime validation in Play Mode; asset creation wrapped in `#if UNITY_EDITOR`. |
| **Location** | Attach to any scene GameObject (suggested: same object as `DeckSaver` or a dedicated "Tools" object). |

### 5.2 Inspector Interface

```
Enemy Deck Recorder (Script)
─────────────────────────────
Output Settings
  Output Folder   [SORefs/Decks/Recorded/]  ← relative to Assets/
  Naming Prefix   [RecordedDeck_]          ← filename prefix
  Use Timestamp   [✓]                      ← append _yyyyMMdd_HHmmss

Hotkey
  Record Hotkey   [F12]                    ← key to trigger Record Now in Play Mode

(Right-click this component header → Record Now)
```

> **Note:** `Phase Manager`, `Enable Auto Record`, `Auto Assign`, and `Source Deck` fields are removed in v2.0.

### 5.3 Execution Flow

```
[Right-click component → Record Now, during Play Mode]
         │
         ▼
[Validate]
    ├──▶ Application.isPlaying == true
    ├──▶ DeckSaver.Me != null
    ├──▶ DeckSaver.Me.playerDeck != null
    └──▶ DeckSaver.Me.playerDeck.deck.Count > 0
         │
         ▼
[Resolve Cards]
    ├──▶ Iterate DeckSaver.Me.playerDeck.deck (List<GameObject>)
    │         ├──▶ For each card prefab, read CardScript.cardTypeID
    │         ├──▶ If cardTypeID is empty, fall back to cardScript.name (with warning)
    │         ├──▶ Look up original prefab via DeckSaver.Me.GetCardPrefabByTypeID()
    │         └──▶ Add prefab to resolvedPrefabs list
    │
    ├──▶ Skip any card where cardScript.isStartCard == true
    └──▶ If any card cannot be resolved:
              LogWarning("Cannot resolve cardTypeID: {id}")
              Skip that card (do NOT add null to deck)
         │
         ▼
[Create Asset — Editor Only]
    ├──▶ ScriptableObject.CreateInstance<DeckSO>()
    ├──▶ deckSO.deck = resolvedPrefabs (List<GameObject>)
    ├──▶ deckSO.resetOnStart = false
    ├──▶ deckSO.description = auto-generated text
    ├──▶ Ensure directory: Assets/{outputFolder}/
    ├──▶ string assetPath = $"Assets/{outputFolder}/{filename}.asset"
    ├──▶ Resolve naming collision:
    │         If file exists → append _1, _2, ... until unique
    ├──▶ AssetDatabase.CreateAsset(deckSO, assetPath)
    ├──▶ AssetDatabase.SaveAssets()
    ├──▶ EditorUtility.FocusProjectWindow()
    ├──▶ Selection.activeObject = deckSO
    └──▶ Debug.Log($"[EnemyDeckRecorder] Recorded enemy deck: {assetPath}")
```

### 5.4 Naming Collision Resolution

```csharp
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
```

### 5.5 Output Example

```
Assets/
└── SORefs/
    └── Decks/
        ├── Default Enemy Decks/          ← existing static decks
        │   ├── #1Deck.asset
        │   ├── #2Deck.asset
        │   └── ...
        └── Recorded/                     ← new: manually generated from player decks
            ├── RecordedDeck_Session0_20260527_143022.asset
            ├── RecordedDeck_Session3_20260527_143022_1.asset
            └── RecordedDeck_Session5_20260614_121030.asset
```

---

## 6. Decisions

### 6.1 ✅ Card Matching Strategy

**Decision:** Use `CardScript.cardTypeID` to look up prefabs from the global card database cache.

**Rationale:** `cardTypeID` is the stable identifier already used by `DeckSaver`. If a card has no `cardTypeID`, fall back to `cardScript.name` with a warning (same behavior as `DeckSaver`).

### 6.2 ✅ DeckSO Storage Format

**Decision:** Store `List<GameObject>` (prefab references) in the generated DeckSO.

**Rationale:** Matches existing `DeckSO` schema exactly. `CombatManager.GatherDecks()` expects `List<GameObject>`.

### 6.3 ✅ Card Database Source

**Decision:** Reuse `DeckSaver`'s card database via a new public getter.

**Rationale:** `DeckSaver` already builds the canonical cache. The Recorder delegates prefab lookups to `DeckSaver.Me.GetCardPrefabByTypeID(string)`.

### 6.4 ✅ Enemy Deck Assignment

**Decision:** The Recorder only **produces** the DeckSO asset. It does **not** modify `CombatManager.enemyDeck` at runtime.

**Rationale:** Avoids timing conflicts with `DeckSaver.LoadJsonToEnemyDeckSo` and keeps the tool side-effect free. Designers drag the new `.asset` into `CombatManager.enemyDeck` or `DeckSaver.defaultEnemyDecks` manually.

### 6.5 ✅ Start Card Handling

**Decision:** Do NOT include the Start Card (`isStartCard == true`) in the recorded deck.

**Rationale:** The Start Card is a neutral round marker added automatically by `CombatManager.GatherDecks()`. Including it would cause duplication.

### 6.6 ✅ Status Effects / Runtime State

**Decision:** Record only the **card prefab** (base type). Do NOT persist runtime state.

**Rationale:** The enemy deck should represent deck composition, not combat state.

### 6.7 ✅ Manual-Only Recording

**Decision:** Remove auto-record entirely.

**Rationale:**
- Prevents asset spam.
- Avoids the `onEnterCombatPhase` listener-order conflict.
- Makes the tool explicit and predictable.

---

## 7. Usage Workflow

### 7.1 First-Time Setup (One-Time)

1. Create an empty GameObject in the scene: `EnemyDeckRecorder`.
2. Attach `EnemyDeckRecorder.cs`.
3. Set **Output Folder** to `SORefs/Decks/Recorded` (or your preference).
4. Set **Naming Prefix** to `RecordedDeck_`.

### 7.2 Manual Capture Workflow (Designer)

1. Enter Play Mode.
2. Buy cards in Shop (or use an existing test player deck).
3. At any point during Shop or Combat, either:
   - right-click the `EnemyDeckRecorder` component header and choose **Record Now**, or
   - press the configured hotkey (default **F12**).
4. A new `.asset` appears in `Assets/SORefs/Decks/Recorded/`.
5. Exit Play Mode.
6. Drag the new asset into `CombatManager.enemyDeck` or `DeckSaver.defaultEnemyDecks` for future combats.

> **Note:** The menu item only works in Play Mode because it depends on `DeckSaver.Me`.

---

## 8. Non-Functional Requirements

| Item | Requirement |
|---|---|
| **Platform** | Editor asset-creation is `#if UNITY_EDITOR` guarded. Runtime validation is a plain MonoBehaviour and safe in builds. |
| **Dependencies** | `UnityEditor` namespace (for `AssetDatabase`, `EditorUtility`). No external packages. Depends on `DeckSaver` singleton being present in the scene. |
| **Performance** | Asset creation is a one-time, manual, low-frequency operation. No runtime impact. |
| **Compatibility** | Unity 6000.0.62f1, URP, Input System. No conflicts with combat flow. |
| **Version Control** | Output folder `Assets/SORefs/Decks/Recorded/` should be Git-tracked. `.asset` and `.meta` files should both be committed. |
| **Null Safety** | All `List<>` fields guarded against null. Missing prefabs log warnings, never crash. |
| **Idempotency** | Naming collision resolution ensures multiple recordings never clobber each other. |

---

## 9. Code

### 9.1 EnemyDeckRecorder.cs

Path: `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TestWriteRead
{
	/// <summary>
	/// Manual tool that records the current player deck as a reusable DeckSO asset.
	/// Intended for designer workflows: run Play Mode, click "Record Now", then drag
	/// the generated asset into CombatManager.enemyDeck or DeckSaver.defaultEnemyDecks.
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
				Debug.LogWarning("[EnemyDeckRecorder] Recording is only available in Play Mode.");
				return;
			}

			var deckSaver = DeckSaver.Me;
			if (deckSaver == null)
			{
				Debug.LogWarning("[EnemyDeckRecorder] DeckSaver singleton is null.");
				return;
			}

			var sourceDeck = deckSaver.playerDeck;
			if (sourceDeck == null)
			{
				Debug.LogWarning("[EnemyDeckRecorder] DeckSaver.playerDeck is null.");
				return;
			}

			if (sourceDeck.deck == null || sourceDeck.deck.Count == 0)
			{
				Debug.LogWarning("[EnemyDeckRecorder] Player deck is empty.");
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
				Debug.LogWarning("[EnemyDeckRecorder] Could not resolve " + missingCards.Count +
					" card(s): " + string.Join(", ", missingCards));
			}

			if (resolvedPrefabs.Count == 0)
			{
				Debug.LogError("[EnemyDeckRecorder] No cards could be resolved. Aborting.");
				return;
			}

#if UNITY_EDITOR
			CreateDeckAsset(resolvedPrefabs, missingCards);
#else
			Debug.Log("[EnemyDeckRecorder] Asset creation is only available in Editor.");
#endif
		}

		private string GetCardTypeID(CardScript cardScript)
		{
			if (!string.IsNullOrEmpty(cardScript.cardTypeID))
				return cardScript.cardTypeID;

			Debug.LogWarning("[EnemyDeckRecorder] Card " + cardScript.name +
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

			string filename = GenerateFilename();
			string assetPath = ResolveUniqueAssetPath(outputFolder, filename);

			string fullFolder = Path.Combine(Application.dataPath, outputFolder);
			if (!Directory.Exists(fullFolder))
				Directory.CreateDirectory(fullFolder);

			AssetDatabase.CreateAsset(deckSO, assetPath);
			AssetDatabase.SaveAssets();

			EditorUtility.FocusProjectWindow();
			Selection.activeObject = deckSO;

			Debug.Log("[EnemyDeckRecorder] Recorded enemy deck: " + assetPath +
				" (" + resolvedPrefabs.Count + " cards)");
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
```

### 9.2 DeckSaver Extension

Path: `Assets/Scripts/Managers/WriteRead/DeckSaver.cs`

Added public getter:

```csharp
/// <summary>
/// Public getter for card prefab by cardTypeID. Used by EnemyDeckRecorder.
/// </summary>
public GameObject GetCardPrefabByTypeID(string cardTypeID)
{
    return FindCardPrefabByTypeID(cardTypeID);
}
```

---

## 10. Implementation Steps

1. ✅ Implement `DeckSaver.GetCardPrefabByTypeID()` — added to `DeckSaver.cs`.
2. ✅ Implement `EnemyDeckRecorder.cs` — placed in `Assets/Scripts/Managers/WriteRead/`.
3. ✅ Create output folder — `Assets/SORefs/Decks/Recorded/`.
4. **Add to scene** — attach `EnemyDeckRecorder` to the same GameObject as `DeckSaver` (or a dedicated tools object).
5. **Test:** Enter Play Mode → configure/buy cards → right-click component → **Record Now** → verify new `.asset` is created → assign to `CombatManager.enemyDeck` → verify next combat uses the recorded deck.
6. **Update documentation** — mention the new manual Recorder workflow in relevant docs.

---

*Document revised: 2026-06-14*  
*Aligned with: OneDeck Unity Project at `D:\Unity Projects\OneDeck`*
