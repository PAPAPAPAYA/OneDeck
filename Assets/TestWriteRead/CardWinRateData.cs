using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
    /// <summary>
    /// 单张卡的统计数据
    /// </summary>
    [System.Serializable]
    public class CardStats
    {
        public string cardTypeID;
        public int totalCombats;
        public int wins;
        public int losses;
        
        // 计算胜率（0-1）
        public float WinRate => totalCombats > 0 ? (float)wins / totalCombats : 0f;
        
        // 格式化输出
        public override string ToString()
        {
            return $"[{cardTypeID}] : 胜率 {WinRate:P1} ({wins}胜/{losses}负/{totalCombats}场)";
        }
    }

    /// <summary>
    /// 卡胜率数据容器（用于JSON序列化）
    /// </summary>
    [System.Serializable]
    public class CardWinRateData
    {
        public List<CardStats> allCardStats = new();
        public string lastUpdated;
        
        public CardWinRateData()
        {
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
