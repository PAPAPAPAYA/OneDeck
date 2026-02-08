using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DefaultNamespace.Managers
{
    /// <summary>
    /// 单张卡在商店中的统计数据
    /// </summary>
    [Serializable]
    public class CardShopStats
    {
        public string cardTypeID;      // 唯一标识（优先使用）
        public string cardName;        // 显示名称
        public int appearCount;        // 出现次数
        public int boughtCount;        // 购买次数
        
        // 计算购买率（0-1）
        public float PurchaseRate => appearCount > 0 ? (float)boughtCount / appearCount : 0f;
        
        // 格式化输出
        public override string ToString()
        {
            return $"[{cardTypeID}] {cardName}: 购买率 {PurchaseRate:P1} ({boughtCount}买/{appearCount}现)";
        }
    }

    /// <summary>
    /// 商店统计数据容器（用于JSON序列化）
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
    /// 商店统计管理器
    /// 功能：
    /// 1. 记录卡牌在商店中的出现次数、购买次数
    /// 2. 记录商店访问次数和刷新次数
    /// 3. 保存到本地JSON
    /// 4. 导出CSV报告
    /// 
    /// 快捷键（Game视图中使用）：
    /// - Ctrl+Shift+P: 打印统计报告
    /// - Ctrl+Shift+E: 导出CSV文件
    /// - Ctrl+Shift+R: 重置统计数据
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

        // 本地数据
        private ShopStatsData _statsData;
        private string _jsonPath;
        private string _csvPath;

        // 延迟保存标记
        private bool _pendingSave = false;

        /// <summary>
        /// 记录卡牌出现在商店中
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
        /// 记录卡牌被购买
        /// </summary>
        public void RecordCardBought(string cardTypeID, string cardName = "")
        {
            if (!enableStats) return;

            var stat = GetOrCreateCardStat(cardTypeID, cardName);
            stat.boughtCount++;
            _pendingSave = true;
        }

        /// <summary>
        /// 记录商店访问次数
        /// </summary>
        public void RecordShopVisit()
        {
            if (!enableStats) return;

            _statsData.totalShopVisits++;
            _pendingSave = true;
        }

        /// <summary>
        /// 记录刷新次数
        /// </summary>
        public void RecordReroll()
        {
            if (!enableStats) return;

            _statsData.totalRerolls++;
            _pendingSave = true;
        }

        /// <summary>
        /// 立即保存（在合适的时机调用，如离开商店时）
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
        /// 获取或创建卡牌统计数据
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
            // 更新卡名（如果之前为空）
            else if (string.IsNullOrEmpty(stat.cardName) && !string.IsNullOrEmpty(cardName))
            {
                stat.cardName = cardName;
            }
            return stat;
        }

        /// <summary>
        /// 获取指定卡牌的统计数据
        /// </summary>
        public CardShopStats GetCardStats(string cardTypeID)
        {
            return _statsData.cardStats.Find(s => s.cardTypeID == cardTypeID);
        }

        /// <summary>
        /// 计算购买率
        /// </summary>
        public float GetPurchaseRate(string cardTypeID)
        {
            var stat = GetCardStats(cardTypeID);
            if (stat == null || stat.appearCount == 0) return 0f;
            return stat.PurchaseRate;
        }

        #region 数据持久化

        /// <summary>
        /// 保存统计数据到JSON
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
                    Debug.Log($"[ShopStatsManager] 统计已保存: {_jsonPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopStatsManager] 保存统计失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从JSON加载统计数据
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
                        // 确保列表不为null
                        if (_statsData.cardStats == null)
                            _statsData.cardStats = new List<CardShopStats>();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ShopStatsManager] 加载统计失败: {e.Message}");
                    _statsData = new ShopStatsData();
                }
            }
            else
            {
                _statsData = new ShopStatsData();
            }
        }

        #endregion

        #region CSV导出

        /// <summary>
        /// 导出统计数据到CSV文件
        /// </summary>
        public void ExportToCSV()
        {
            if (_statsData.cardStats.Count == 0)
            {
                Debug.LogWarning("[ShopStatsManager] 没有数据可以导出");
                return;
            }

            var sb = new StringBuilder();
            
            // CSV头部
            sb.AppendLine("CardTypeID,CardName,AppearCount,BoughtCount,PurchaseRate,LastUpdated");
            
            // 按购买率排序
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
                Debug.Log($"[ShopStatsManager] CSV已导出: {_csvPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopStatsManager] CSV导出失败: {e.Message}");
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 打印所有统计报告到控制台
        /// </summary>
        public void PrintReport()
        {
            if (_statsData.cardStats.Count == 0)
            {
                Debug.Log("[ShopStatsManager] 暂无数据");
                return;
            }

            Debug.Log("========== 商店卡牌统计报告 ==========");
            Debug.Log($"总商店访问次数: {_statsData.totalShopVisits}");
            Debug.Log($"总刷新次数: {_statsData.totalRerolls}");
            Debug.Log($"统计卡牌种类数: {_statsData.cardStats.Count}");
            Debug.Log("");
            
            var sortedStats = _statsData.cardStats
                .OrderByDescending(s => s.PurchaseRate)
                .ThenByDescending(s => s.appearCount)
                .ToList();

            foreach (var stat in sortedStats)
            {
                Debug.Log(stat.ToString());
            }
            
            Debug.Log($"最后更新: {_statsData.lastUpdated}");
            Debug.Log("======================================");
        }

        /// <summary>
        /// 重置所有统计数据
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
            
            Debug.Log("[ShopStatsManager] 统计数据已重置");
        }

        #endregion

        #region 生命周期

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
            
            // Ctrl + Shift + R: 重置数据
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
