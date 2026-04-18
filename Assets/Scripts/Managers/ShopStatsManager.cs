using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DefaultNamespace.Managers
{
    /// <summary>
    /// Stats for a single card in the shop
    /// </summary>
    [Serializable]
    public class CardShopStats
    {
        public string cardTypeID;      // Unique identifier (preferred)
        public string cardName;        // Display name
        public int appearCount;        // Appear count
        public int boughtCount;        // Bought count
        
        // Calculate purchase rate (0-1)
        public float PurchaseRate => appearCount > 0 ? (float)boughtCount / appearCount : 0f;
        
        // Formatted output
        public override string ToString()
        {
            return $"[{cardTypeID}] {cardName}: Purchase Rate {PurchaseRate:P1} ({boughtCount} bought/{appearCount} appeared)";
        }
    }

    /// <summary>
    /// Shop stats data container (for JSON serialization)
    /// </summary>
    [Serializable]
    public class ShopStatsData
    {
        public List<CardShopStats> cardStats = new List<CardShopStats>();
        public int totalShopVisits;
        public int totalRerolls;
        public string lastUpdated;
        
        public ShopStatsData()
        {
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// Shop stats manager
    /// Features:
    /// 1. Record card appear count and bought count in shop
    /// 2. Record shop visit count and reroll count
    /// 3. Save to local JSON
    /// 4. Export CSV report
    /// 
    /// Shortcuts (used in Game view):
    /// - Ctrl+Shift+P: Print stats report
    /// - Ctrl+Shift+E: Export CSV file
    /// - Ctrl+Shift+R: Reset stats
    /// </summary>
    public class ShopStatsManager : MonoBehaviour
    {
        #region Singleton
        public static ShopStatsManager Me;

        private void Awake()
        {
            Me = this;
            _jsonPath = Path.Combine(Application.persistentDataPath, "shop_stats.json");
            _csvPath = Path.Combine(Application.persistentDataPath, "shop_stats.csv");
            LoadStats();
            
            if (resetOnStart)
            {
                ResetStats();
            }
        }
        #endregion

        [Header("System Switch")]
        public bool enableStats = true;
        public bool resetOnStart = false;

        [Header("Debug")]
        [SerializeField] private bool printOnSave = true;

        // Local data
        private ShopStatsData _statsData;
        private string _jsonPath;
        private string _csvPath;

        // Pending save flag
        private bool _pendingSave = false;

        /// <summary>
        /// Record card appeared in shop
        /// </summary>
        public void RecordCardAppeared(string cardTypeID, string cardName = "")
        {
            if (!enableStats) return;

            var stat = GetOrCreateCardStat(cardTypeID, cardName);
            stat.appearCount++;
            _pendingSave = true;
            print("recorded card appeared");
        }

        /// <summary>
        /// Record card bought
        /// </summary>
        public void RecordCardBought(string cardTypeID, string cardName = "")
        {
            if (!enableStats) return;

            var stat = GetOrCreateCardStat(cardTypeID, cardName);
            stat.boughtCount++;
            _pendingSave = true;
        }

        /// <summary>
        /// Record shop visit count
        /// </summary>
        public void RecordShopVisit()
        {
            if (!enableStats) return;

            _statsData.totalShopVisits++;
            _pendingSave = true;
        }

        /// <summary>
        /// Record reroll count
        /// </summary>
        public void RecordReroll()
        {
            if (!enableStats) return;

            _statsData.totalRerolls++;
            _pendingSave = true;
        }

        /// <summary>
        /// Save immediately (call at appropriate times, such as when leaving shop)
        /// </summary>
        public void Flush()
        {
            if (_pendingSave)
            {
                SaveStats();
                _pendingSave = false;
            }
        }

        /// <summary>
        /// Get or create card stats
        /// </summary>
        private CardShopStats GetOrCreateCardStat(string cardTypeID, string cardName = "")
        {
            var stat = _statsData.cardStats.Find(s => s.cardTypeID == cardTypeID);
            if (stat == null)
            {
                stat = new CardShopStats
                {
                    cardTypeID = cardTypeID,
                    cardName = cardName,
                    appearCount = 0,
                    boughtCount = 0
                };
                _statsData.cardStats.Add(stat);
            }
            // Update card name (if previously empty)
            else if (string.IsNullOrEmpty(stat.cardName) && !string.IsNullOrEmpty(cardName))
            {
                stat.cardName = cardName;
            }
            return stat;
        }

        /// <summary>
        /// Get stats for specified card
        /// </summary>
        public CardShopStats GetCardStats(string cardTypeID)
        {
            return _statsData.cardStats.Find(s => s.cardTypeID == cardTypeID);
        }

        /// <summary>
        /// Calculate purchase rate
        /// </summary>
        public float GetPurchaseRate(string cardTypeID)
        {
            var stat = GetCardStats(cardTypeID);
            if (stat == null || stat.appearCount == 0) return 0f;
            return stat.PurchaseRate;
        }

        #region Data Persistence

        /// <summary>
        /// Save stats to JSON
        /// </summary>
        public void SaveStats()
        {
            if (!enableStats) return;
            if (_statsData == null) return;

            _statsData.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                string json = JsonUtility.ToJson(_statsData, true);
                File.WriteAllText(_jsonPath, json);
                
                if (printOnSave)
                {
                    Debug.Log($"[ShopStatsManager] Stats saved: {_jsonPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopStatsManager] Failed to save stats: {e.Message}");
            }
        }

        /// <summary>
        /// Load stats from JSON
        /// </summary>
        public void LoadStats()
        {
            if (File.Exists(_jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(_jsonPath);
                    _statsData = JsonUtility.FromJson<ShopStatsData>(json);
                    
                    if (_statsData == null)
                    {
                        _statsData = new ShopStatsData();
                    }
                    else
                    {
                        // Ensure list is not null
                        if (_statsData.cardStats == null)
                            _statsData.cardStats = new List<CardShopStats>();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ShopStatsManager] Failed to load stats: {e.Message}");
                    _statsData = new ShopStatsData();
                }
            }
            else
            {
                _statsData = new ShopStatsData();
            }
        }

        #endregion

        #region CSV Export

        /// <summary>
        /// Export stats to CSV file
        /// </summary>
        public void ExportToCSV()
        {
            if (_statsData.cardStats.Count == 0)
            {
                Debug.LogWarning("[ShopStatsManager] No data to export");
                return;
            }

            var sb = new StringBuilder();
            
            // CSV header
            sb.AppendLine("CardTypeID,CardName,AppearCount,BoughtCount,PurchaseRate,LastUpdated");
            
            // Sort by purchase rate
            var sortedStats = _statsData.cardStats
                .OrderByDescending(s => s.PurchaseRate)
                .ThenByDescending(s => s.appearCount)
                .ToList();
            
            foreach (var stat in sortedStats)
            {
                sb.AppendLine($"{stat.cardTypeID},{stat.cardName},{stat.appearCount},{stat.boughtCount},{stat.PurchaseRate:F4},{_statsData.lastUpdated}");
            }

            try
            {
                File.WriteAllText(_csvPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[ShopStatsManager] CSV exported: {_csvPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopStatsManager] CSV export failed: {e.Message}");
            }
        }

        #endregion

        #region Query Interface

        /// <summary>
        /// Print all stats report to console
        /// </summary>
        public void PrintReport()
        {
            if (_statsData.cardStats.Count == 0)
            {
                Debug.Log("[ShopStatsManager] No data yet");
                return;
            }

            Debug.Log("========== Shop Card Statistics Report ==========");
            Debug.Log($"Total shop visits: {_statsData.totalShopVisits}");
            Debug.Log($"Total rerolls: {_statsData.totalRerolls}");
            Debug.Log($"Card types tracked: {_statsData.cardStats.Count}");
            Debug.Log("");
            
            var sortedStats = _statsData.cardStats
                .OrderByDescending(s => s.PurchaseRate)
                .ThenByDescending(s => s.appearCount)
                .ToList();

            foreach (var stat in sortedStats)
            {
                Debug.Log(stat.ToString());
            }
            
            Debug.Log($"Last updated: {_statsData.lastUpdated}");
            Debug.Log("======================================");
        }

        /// <summary>
        /// Reset all stats
        /// </summary>
        public void ResetStats()
        {
            _statsData = new ShopStatsData();
            _pendingSave = false;
            
            if (File.Exists(_jsonPath))
            {
                File.Delete(_jsonPath);
            }
            if (File.Exists(_csvPath))
            {
                File.Delete(_csvPath);
            }
            
            Debug.Log("[ShopStatsManager] Stats reset");
        }

        #endregion

        #region Lifecycle

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
            
            // Ctrl + Shift + R: Reset data
            if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                ResetStats();
            }
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        private void OnDestroy()
        {
            Flush();
        }

        #endregion
    }
}
