using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DefaultNamespace.Managers;
using UnityEngine;

// script responsible for: 
// 1. save current player deck to json (using cardTypeID for stability)
// 2. load matching round number deck randomly to enemy deck
// 3. wipe saved decks
// 4. enable/disable save/load
namespace TestWriteRead
{
    /// <summary>
    /// Deck save system - uses cardTypeID instead of GameObject instances to store decks, avoiding instance ID change issues
    /// </summary>
    public class DeckSaver : MonoBehaviour
    {
        #region SINGLETON
        public static DeckSaver Me;

        private void Awake()
        {
            Me = this;
            _savePath = Application.persistentDataPath + "/deckdata.json";
        }
        #endregion

        [Header("System Switch")]
        public bool switchOnSaveLoad = false;
        public bool resetOnStart = false;

        [Header("Deck Info Refs")]
        public DeckSO playerDeck; // Player deck reference
        public IntSO winAmount; // Current win count
        public IntSO heartLeft; // Current hearts
        public IntSO sessionNumber; // Current session number
        public DeckSO enemyDeckToPopulate; // Enemy deck reference (for population)

        [Header("Status Refs")]
        public PlayerStatusSO playerStatusRef; // Player status reference (for hpMax)
        public PlayerStatusSO enemyStatusRef; // Enemy status reference (for setting hpMax)

        [Header("Card Database")]
        [Tooltip("Shop card pool, used to build card database (automatically reads all available cards from it)")]
        public DeckSO shopPoolRef; // Shop card pool reference

        [Tooltip("Additional card prefabs (optional, for cards not in shop pool)")]
        public List<GameObject> additionalCardPrefabs; // Additional cards (optional)

        [Header("Default Enemy Decks")]
        [Tooltip("When no deck for corresponding session exists in JSON, randomly select from this list")]
        public List<DeckSO> defaultEnemyDecks; // Default enemy deck configuration list

        [Header("Debug")]
        [SerializeField] private bool printOnSave = true;

        // Local data
        private DeckData _currentData;
        private string _savePath;

        // Card type ID to prefab mapping cache
        private Dictionary<string, GameObject> _cardTypeToPrefabCache;

        private void Start()
        {
            BuildCardDatabaseCache();


            if (resetOnStart)
            {
                WipeDeckSaves();
            }

            LoadData();
        }

        /// <summary>
        /// Build card type ID to prefab mapping cache
        /// </summary>
        private void BuildCardDatabaseCache()
        {
            _cardTypeToPrefabCache = new Dictionary<string, GameObject>();

            // Read cards from shop pool
            if (shopPoolRef != null && shopPoolRef.deck != null)
            {
                foreach (var cardPrefab in shopPoolRef.deck)
                {
                    AddCardToCache(cardPrefab);
                }
            }
            else
            {
                Debug.LogWarning("[DeckSaver] ShopPoolRef is not set or empty, card database will be empty");
            }

            // Add additional cards (if any)
            if (additionalCardPrefabs != null)
            {
                foreach (var cardPrefab in additionalCardPrefabs)
                {
                    AddCardToCache(cardPrefab);
                }
            }

            if (printOnSave)
            {
                Debug.Log($"[DeckSaver] Card database built, total {_cardTypeToPrefabCache.Count} cards");
            }
        }

        /// <summary>
        /// Add card to cache dictionary
        /// </summary>
        private void AddCardToCache(GameObject cardPrefab)
        {
            if (cardPrefab == null) return;

            var cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null) return;

            string typeID = GetCardTypeID(cardScript);
            if (string.IsNullOrEmpty(typeID)) return;

            if (_cardTypeToPrefabCache.ContainsKey(typeID))
            {
                Debug.LogWarning($"[DeckSaver] Duplicate cardTypeID: {typeID}, card: {cardPrefab.name}");
                return;
            }
            _cardTypeToPrefabCache[typeID] = cardPrefab;
        }

        /// <summary>
        /// Get stable card type ID from CardScript
        /// </summary>
        private string GetCardTypeID(CardScript cardScript)
        {
            // Prefer configured cardTypeID
            if (!string.IsNullOrEmpty(cardScript.cardTypeID))
            {
                return cardScript.cardTypeID;
            }

            // If not configured, use card name and warn
            Debug.LogWarning($"[DeckSaver] Card {cardScript.name} has no cardTypeID configured, using card name as identifier");
            return cardScript.name;
        }

        /// <summary>
        /// Find card prefab by cardTypeID
        /// </summary>
        private GameObject FindCardPrefabByTypeID(string cardTypeID)
        {
            if (_cardTypeToPrefabCache == null || _cardTypeToPrefabCache.Count == 0)
            {
                BuildCardDatabaseCache();
            }

            if (_cardTypeToPrefabCache.TryGetValue(cardTypeID, out var prefab))
            {
                return prefab;
            }

            Debug.LogError($"[DeckSaver] Cannot find card prefab with cardTypeID {cardTypeID}");
            return null;
        }

        #region Data Persistence

        /// <summary>
        /// Load saved data
        /// </summary>
        private void LoadData()
        {
            if (!File.Exists(_savePath))
            {
                _currentData = new DeckData();
                return;
            }

            try
            {
                var json = File.ReadAllText(_savePath);
                _currentData = JsonUtility.FromJson<DeckData>(json);

                if (_currentData == null)
                {
                    _currentData = new DeckData();
                }
                else
                {
                    // Ensure list is not null
                    if (_currentData.savedDecks == null)
                        _currentData.savedDecks = new List<DeckSaveEntry>();

                    // Version migration: migrate old format data to new format
                    MigrateOldData();
                }

                if (printOnSave)
                {
                    Debug.Log($"[DeckSaver] Loaded {_currentData.savedDecks.Count} saved decks");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeckSaver] Failed to read data: {e.Message}");
                _currentData = new DeckData();
            }
        }

        /// <summary>
        /// Migrate old format data (using GameObject lists) to new format (using cardTypeID lists)
        /// </summary>
        private void MigrateOldData()
        {
            // Keep compatibility here, if old data needs migration it can be handled here
            // Currently new data structure is independent from GameObject
        }

        /// <summary>
        /// Save data to JSON
        /// </summary>
        private void SaveData()
        {
            if (!switchOnSaveLoad) return;

            _currentData.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                var json = JsonUtility.ToJson(_currentData, true);
                File.WriteAllText(_savePath, json);

                if (printOnSave)
                {
                    Debug.Log($"[DeckSaver] Deck saved: {_savePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeckSaver] Save data failed: {e.Message}");
            }
        }

        #endregion

        #region Deck Operations

        /// <summary>
        /// Save current player deck to JSON
        /// </summary>
        public void SavePlayerDeckToJson()
        {
            if (!switchOnSaveLoad) return;

            // Create deck entry
            var deckEntry = CreateDeckSaveEntry();
            _currentData.savedDecks.Add(deckEntry);

            SaveData();

            Debug.Log($"[DeckSaver] Saved session {sessionNumber.value} deck, total {deckEntry.cardTypeIDs.Count} cards");
        }

        /// <summary>
        /// Create save entry from current player deck
        /// </summary>
        private DeckSaveEntry CreateDeckSaveEntry()
        {
            var cardTypeIDs = new List<string>();

            foreach (var cardPrefab in playerDeck.deck)
            {
                if (cardPrefab == null) continue;

                var cardScript = cardPrefab.GetComponent<CardScript>();
                if (cardScript == null) continue;

                string typeID = GetCardTypeID(cardScript);
                if (!string.IsNullOrEmpty(typeID))
                {
                    cardTypeIDs.Add(typeID);
                }
            }

            return new DeckSaveEntry
            {
                cardTypeIDs = cardTypeIDs,
                winAmount = winAmount.value,
                heartLeft = heartLeft.value,
                sessionNum = sessionNumber.value,
                hpMax = playerStatusRef != null ? playerStatusRef.hpMax : 20
            };
        }

        /// <summary>
        /// Populate enemy deck by current session number.
        /// Prioritize loading saved deck from JSON, otherwise use default deck list.
        /// </summary>
        public void PopulateEnemyDeckBySessionNumber()
        {
            // Try loading from JSON first
            if (TryLoadFromJson())
            {
                return;
            }

            // When no JSON match, select from default list
            PopulateFromDefaultDecks();
        }

        /// <summary>
        /// Try to load deck matching current session number from JSON file
        /// </summary>
        /// <returns>Whether loading succeeded</returns>
        private bool TryLoadFromJson()
        {
            if (!switchOnSaveLoad) return false;

            // Filter matching decks
            var matchingDecks = _currentData.savedDecks
                .Where(d => d.sessionNum == sessionNumber.value)
                .ToList();

            if (matchingDecks.Count == 0) return false;

            // Randomly select a matching deck
            var randomDeck = matchingDecks[UnityEngine.Random.Range(0, matchingDecks.Count)];

            // Convert cardTypeID list to GameObject list
            var cardPrefabs = new List<GameObject>();
            foreach (var typeID in randomDeck.cardTypeIDs)
            {
                var prefab = FindCardPrefabByTypeID(typeID);
                if (prefab != null)
                {
                    cardPrefabs.Add(prefab);
                }
            }

            // Populate enemy deck
            enemyDeckToPopulate.deck.Clear();
            enemyDeckToPopulate.deck.AddRange(cardPrefabs);

            // Apply saved hpMax to enemy
            if (enemyStatusRef != null)
            {
                enemyStatusRef.hpMax = randomDeck.hpMax > 0 ? randomDeck.hpMax : 20;
                Debug.Log($"[DeckSaver] Loaded enemy deck for session {sessionNumber.value} from JSON, total {cardPrefabs.Count} cards, enemy hpMax set to {enemyStatusRef.hpMax}");
            }
            else
            {
                Debug.Log($"[DeckSaver] Loaded enemy deck for session {sessionNumber.value} from JSON, total {cardPrefabs.Count} cards (enemy StatusRef not set, cannot apply hpMax)");
            }
            return true;
        }

        /// <summary>
        /// Select corresponding deck from default enemy deck list by current session number to populate.
        /// session 1 -> list item 1, session 2 -> list item 2, and so on.
        /// If session number exceeds list range, use last item.
        /// </summary>
        private void PopulateFromDefaultDecks()
        {
            if (defaultEnemyDecks == null || defaultEnemyDecks.Count == 0)
            {
                Debug.LogWarning($"[DeckSaver] Session {sessionNumber.value}: No JSON record and default deck list is empty, cannot populate enemy deck");
                return;
            }

            // Use session number directly as deck index (session 0 -> #1Deck, session 1 -> #2Deck)
            int deckIndex = sessionNumber.value;
            // If out of range, use last item
            if (deckIndex >= defaultEnemyDecks.Count)
            {
                deckIndex = defaultEnemyDecks.Count - 1;
            }
            var selectedDeck = defaultEnemyDecks[deckIndex];

            // Use utility function to copy deck
            UtilityFuncManagerScript.CopyGameObjectList(selectedDeck.deck, enemyDeckToPopulate.deck, true);
            Debug.Log($"[DeckSaver] Session {sessionNumber.value}: Loaded enemy deck from default list: {selectedDeck.name}");
        }

        /// <summary>
        /// Delete all saved deck data
        /// </summary>
        public void WipeDeckSaves()
        {
            _currentData = new DeckData();

            if (File.Exists(_savePath))
            {
                try
                {
                    File.Delete(_savePath);
                    Debug.Log($"[DeckSaver] Deleted save file: {_savePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DeckSaver] Failed to delete save file: {e.Message}");
                }
            }
        }

        #endregion

        #region Query Interface

        /// <summary>
        /// Get all saved deck statistics
        /// </summary>
        public void PrintSavedDecksInfo()
        {
            if (_currentData.savedDecks.Count == 0)
            {
                Debug.Log("[DeckSaver] No saved decks");
                return;
            }

            Debug.Log("========== SAVED DECK STATISTICS ==========");

            var groupedBySession = _currentData.savedDecks
                .GroupBy(d => d.sessionNum)
                .OrderBy(g => g.Key);

            foreach (var group in groupedBySession)
            {
                Debug.Log($"Session {group.Key}: {group.Count()} decks");
            }

            Debug.Log($"Total {_currentData.savedDecks.Count} decks, last updated: {_currentData.lastUpdated}");
            Debug.Log("====================================");
        }

        #endregion

        #region Backward Compatibility

        // Keep old method but call new one for backward compatibility
        [Obsolete("Use PopulateEnemyDeckBySessionNumber instead")]
        public void LoadJsonToEnemyDeckSo()
        {
            PopulateEnemyDeckBySessionNumber();
        }

        #endregion

        #region Debug Hotkeys
        // Hotkey instructions (must be active in Game view):
        // Ctrl + S: Save current player deck to JSON
        // Ctrl + L: Load deck to enemy deck
        // Ctrl + W: Clear all saved decks
        // Ctrl + D: Print saved deck statistics

        private void Update()
        {
            if (!Input.GetKey(KeyCode.LeftControl)) return;

            // Ctrl + S: Save
            if (Input.GetKeyDown(KeyCode.S) && !Input.GetKey(KeyCode.LeftShift))
            {
                SavePlayerDeckToJson();
            }

            // Ctrl + L: Load
            if (Input.GetKeyDown(KeyCode.L))
            {
                PopulateEnemyDeckBySessionNumber();
            }

            // Ctrl + W: Clear
            if (Input.GetKeyDown(KeyCode.W))
            {
                WipeDeckSaves();
            }

            // Ctrl + D: Print statistics
            if (Input.GetKeyDown(KeyCode.D))
            {
                PrintSavedDecksInfo();
            }
        }

        #endregion
    }
}
