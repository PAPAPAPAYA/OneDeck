using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DefaultNamespace.Managers;
using UnityEngine;

namespace TestWriteRead
{
    /// <summary>
    /// Single card win rate tracker
    /// Features:
    /// 1. Record combat count, wins, and losses for each player card
    /// 2. Save to local JSON
    /// 3. Export CSV report
    /// 
    /// Hotkeys (use in Game view):
    /// - Ctrl+Shift+P: Print win rate report
    /// - Ctrl+Shift+E: Export CSV file
    /// - Ctrl+Shift+C: Clear statistics data
    /// </summary>
    public class CardWinRateTracker : MonoBehaviour
    {
        #region SINGLETON
        public static CardWinRateTracker Me;

        private void Awake()
        {
            Me = this;
            _jsonPath = Application.persistentDataPath + "/card_winrate.json";
            _csvPath = Application.persistentDataPath + "/card_winrate.csv";
            LoadData();
            
            if (resetOnStart)
            {
                ClearAllData();
            }
        }
        #endregion

        [Header("System Switch")]
        public bool switchOnTracking = true;
        public bool resetOnStart = false;

        [Header("Debug")]
        [SerializeField] private bool printOnSave = true;

        // Local data
        private CardWinRateData _data;
        private string _jsonPath;
        private string _csvPath;

        // Current combat deck snapshot (recorded at combat start)
        private List<string> _currentCombatPlayerCardTypeIDs = new();

        /// <summary>
        /// Called at combat start, records cards in current player deck (pass prefab list)
        /// </summary>
        public void RecordPlayerDeckSnapshot(List<GameObject> playerCardPrefabs)
        {
            if (!switchOnTracking) return;
            
            _currentCombatPlayerCardTypeIDs.Clear();
            
            foreach (var cardPrefab in playerCardPrefabs)
            {
                if (cardPrefab == null) continue;
                
                var cardScript = cardPrefab.GetComponent<CardScript>();
                if (cardScript == null) continue;
                
                // Use cardTypeID as identifier, if none then use card name and warn
                string typeID = GetCardTypeID(cardScript);
                if (!string.IsNullOrEmpty(typeID))
                {
                    _currentCombatPlayerCardTypeIDs.Add(typeID);
                }
            }
            
            // Deduplicate (multiple cards of same type may exist)
            _currentCombatPlayerCardTypeIDs = _currentCombatPlayerCardTypeIDs.Distinct().ToList();
        }

        /// <summary>
        /// Called at combat end, updates stats for all participating cards
        /// </summary>
        /// <param name="playerWon">Whether player won</param>
        public void RecordCombatResult(bool playerWon)
        {
            if (!switchOnTracking) return;
            if (_currentCombatPlayerCardTypeIDs.Count == 0) return;

            foreach (var cardTypeID in _currentCombatPlayerCardTypeIDs)
            {
                UpdateCardStats(cardTypeID, playerWon);
            }

            SaveData();
            
            if (printOnSave)
            {
                Debug.Log($"[CardWinRateTracker] Recorded combat result: {(playerWon ? "Win" : "Loss")}, " +
                          $"Affected {_currentCombatPlayerCardTypeIDs.Count} cards");
            }
        }

        /// <summary>
        /// Get or create card statistics record
        /// </summary>
        private CardStats GetOrCreateStats(string cardTypeID)
        {
            var existing = _data.allCardStats.Find(s => s.cardTypeID == cardTypeID);
            if (existing != null) return existing;

            var newStats = new CardStats
            {
                cardTypeID = cardTypeID,
                totalCombats = 0,
                wins = 0,
                losses = 0
            };
            _data.allCardStats.Add(newStats);
            return newStats;
        }

        /// <summary>
        /// Update single card statistics
        /// </summary>
        private void UpdateCardStats(string cardTypeID, bool won)
        {
            var stats = GetOrCreateStats(cardTypeID);
            stats.totalCombats++;
            
            if (won)
                stats.wins++;
            else
                stats.losses++;
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
            Debug.LogWarning($"[CardWinRateTracker] Card {cardScript.name} has no cardTypeID configured, using card name as identifier");
            return cardScript.name;
        }

        #region Data Persistence

        private void LoadData()
        {
            if (File.Exists(_jsonPath))
            {
                try
                {
                    var json = File.ReadAllText(_jsonPath);
                    _data = JsonUtility.FromJson<CardWinRateData>(json);
                    if (_data == null)
                    {
                        _data = new CardWinRateData();
                    }
                    else
                    {
                        // Ensure list is not null
                        if (_data.allCardStats == null)
                            _data.allCardStats = new List<CardStats>();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CardWinRateTracker] Failed to read data: {e.Message}");
                    _data = new CardWinRateData();
                }
            }
            else
            {
                _data = new CardWinRateData();
            }
        }

        private void SaveData()
        {
            if (!switchOnTracking) return;
            
            _data.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            try
            {
                var json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(_jsonPath, json);
                if (printOnSave)
                {
                    Debug.Log($"[CardWinRateTracker] Statistics saved: {_jsonPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardWinRateTracker] Save data failed: {e.Message}");
            }
        }

        #endregion

        #region CSV Export

        /// <summary>
        /// Export win rate data to CSV file
        /// </summary>
        public void ExportToCSV()
        {
            if (_data.allCardStats.Count == 0)
            {
                Debug.LogWarning("[CardWinRateTracker] No data to export");
                return;
            }

            var sb = new StringBuilder();
            
            // CSV header
            sb.AppendLine("CardTypeID,CardName,TotalCombats,Wins,Losses,WinRate,LastUpdated");
            
            // Sort by win rate
            var sortedStats = _data.allCardStats
                .OrderByDescending(s => s.WinRate)
                .ThenByDescending(s => s.totalCombats)
                .ToList();
            
            foreach (var stat in sortedStats)
            {
                sb.AppendLine($"{stat.cardTypeID},{stat.totalCombats},{stat.wins},{stat.losses},{stat.WinRate:F4},{_data.lastUpdated}");
            }

            try
            {
                File.WriteAllText(_csvPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[CardWinRateTracker] CSV exported to: {_csvPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardWinRateTracker] CSV export failed: {e.Message}");
            }
        }

        #endregion

        #region Query Interface

        /// <summary>
        /// Get single card statistics
        /// </summary>
        public CardStats GetCardStats(string cardTypeID)
        {
            return _data.allCardStats.Find(s => s.cardTypeID == cardTypeID);
        }

        /// <summary>
        /// Print all cards' win rate report to console
        /// </summary>
        public void PrintReport()
        {
            if (_data.allCardStats.Count == 0)
            {
                Debug.Log("[CardWinRateTracker] No data yet");
                return;
            }

            Debug.Log("========== CARD WIN RATE REPORT ==========");
            
            var sortedStats = _data.allCardStats
                .OrderByDescending(s => s.WinRate)
                .ThenByDescending(s => s.totalCombats)
                .ToList();

            foreach (var stat in sortedStats)
            {
                Debug.Log(stat.ToString());
            }
            
            Debug.Log($"Total {_data.allCardStats.Count} cards, last updated: {_data.lastUpdated}");
            Debug.Log("====================================");
        }

        /// <summary>
        /// Clear all statistics data
        /// </summary>
        public void ClearAllData()
        {
            _data = new CardWinRateData();
            if (File.Exists(_jsonPath))
            {
                File.Delete(_jsonPath);
            }
            if (File.Exists(_csvPath))
            {
                File.Delete(_csvPath);
            }
            Debug.Log("[CardWinRateTracker] All statistics data cleared");
        }

        #endregion

        #region Debug Hotkeys
        // Hotkey instructions (must be active in Game view):
        // Ctrl + Shift + P: Print win rate report to console
        // Ctrl + Shift + E: Export CSV file to persistent path
        // Ctrl + Shift + C: Clear all statistics data (use with caution)

        private void Update()
        {
            // Ctrl + Shift + P: Print report
            if (Input.GetKeyDown(KeyCode.P) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                PrintReport();
            }
            
            // Ctrl + Shift + E: Export CSV
            if (Input.GetKeyDown(KeyCode.E) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                ExportToCSV();
            }
            
            // Ctrl + Shift + C: Clear data
            if (Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                ClearAllData();
            }
        }

        #endregion
    }
}
