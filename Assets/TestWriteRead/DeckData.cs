using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
    /// <summary>
    /// 卡组保存条目 - 使用cardTypeID而非GameObject实例
    /// 避免GameObject实例ID变化导致的问题
    /// </summary>
    [System.Serializable]
    public class DeckSaveEntry
    {
        /// <summary>卡组中所有卡牌的类型ID列表</summary>
        public List<string> cardTypeIDs = new();
        
        /// <summary>保存时的胜利数</summary>
        public int winAmount;
        
        /// <summary>保存时的生命值</summary>
        public int heartLeft;
        
        /// <summary>保存时的session编号</summary>
        public int sessionNum;
        
        /// <summary>保存时的最大生命值</summary>
        public int hpMax;
        
        /// <summary>保存时间戳</summary>
        public string savedAt;

        public DeckSaveEntry()
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// 卡组数据容器（用于JSON序列化）
    /// </summary>
    [System.Serializable]
    public class DeckData
    {
        /// <summary>所有保存的卡组条目</summary>
        public List<DeckSaveEntry> savedDecks = new();
        
        /// <summary>数据版本号，用于版本迁移</summary>
        public int version = 1;
        
        /// <summary>最后更新时间</summary>
        public string lastUpdated;

        public DeckData()
        {
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
