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
    /// 卡组保存系统 - 使用cardTypeID而非GameObject实例来存储卡组，避免实例ID变化问题
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
        public DeckSO playerDeck; // 玩家卡组引用
        public IntSO winAmount; // 当前胜利数
        public IntSO heartLeft; // 当前生命值
        public IntSO sessionNumber; // 当前session编号
        public DeckSO enemyDeckToPopulate; // 敌人卡组引用（用于填充）
        
        [Header("Status Refs")]
        public PlayerStatusSO playerStatusRef; // 玩家状态引用（获取hpMax）
        public PlayerStatusSO enemyStatusRef; // 敌人状态引用（设置hpMax）
        
        [Header("Card Database")]
        [Tooltip("商店卡牌池，用于构建卡牌数据库（自动从中读取所有可用卡牌）")]
        public DeckSO shopPoolRef; // 商店卡牌池引用
        
        [Tooltip("额外的卡牌预制体（可选，用于不在商店池中的卡牌）")]
        public List<GameObject> additionalCardPrefabs; // 额外卡牌（可选）
        
        [Header("Default Enemy Decks")]
        [Tooltip("当JSON中没有对应session的卡组时，从此列表中随机选择一个")]
        public List<DeckSO> defaultEnemyDecks; // 默认敌人卡组配置列表
        
        [Header("Debug")]
        [SerializeField] private bool printOnSave = true;

        // 本地数据
        private DeckData _currentData;
        private string _savePath;
        
        // 卡牌类型ID到预制体的映射缓存
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
        /// 构建卡牌类型ID到预制体的映射缓存
        /// </summary>
        private void BuildCardDatabaseCache()
        {
            _cardTypeToPrefabCache = new Dictionary<string, GameObject>();
            
            // 从商店池读取卡牌
            if (shopPoolRef != null && shopPoolRef.deck != null)
            {
                foreach (var cardPrefab in shopPoolRef.deck)
                {
                    AddCardToCache(cardPrefab);
                }
            }
            else
            {
                Debug.LogWarning("[DeckSaver] ShopPoolRef 未设置或为空，卡牌数据库将为空");
            }
            
            // 添加额外卡牌（如果有）
            if (additionalCardPrefabs != null)
            {
                foreach (var cardPrefab in additionalCardPrefabs)
                {
                    AddCardToCache(cardPrefab);
                }
            }
            
            if (printOnSave)
            {
                Debug.Log($"[DeckSaver] 卡牌数据库构建完成，共 {_cardTypeToPrefabCache.Count} 张卡牌");
            }
        }
        
        /// <summary>
        /// 将卡牌添加到缓存字典
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
                Debug.LogWarning($"[DeckSaver] 重复的cardTypeID: {typeID}，卡牌: {cardPrefab.name}");
                return;
            }
            _cardTypeToPrefabCache[typeID] = cardPrefab;
        }

        /// <summary>
        /// 从CardScript获取稳定的卡类型ID
        /// </summary>
        private string GetCardTypeID(CardScript cardScript)
        {
            // 优先使用配置的cardTypeID
            if (!string.IsNullOrEmpty(cardScript.cardTypeID))
            {
                return cardScript.cardTypeID;
            }
            
            // 如果没有配置，使用卡名并警告
            Debug.LogWarning($"[DeckSaver] 卡 {cardScript.name} 没有配置cardTypeID，使用卡名作为标识");
            return cardScript.name;
        }

        /// <summary>
        /// 根据cardTypeID查找卡牌预制体
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
            
            Debug.LogError($"[DeckSaver] 找不到cardTypeID为 {cardTypeID} 的卡牌预制体");
            return null;
        }

        #region 数据持久化

        /// <summary>
        /// 加载已保存的数据
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
                    // 确保列表不为null
                    if (_currentData.savedDecks == null)
                        _currentData.savedDecks = new List<DeckSaveEntry>();
                        
                    // 版本迁移：将旧格式的数据迁移到新格式
                    MigrateOldData();
                }
                
                if (printOnSave)
                {
                    Debug.Log($"[DeckSaver] 已加载 {_currentData.savedDecks.Count} 个保存的卡组");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeckSaver] 读取数据失败: {e.Message}");
                _currentData = new DeckData();
            }
        }

        /// <summary>
        /// 将旧格式数据（使用GameObject列表）迁移到新格式（使用cardTypeID列表）
        /// </summary>
        private void MigrateOldData()
        {
            // 这里保留兼容性，如果有旧数据需要迁移可以在这里处理
            // 目前新数据结构已经独立于GameObject
        }

        /// <summary>
        /// 保存数据到JSON
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
                    Debug.Log($"[DeckSaver] 卡组已保存: {_savePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeckSaver] 保存数据失败: {e.Message}");
            }
        }

        #endregion

        #region 卡组操作

        /// <summary>
        /// 将当前玩家卡组保存到JSON
        /// </summary>
        public void SavePlayerDeckToJson()
        {
            if (!switchOnSaveLoad) return;
            
            // 创建卡组条目
            var deckEntry = CreateDeckSaveEntry();
            _currentData.savedDecks.Add(deckEntry);
            
            SaveData();
            
            Debug.Log($"[DeckSaver] 已保存session {sessionNumber.value}的卡组，共 {deckEntry.cardTypeIDs.Count} 张卡");
        }

        /// <summary>
        /// 从当前玩家卡组创建保存条目
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
        /// 根据当前session number填充enemy deck
        /// 优先从JSON加载已保存的卡组，如果没有则使用默认卡组列表
        /// </summary>
        public void PopulateEnemyDeckBySessionNumber()
        {
            // 先尝试从JSON加载
            if (TryLoadFromJson())
            {
                return;
            }
            
            // JSON没有匹配时，从默认列表选择
            PopulateFromDefaultDecks();
        }

        /// <summary>
        /// 尝试从JSON文件加载匹配当前session number的卡组
        /// </summary>
        /// <returns>是否成功加载</returns>
        private bool TryLoadFromJson()
        {
            if (!switchOnSaveLoad) return false;
            
            // 筛选匹配的卡组
            var matchingDecks = _currentData.savedDecks
                .Where(d => d.sessionNum == sessionNumber.value)
                .ToList();
            
            if (matchingDecks.Count == 0) return false;
            
            // 随机选择一个匹配卡组
            var randomDeck = matchingDecks[UnityEngine.Random.Range(0, matchingDecks.Count)];
            
            // 将cardTypeID列表转换为GameObject列表
            var cardPrefabs = new List<GameObject>();
            foreach (var typeID in randomDeck.cardTypeIDs)
            {
                var prefab = FindCardPrefabByTypeID(typeID);
                if (prefab != null)
                {
                    cardPrefabs.Add(prefab);
                }
            }
            
            // 填充到enemy deck
            enemyDeckToPopulate.deck.Clear();
            enemyDeckToPopulate.deck.AddRange(cardPrefabs);
            
            // 应用保存的hpMax到敌人
            if (enemyStatusRef != null)
            {
                enemyStatusRef.hpMax = randomDeck.hpMax > 0 ? randomDeck.hpMax : 20;
                Debug.Log($"[DeckSaver] 从JSON加载了session {sessionNumber.value}的敌人卡组，共 {cardPrefabs.Count} 张卡，敌人hpMax设置为 {enemyStatusRef.hpMax}");
            }
            else
            {
                Debug.Log($"[DeckSaver] 从JSON加载了session {sessionNumber.value}的敌人卡组，共 {cardPrefabs.Count} 张卡（敌人StatusRef未设置，无法应用hpMax）");
            }
            return true;
        }

        /// <summary>
        /// 根据当前session number从默认敌人卡组列表中选择对应卡组填充
        /// session 1 -> 列表第1个，session 2 -> 列表第2个，以此类推
        /// 如果session number超出列表范围，则使用列表最后一项
        /// </summary>
        private void PopulateFromDefaultDecks()
        {
            if (defaultEnemyDecks == null || defaultEnemyDecks.Count == 0)
            {
                Debug.LogWarning($"[DeckSaver] Session {sessionNumber.value}: JSON无记录且默认卡组列表为空，无法填充enemy deck");
                return;
            }
            
            // 直接使用session number作为deck index（session 0 -> #1Deck, session 1 -> #2Deck）
            int deckIndex = sessionNumber.value;
            // 如果超出范围，使用最后一项
            if (deckIndex >= defaultEnemyDecks.Count)
            {
                deckIndex = defaultEnemyDecks.Count - 1;
            }
            var selectedDeck = defaultEnemyDecks[deckIndex];
            
            // 使用工具函数复制卡组
            UtilityFuncManagerScript.CopyGameObjectList(selectedDeck.deck, enemyDeckToPopulate.deck, true);
            Debug.Log($"[DeckSaver] Session {sessionNumber.value}: 从默认列表加载了敌人卡组: {selectedDeck.name}");
        }

        /// <summary>
        /// 删除所有保存的卡组数据
        /// </summary>
        public void WipeDeckSaves()
        {
            _currentData = new DeckData();
            
            if (File.Exists(_savePath))
            {
                try
                {
                    File.Delete(_savePath);
                    Debug.Log($"[DeckSaver] 已删除保存文件: {_savePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DeckSaver] 删除保存文件失败: {e.Message}");
                }
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 获取所有保存的卡组统计信息
        /// </summary>
        public void PrintSavedDecksInfo()
        {
            if (_currentData.savedDecks.Count == 0)
            {
                Debug.Log("[DeckSaver] 没有保存的卡组");
                return;
            }

            Debug.Log("========== 已保存卡组统计 ==========");
            
            var groupedBySession = _currentData.savedDecks
                .GroupBy(d => d.sessionNum)
                .OrderBy(g => g.Key);
            
            foreach (var group in groupedBySession)
            {
                Debug.Log($"Session {group.Key}: {group.Count()} 个卡组");
            }
            
            Debug.Log($"总计 {_currentData.savedDecks.Count} 个卡组，最后更新: {_currentData.lastUpdated}");
            Debug.Log("====================================");
        }

        #endregion

        #region 向后兼容

        // 旧方法保留，但调用新方法以保持向后兼容
        [Obsolete("使用 PopulateEnemyDeckBySessionNumber 替代")]
        public void LoadJsonToEnemyDeckSo()
        {
            PopulateEnemyDeckBySessionNumber();
        }

        #endregion

        #region Debug快捷键
        // 快捷键说明（需在 Game 视图中激活）:
        // Ctrl + S: 保存当前玩家卡组到JSON
        // Ctrl + L: 加载卡组到敌人卡组
        // Ctrl + W: 清空所有保存的卡组
        // Ctrl + D: 打印已保存卡组统计信息

        private void Update()
        {
            if (!Input.GetKey(KeyCode.LeftControl)) return;
            
            // Ctrl + S: 保存
            if (Input.GetKeyDown(KeyCode.S) && !Input.GetKey(KeyCode.LeftShift))
            {
                SavePlayerDeckToJson();
            }
            
            // Ctrl + L: 加载
            if (Input.GetKeyDown(KeyCode.L))
            {
                PopulateEnemyDeckBySessionNumber();
            }
            
            // Ctrl + W: 清空
            if (Input.GetKeyDown(KeyCode.W))
            {
                WipeDeckSaves();
            }
            
            // Ctrl + D: 打印统计
            if (Input.GetKeyDown(KeyCode.D))
            {
                PrintSavedDecksInfo();
            }
        }

        #endregion
    }
}
