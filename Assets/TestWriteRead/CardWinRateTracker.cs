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
    /// 单卡胜率统计器
    /// 功能：
    /// 1. 记录每张玩家卡的战斗次数、胜利次数、失败次数
    /// 2. 保存到本地JSON
    /// 3. 导出CSV报告
    /// 
    /// 快捷键（Game视图中使用）：
    /// - Ctrl+Shift+P: 打印胜率报告
    /// - Ctrl+Shift+E: 导出CSV文件
    /// - Ctrl+Shift+C: 清空统计数据
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
        }
        #endregion

        [Header("System Switch")]
        public bool switchOnTracking = true;

        [Header("Debug")]
        [SerializeField] private bool printOnSave = true;

        // 本地数据
        private CardWinRateData _data;
        private string _jsonPath;
        private string _csvPath;

        // 当前战斗中的卡组快照（在战斗开始时记录）
        private List<string> _currentCombatPlayerCardTypeIDs = new();

        /// <summary>
        /// 战斗开始时调用，记录当前玩家卡组中的卡（传入预制体列表）
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
                
                // 使用 cardTypeID 作为标识，如果没有则使用卡名并警告
                string typeID = GetCardTypeID(cardScript);
                if (!string.IsNullOrEmpty(typeID))
                {
                    _currentCombatPlayerCardTypeIDs.Add(typeID);
                }
            }
            
            // 去重（同类型的卡可能有多个）
            _currentCombatPlayerCardTypeIDs = _currentCombatPlayerCardTypeIDs.Distinct().ToList();
        }

        /// <summary>
        /// 战斗结束时调用，更新所有参与卡的统计
        /// </summary>
        /// <param name="playerWon">玩家是否获胜</param>
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
                Debug.Log($"[CardWinRateTracker] 已记录战斗结果：{(playerWon ? "胜利" : "失败")}，" +
                          $"影响 {_currentCombatPlayerCardTypeIDs.Count} 张卡");
            }
        }

        /// <summary>
        /// 获取或创建卡的统计记录
        /// </summary>
        private CardStats GetOrCreateStats(string cardTypeID, string cardName = "")
        {
            var existing = _data.allCardStats.Find(s => s.cardTypeID == cardTypeID);
            if (existing != null) return existing;

            var newStats = new CardStats
            {
                cardTypeID = cardTypeID,
                cardName = cardName,
                totalCombats = 0,
                wins = 0,
                losses = 0
            };
            _data.allCardStats.Add(newStats);
            return newStats;
        }

        /// <summary>
        /// 更新单张卡的统计数据
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
        /// 从 CardScript 获取稳定的卡类型ID
        /// </summary>
        private string GetCardTypeID(CardScript cardScript)
        {
            // 优先使用配置的 cardTypeID
            if (!string.IsNullOrEmpty(cardScript.cardTypeID))
            {
                return cardScript.cardTypeID;
            }
            
            // 如果没有配置，使用卡名并警告
            Debug.LogWarning($"[CardWinRateTracker] 卡 {cardScript.name} 没有配置 cardTypeID，使用卡名作为标识");
            return cardScript.name;
        }

        #region 数据持久化

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
                        // 确保列表不为null
                        if (_data.allCardStats == null)
                            _data.allCardStats = new List<CardStats>();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CardWinRateTracker] 读取数据失败: {e.Message}");
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
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardWinRateTracker] 保存数据失败: {e.Message}");
            }
        }

        #endregion

        #region CSV导出

        /// <summary>
        /// 导出胜率数据到CSV文件
        /// </summary>
        public void ExportToCSV()
        {
            if (_data.allCardStats.Count == 0)
            {
                Debug.LogWarning("[CardWinRateTracker] 没有数据可以导出");
                return;
            }

            var sb = new StringBuilder();
            
            // CSV头部
            sb.AppendLine("CardTypeID,CardName,TotalCombats,Wins,Losses,WinRate,LastUpdated");
            
            // 按胜率排序
            var sortedStats = _data.allCardStats
                .OrderByDescending(s => s.WinRate)
                .ThenByDescending(s => s.totalCombats)
                .ToList();
            
            foreach (var stat in sortedStats)
            {
                sb.AppendLine($"{stat.cardTypeID},{stat.cardName},{stat.totalCombats},{stat.wins},{stat.losses},{stat.WinRate:F4},{_data.lastUpdated}");
            }

            try
            {
                File.WriteAllText(_csvPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[CardWinRateTracker] CSV已导出到: {_csvPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardWinRateTracker] CSV导出失败: {e.Message}");
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 获取单张卡的统计
        /// </summary>
        public CardStats GetCardStats(string cardTypeID)
        {
            return _data.allCardStats.Find(s => s.cardTypeID == cardTypeID);
        }

        /// <summary>
        /// 打印所有卡的胜率报告到控制台
        /// </summary>
        public void PrintReport()
        {
            if (_data.allCardStats.Count == 0)
            {
                Debug.Log("[CardWinRateTracker] 暂无数据");
                return;
            }

            Debug.Log("========== 卡胜率统计报告 ==========");
            
            var sortedStats = _data.allCardStats
                .OrderByDescending(s => s.WinRate)
                .ThenByDescending(s => s.totalCombats)
                .ToList();

            foreach (var stat in sortedStats)
            {
                Debug.Log(stat.ToString());
            }
            
            Debug.Log($"总计 {_data.allCardStats.Count} 张卡，最后更新: {_data.lastUpdated}");
            Debug.Log("====================================");
        }

        /// <summary>
        /// 清空所有统计数据
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
            Debug.Log("[CardWinRateTracker] 已清空所有统计数据");
        }

        #endregion

        #region Debug快捷键
        // 快捷键说明（需在 Game 视图中激活）:
        // Ctrl + Shift + P: 打印胜率报告到控制台
        // Ctrl + Shift + E: 导出CSV文件到持久化路径
        // Ctrl + Shift + C: 清空所有统计数据（谨慎使用）

        private void Update()
        {
            // Ctrl + Shift + P: 打印报告
            if (Input.GetKeyDown(KeyCode.P) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                PrintReport();
            }
            
            // Ctrl + Shift + E: 导出CSV
            if (Input.GetKeyDown(KeyCode.E) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                ExportToCSV();
            }
            
            // Ctrl + Shift + C: 清空数据
            if (Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
            {
                ClearAllData();
            }
        }

        #endregion
    }
}
