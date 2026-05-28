# OneDeck — Enemy Deck Recorder Product Requirements Document (PRD)

> **Version:** v1.0  
> **Date:** 2026-05-27  
> **Status:** Draft — Aligned with Existing Codebase

---

## 1. Background

The project already has a **DeckSaver** system that persists player decks to JSON (`deckdata.json`) using `cardTypeID` strings. It also has a **DeckSO** ScriptableObject system with 10 default enemy decks (`#1Deck` through `#10Deck`).

However, there is no bridge between these two systems:
- DeckSaver writes to an opaque JSON file outside version control.
- DeckSOs are manually authored and static.
- There is no automated way to **turn a proven player deck (from PlayMode) into a reusable enemy DeckSO asset**.

The Enemy Deck Recorder closes this gap. It sits in the scene as a MonoBehaviour, optionally auto-triggers at combat exit, and produces a new `.asset` file that can be immediately assigned to `CombatManager.enemyDeck` or `DeckSaver.defaultEnemyDecks`.

---

## 2. Functional Goals

| Goal | Description |
|---|---|
| **Auto-capture player deck** | At combat exit (or manual trigger), read the current `playerDeck` GameObject list and persist it. |
| **Produce reusable DeckSO assets** | Generate a new `DeckSO` `.asset` file under `Assets/SORefs/Decks/Recorded/` with all card prefabs resolved. |
| **Stable card matching** | Use `CardScript.cardTypeID` to look up the original prefab from the global card database (same cache used by `DeckSaver`). |
| **Non-destructive naming** | Each recording gets a unique filename; never overwrites an existing asset silently. |
| **Zero runtime code changes** | Existing `CombatManager`, `PhaseManager`, and `DeckSaver` logic remains untouched. Recorder is an additive component. |

---

## 3. Existing System Analysis

### 3.1 Code Scan Results (2026-05-27)

| Component | File | Relevant Facts |
|---|---|---|
| `DeckSO` | `Assets/Scripts/SOScripts/DeckSO.cs` | `List<GameObject> deck`, `defaultDeck`, `resetOnStart`, `description`. Already a `[CreateAssetMenu]` ScriptableObject. |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` | Has `public string cardTypeID` (unique stable ID), `displayName`, `cardDesc`, `rarity`, `shopRollWeightMultiplier`, `takeUpSpace`, `isStartCard`, `isMinion`. |
| `DeckSaver` | `Assets/Scripts/Managers/WriteRead/DeckSaver.cs` | Singleton. JSON persistence to `Application.persistentDataPath + "/deckdata.json"`. Uses `DeckSaveEntry` (`List<string> cardTypeIDs`). Has `BuildCardDatabaseCache()` via `shopPoolRef` + `additionalCardPrefabs`. |
| `CardFactory` | `Assets/Scripts/Managers/CardFactory.cs` | Singleton. `CreateLogicalCard()` instantiates prefabs with ownership setup. |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Singleton. `playerDeck` and `enemyDeck` are both `DeckSO`. `GatherDecks()` iterates `deck` lists and calls `CardFactory` to instantiate. |
| `PhaseManager` | `Assets/Scripts/Managers/PhaseManager.cs` | Drives Shop → Combat → Result loop. Fires `onExitCombatPhase` UnityEvent when combat ends. |
| `EffectRecorder` | `Assets/Scripts/Managers/EffectRecorder.cs` | Existing recorder for animation replay. **Unrelated** — this PRD is about deck recording, not effect recording. |
| `DeckTester` | `Assets/Scripts/Managers/DeckTester.cs` | Auto-battle test harness. Tracks win rates and damage per session. |
| `CardIDRetriever` | `Assets/Scripts/Managers/CardIDRetriever.cs` | Singleton. Assigns runtime `cardID` integers. Not used for stable identification. |

### 3.2 Asset Inventory

| Path | Contents |
|---|---|
| `Assets/SORefs/Decks/Default Enemy/` | `#1Deck.asset` … `#10Deck.asset` — static default enemy decks used by `DeckSaver.defaultEnemyDecks`. |
| `Assets/SORefs/Decks/Deprecated/` | Old test decks (`TestDeckAllStab`, `mana`, `shiv`, etc.). |
| `Assets/SORefs/Decks/PlayerRefs/test/` | Test player decks for specific mechanic validation. |
| `Assets/SORefs/EnemyRefs/` | `EnemyDeckRef.asset`, `EnemyDefaultDeckRef.asset`, `EnemyStatusRef.asset`. |

### 3.3 Key Finding: Two Parallel Deck Systems

```
┌─────────────────────────────┐      ┌─────────────────────────────┐
│      DeckSO System          │      │    DeckSaver JSON System    │
│  (ScriptableObject assets)  │      │  (Runtime JSON persistence) │
│                             │      │                             │
│  List<GameObject> deck      │      │  List<string> cardTypeIDs   │
│  Used by CombatManager      │      │  Used by DeckSaver          │
│  for GatherDecks()          │      │  for Save/Load between      │
│                             │      │  sessions                   │
└─────────────────────────────┘      └─────────────────────────────┘
              │                                    │
              └─────────── Enemy Deck Recorder ────┘
                        (bridges both systems)
```

The Recorder reads from the **live `playerDeck`** (`DeckSO` with `List<GameObject>`) and produces a **new `DeckSO` asset** containing the same card prefabs, ready for reuse as an enemy deck.

---

## 4. Data Structures (As-Is — No New Types Required)

### 4.1 DeckSO (Existing)

```csharp
[CreateAssetMenu(fileName = "DeckSO", menuName = "SORefs/DeckSO")]
public class DeckSO : ScriptableObject
{
    public List<GameObject> deck;
    public DeckSO defaultDeck;
    public bool resetOnStart;
    [TextArea]
    public string description;
    // ... OnEnable() handles reset logic
}
```

### 4.2 CardScript (Relevant Fields)

```csharp
public class CardScript : MonoBehaviour
{
    public int cardID;               // Runtime ID (from CardIDRetriever)
    public string cardTypeID;        // STABLE unique identifier ✅
    public string displayName;       // Display name
    public string cardDesc;            // Description
    public EnumStorage.Rarity rarity;
    public float shopRollWeightMultiplier = 1f;
    public bool takeUpSpace = true;
    public bool isStartCard = false;
    public bool isMinion = false;
    public IntSO price;
    public PlayerStatusSO myStatusRef;
    public PlayerStatusSO theirStatusRef;
    public List<EnumStorage.StatusEffect> myStatusEffects;
    public List<EnumStorage.Tag> myTags;
}
```

### 4.3 DeckSaver Card Database (Reused by Recorder)

`DeckSaver` already builds a `Dictionary<string, GameObject> _cardTypeToPrefabCache` from:
1. `shopPoolRef.deck` (primary source)
2. `additionalCardPrefabs` (overflow / non-shop cards)

The Recorder **must reuse this same cache** (or build an identical one) to ensure card prefab resolution is consistent with the save/load system.

---

## 5. Recorder Design

### 5.1 Component Specification

| Property | Value |
|---|---|
| **Script Name** | `EnemyDeckRecorder` |
| **Type** | `MonoBehaviour` |
| **Namespace** | `TestWriteRead` (same as `DeckSaver` and `DeckData`) |
| **Lifecycle** | Editor-only `#if UNITY_EDITOR` wrapper for asset creation; runtime portion is harmless in builds. |
| **Location** | Attach to any scene GameObject (suggested: same object as `DeckSaver` or a dedicated "Tools" object). |

### 5.2 Inspector Interface

```
Enemy Deck Recorder (Script)
─────────────────────────────
[✓] Enable Auto Record              ← bool, triggers automatically on combat exit

Source Deck
  [PlayerDeckRef (DeckSO)________]  ← drag player DeckSO here

Card Database
  [ShopPoolRef (DeckSO)____________]  ← same as DeckSaver.shopPoolRef
  [Additional Cards (GameObject[])_]  ← same as DeckSaver.additionalCardPrefabs

Output Settings
  Output Folder   [SORefs/Decks/Recorded/]  ← relative to Assets/
  Naming Prefix   [RecordedDeck_]          ← filename prefix
  Use Timestamp   [✓]                      ← append _yyyyMMdd_HHmmss

Advanced
  [✓] Append Session Number             ← include sessionNum in filename
  [ ] Include Win/Loss in Description   ← auto-fill DeckSO.description

─────────────────────────────
[ Record Now ]  ← manual button (Editor-only)
```

### 5.3 Execution Flow — Auto Mode

```
[Enable Auto Record = true]
         │
         ▼
[Combat Ends] ← PhaseManager.ExitingCombatPhase() / combatFinished = true
         │
         ▼
[EnemyDeckRecorder.OnCombatExit()] ← subscribed to PhaseManager.onExitCombatPhase
         │
         ▼
[Validate]
    ├──▶ sourcePlayerDeck != null
    ├──▶ sourcePlayerDeck.deck.Count > 0
    └──▶ (Optional) combatResult == Win → only record winning decks
         │
         ▼
[Resolve Cards]
    ├──▶ Iterate sourcePlayerDeck.deck (List<GameObject>)
    │         ├──▶ For each cardInstance, read CardScript.cardTypeID
    │         ├──▶ Look up original prefab in card database cache
    │         └──▶ Add prefab to resolvedPrefabs list
    │
    └──▶ If any cardTypeID cannot be resolved:
              LogWarning("Cannot resolve cardTypeID: {id}")
              Skip that card (do NOT add null to deck)
         │
         ▼
[Create Asset — Editor Only]
    ├──▶ ScriptableObject.CreateInstance<DeckSO>()
    ├──▶ deckSO.deck = resolvedPrefabs (List<GameObject>)
    ├──▶ deckSO.description = auto-generated text
    │         e.g. "Recorded from player deck after session {N}. {W} wins."
    ├──▶ Ensure directory: Assets/{outputFolder}/
    ├──▶ string assetPath = $"Assets/{outputFolder}/{filename}.asset"
    ├──▶ Resolve naming collision:
    │         If file exists → append _1, _2, ... until unique
    ├──▶ AssetDatabase.CreateAsset(deckSO, assetPath)
    ├──▶ AssetDatabase.SaveAssets()
    ├──▶ EditorUtility.FocusProjectWindow()
    ├──▶ Selection.activeObject = deckSO
    └──▶ Debug.Log($"[EnemyDeckRecorder] Recorded enemy deck: {assetPath}")
         │
         ▼
[Post-Record Action (Optional)]
    ├──▶ If autoAssignToEnemyDeck == true:
    │         CombatManager.Me.enemyDeck = deckSO
    └──▶ If autoAddToDefaults == true:
              DeckSaver.Me.defaultEnemyDecks.Add(deckSO)
```

### 5.4 Execution Flow — Manual Mode (Editor)

```
[Inspector: Click "Record Now"]
         │
         ▼
[EnemyDeckRecorder.RecordNow()]
    ├──▶ Same validation + resolve + create logic as Auto Mode
    └──▶ Does NOT require PlayMode — can record from any assigned source deck
```

### 5.5 Naming Collision Resolution

```csharp
private string ResolveUniqueAssetPath(string folder, string baseName)
{
    string path = $"Assets/{folder}/{baseName}.asset";
    if (!File.Exists(Path.Combine(Application.dataPath, $"{folder}/{baseName}.asset")))
        return path;

    int suffix = 1;
    while (true)
    {
        string candidate = $"Assets/{folder}/{baseName}_{suffix}.asset";
        if (!File.Exists(Path.Combine(Application.dataPath, $"{folder}/{baseName}_{suffix}.asset")))
            return candidate;
        suffix++;
    }
}
```

### 5.6 Output Example

```
Assets/
└── SORefs/
    └── Decks/
        ├── Default Enemy/          ← existing static decks
        │   ├── #1Deck.asset
        │   ├── #2Deck.asset
        │   └── ...
        └── Recorded/               ← new: auto-generated from player decks
            ├── RecordedDeck_20260527_143022.asset
            ├── RecordedDeck_20260527_143022_1.asset
            ├── RecordedDeck_Session5_Win_20260528_090015.asset
            └── RecordedDeck_Session12_Win_20260529_213045.asset
```

---

## 6. Decisions — All Confirmed

### 6.1 ✅ Card Matching Strategy

**Decision:** Use `CardScript.cardTypeID` to look up prefabs from the global card database cache.

**Rationale:** `cardID` is runtime-only (assigned by `CardIDRetriever`). `cardTypeID` is the stable identifier already used by `DeckSaver`, win-rate tracking, and the shop system. If a card in the player deck has no `cardTypeID`, fall back to `cardScript.name` with a warning (same behavior as `DeckSaver.GetCardTypeID()`).

### 6.2 ✅ DeckSO Storage Format

**Decision:** Store `List<GameObject>` (prefab references) in the generated DeckSO.

**Rationale:** This matches the existing `DeckSO` schema exactly. `CombatManager.GatherDecks()` expects `List<GameObject>` and passes each element to `CardFactory.CreateLogicalCard()`. No schema migration needed.

### 6.3 ✅ Card Database Source

**Decision:** Reuse `DeckSaver`'s card database building logic.

**Rationale:** `DeckSaver` already has the canonical `BuildCardDatabaseCache()` method that reads from `shopPoolRef` + `additionalCardPrefabs`. The Recorder should either:
- **Option A (Recommended):** Call `DeckSaver.Me._cardTypeToPrefabCache` directly (if accessible) or expose a public getter.
- **Option B:** Duplicate the cache-building logic internally, using the same Inspector fields (`shopPoolRef`, `additionalCardPrefabs`).

**Recommended:** Option A — add a public `GetCardPrefab(string cardTypeID)` method to `DeckSaver` so the Recorder can share the cache without duplicating configuration.

### 6.4 ✅ Enemy Deck Assignment

**Decision:** The Recorder only **produces** the DeckSO asset. It does **not** automatically modify `CombatManager.enemyDeck` at runtime.

**Rationale:**
- The existing `DeckSaver.PopulateEnemyDeckBySessionNumber()` already handles runtime assignment from JSON.
- Designers can drag the new `.asset` into `CombatManager.enemyDeck` or `DeckSaver.defaultEnemyDecks` manually.
- Optional: add an `[ExecuteInEditMode]` utility button "Assign to Enemy Deck" for one-click wiring.

### 6.5 ✅ Start Card Handling

**Decision:** Do NOT include the Start Card (`isStartCard == true`) in the recorded deck.

**Rationale:** The Start Card is a neutral round marker added automatically by `CombatManager.GatherDecks()` via `factory.CreateStartCard()`. Including it in a recorded enemy deck would cause duplication.

**Implementation:** During resolution, skip any card where `cardScript.isStartCard == true`.

### 6.6 ✅ Status Effects / Runtime State

**Decision:** Record only the **card prefab** (base type). Do NOT persist runtime state (applied status effects, modified HP, etc.).

**Rationale:** The enemy deck should represent the **deck composition** (which cards), not the **state** of those cards. Status effects are applied during combat by `EffectChainManager` and `StatusEffectGiverEffect`.

---

## 7. Usage Workflow

### 7.1 First-Time Setup (One-Time)

1. Create an empty GameObject in the scene: `EnemyDeckRecorder`.
2. Attach `EnemyDeckRecorder.cs`.
3. Drag `PlayerDeckRef` (or the active player `DeckSO`) into **Source Deck**.
4. Drag `ShopPoolRef` into **Card Database → Shop Pool Ref**.
5. (Optional) Add any non-shop card prefabs to **Additional Cards**.
6. Set **Output Folder** to `SORefs/Decks/Recorded` (or your preference).
7. Set **Naming Prefix** to `RecordedDeck_`.
8. (Optional) Check **Enable Auto Record** for hands-free capture.

### 7.2 Per-Session Auto-Capture Workflow

1. Enter PlayMode. Fight a combat session.
2. Combat ends → `PhaseManager` fires `onExitCombatPhase`.
3. `EnemyDeckRecorder` auto-triggers (if enabled).
4. A new `.asset` appears in `Assets/SORefs/Decks/Recorded/`.
5. (Optional) Drag the new asset into `CombatManager.enemyDeck` for the next fight.
6. (Optional) Add it to `DeckSaver.defaultEnemyDecks` for session-based auto-loading.

### 7.3 Manual Capture Workflow (Designer)

1. In Edit Mode (no need to enter PlayMode):
   - Configure a test player deck in a `DeckSO` asset.
   - Drag it into `EnemyDeckRecorder` → **Source Deck**.
2. Click **Record Now** in the Inspector.
3. New `.asset` is created immediately.
4. Use this asset for balancing tests in `DeckTester`.

---

## 8. Non-Functional Requirements

| Item | Requirement |
|---|---|
| **Platform** | Editor asset-creation is `#if UNITY_EDITOR` guarded. Runtime portion is a plain MonoBehaviour and safe in builds. |
| **Dependencies** | `UnityEditor` namespace (for `AssetDatabase`, `EditorUtility`). No external packages. |
| **Performance** | Asset creation is a one-time, low-frequency operation. Uses `AssetDatabase.CreateAsset()` — no runtime impact. |
| **Compatibility** | Unity 6000.0.62f1, URP, Input System. No conflicts with `CombatUXManager`, `EffectChainManager`, or `RecorderAnimationPlayer`. |
| **Version Control** | Output folder `Assets/SORefs/Decks/Recorded/` should be Git-tracked. `.asset` files are YAML text and merge-friendly. |
| **Null Safety** | All `List<>` fields guarded against null. Missing prefabs log warnings, never crash. |
| **Idempotency** | Naming collision resolution ensures multiple recordings never clobber each other. |

---

## 9. Code Sketch

### 9.1 EnemyDeckRecorder.cs (Runtime + Editor)

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
    public class EnemyDeckRecorder : MonoBehaviour
    {
        [Header("Auto Record")]
        [Tooltip("Automatically record player deck when combat exits")]
        public bool enableAutoRecord = false;

        [Tooltip("Only record if player won the combat")]
        public bool recordOnlyOnWin = false;

        [Header("Source")]
        [Tooltip("Player deck to record from")]
        public DeckSO sourcePlayerDeck;

        [Header("Card Database")]
        [Tooltip("Shop pool — primary card database source")]
        public DeckSO shopPoolRef;

        [Tooltip("Additional card prefabs not in shop pool")]
        public List<GameObject> additionalCardPrefabs;

        [Header("Output")]
        [Tooltip("Output folder relative to Assets/")]
        public string outputFolder = "SORefs/Decks/Recorded";

        [Tooltip("Filename prefix")]
        public string namingPrefix = "RecordedDeck_";

        [Tooltip("Append timestamp to filename")]
        public bool useTimestamp = true;

        [Tooltip("Append session number to filename")]
        public bool appendSessionNumber = true;

        [Tooltip("Include win/loss info in DeckSO description")]
        public bool includeResultInDescription = true;

        // Internal cache
        private Dictionary<string, GameObject> _cardTypeToPrefabCache;

        private void OnEnable()
        {
            // Subscribe to combat exit if PhaseManager exposes the event
            var phaseManager = FindObjectOfType<PhaseManager>();
            if (phaseManager != null)
            {
                phaseManager.onExitCombatPhase.AddListener(OnCombatExit);
            }
        }

        private void OnDisable()
        {
            var phaseManager = FindObjectOfType<PhaseManager>();
            if (phaseManager != null)
            {
                phaseManager.onExitCombatPhase.RemoveListener(OnCombatExit);
            }
        }

        /// <summary>
        /// Called automatically when combat exits (if subscribed to PhaseManager event)
        /// </summary>
        private void OnCombatExit()
        {
            if (!enableAutoRecord) return;

            // Optional: check win condition
            if (recordOnlyOnWin)
            {
                var combatManager = CombatManager.Me;
                if (combatManager == null || combatManager.ownerPlayerStatusRef.hp <= 0)
                    return; // Player lost — skip recording
            }

            RecordDeck();
        }

        /// <summary>
        /// Main record entry point. Can be called manually or automatically.
        /// </summary>
        public void RecordDeck()
        {
            if (sourcePlayerDeck == null)
            {
                Debug.LogWarning("[EnemyDeckRecorder] Source player deck is null.");
                return;
            }

            if (sourcePlayerDeck.deck == null || sourcePlayerDeck.deck.Count == 0)
            {
                Debug.LogWarning("[EnemyDeckRecorder] Source player deck is empty.");
                return;
            }

            BuildCardDatabaseCache();

            var resolvedPrefabs = new List<GameObject>();
            var missingCards = new List<string>();

            foreach (var cardInstance in sourcePlayerDeck.deck)
            {
                if (cardInstance == null) continue;

                var cardScript = cardInstance.GetComponent<CardScript>();
                if (cardScript == null) continue;

                // Skip Start Card — it's auto-added by CombatManager
                if (cardScript.isStartCard) continue;

                string typeID = GetCardTypeID(cardScript);
                if (string.IsNullOrEmpty(typeID))
                {
                    missingCards.Add(cardInstance.name);
                    continue;
                }

                var prefab = FindCardPrefabByTypeID(typeID);
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
                Debug.LogWarning($"[EnemyDeckRecorder] Could not resolve {missingCards.Count} card(s): " +
                    string.Join(", ", missingCards));
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

        #region Card Database Cache

        private void BuildCardDatabaseCache()
        {
            _cardTypeToPrefabCache = new Dictionary<string, GameObject>();

            if (shopPoolRef != null && shopPoolRef.deck != null)
            {
                foreach (var prefab in shopPoolRef.deck)
                    AddCardToCache(prefab);
            }

            if (additionalCardPrefabs != null)
            {
                foreach (var prefab in additionalCardPrefabs)
                    AddCardToCache(prefab);
            }
        }

        private void AddCardToCache(GameObject prefab)
        {
            if (prefab == null) return;
            var cardScript = prefab.GetComponent<CardScript>();
            if (cardScript == null) return;

            string typeID = GetCardTypeID(cardScript);
            if (string.IsNullOrEmpty(typeID)) return;

            if (_cardTypeToPrefabCache.ContainsKey(typeID))
                return; // Skip duplicates silently

            _cardTypeToPrefabCache[typeID] = prefab;
        }

        private string GetCardTypeID(CardScript cardScript)
        {
            if (!string.IsNullOrEmpty(cardScript.cardTypeID))
                return cardScript.cardTypeID;
            return cardScript.name;
        }

        private GameObject FindCardPrefabByTypeID(string cardTypeID)
        {
            if (_cardTypeToPrefabCache == null || _cardTypeToPrefabCache.Count == 0)
                BuildCardDatabaseCache();

            _cardTypeToPrefabCache.TryGetValue(cardTypeID, out var prefab);
            return prefab;
        }

        #endregion

#if UNITY_EDITOR

        #region Asset Creation (Editor Only)

        private void CreateDeckAsset(List<GameObject> resolvedPrefabs, List<string> missingCards)
        {
            var deckSO = ScriptableObject.CreateInstance<DeckSO>();
            deckSO.deck = new List<GameObject>(resolvedPrefabs);
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

            Debug.Log($"[EnemyDeckRecorder] Recorded enemy deck: {assetPath} ({resolvedPrefabs.Count} cards)");
        }

        private string GenerateFilename()
        {
            var parts = new List<string> { namingPrefix };

            if (appendSessionNumber)
            {
                var sessionRef = FindObjectOfType<PhaseManager>()?.sessionNum;
                int sessionNum = sessionRef != null ? sessionRef.value : 0;
                parts.Add($"Session{sessionNum}_");
            }

            if (useTimestamp)
            {
                parts.Add(DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            }

            return string.Join("", parts).TrimEnd('_');
        }

        private string GenerateDescription(int cardCount, int missingCount)
        {
            var lines = new List<string>
            {
                $"Auto-recorded player deck.",
                $"Cards: {cardCount}",
                $"Missing: {missingCount}",
                $"Recorded: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            };

            if (includeResultInDescription)
            {
                var combatManager = CombatManager.Me;
                if (combatManager != null)
                {
                    bool playerWon = combatManager.ownerPlayerStatusRef.hp > 0;
                    lines.Add($"Result: {(playerWon ? "WIN" : "LOSE")}");
                }
            }

            return string.Join("\n", lines);
        }

        private string ResolveUniqueAssetPath(string folder, string baseName)
        {
            string relativePath = $"Assets/{folder}/{baseName}.asset";
            string fullPath = Path.Combine(Application.dataPath, $"{folder}/{baseName}.asset");

            if (!File.Exists(fullPath))
                return relativePath;

            int suffix = 1;
            while (true)
            {
                string candidateRelative = $"Assets/{folder}/{baseName}_{suffix}.asset";
                string candidateFull = Path.Combine(Application.dataPath, $"{folder}/{baseName}_{suffix}.asset");
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

### 9.2 Optional: DeckSaver Extension

Add a public getter to `DeckSaver` so `EnemyDeckRecorder` can reuse its cache:

```csharp
// In DeckSaver.cs — add this public method
public GameObject GetCardPrefabByTypeID(string cardTypeID)
{
    if (_cardTypeToPrefabCache == null || _cardTypeToPrefabCache.Count == 0)
        BuildCardDatabaseCache();

    _cardTypeToPrefabCache.TryGetValue(cardTypeID, out var prefab);
    return prefab;
}
```

If this is added, `EnemyDeckRecorder` can skip building its own cache and simply call `DeckSaver.Me?.GetCardPrefabByTypeID(typeID)`.

---

## 10. Next Steps

1. **Confirm approach:** Does the user want the standalone `EnemyDeckRecorder` (with its own cache), or a tighter integration where it delegates to `DeckSaver`?
2. **Implement `EnemyDeckRecorder.cs`** — place in `Assets/Scripts/Managers/WriteRead/` alongside `DeckSaver` and `DeckData`.
3. **(Optional) Extend `DeckSaver`** — add `GetCardPrefabByTypeID()` public method.
4. **Create output folder** — `Assets/SORefs/Decks/Recorded/`.
5. **Add to scene** — attach `EnemyDeckRecorder` to the same GameObject as `DeckSaver` (or a dedicated tools object).
6. **Wire events** — ensure `PhaseManager.onExitCombatPhase` fires (it already does via `ExitingCombatPhase()` → `InvokeExitCombatPhaseEvent()`).
7. **Test:** Enter PlayMode → fight → exit combat → verify new `.asset` is created → assign to `CombatManager.enemyDeck` → verify next combat uses the recorded deck.
8. **Update `DeckSaver`** documentation / in-game tooltip to mention the new Recorder workflow.

---

*Document generated: 2026-05-27*  
*Aligned with: OneDeck Unity Project at `D:\Unity Projects\OneDeck`*
