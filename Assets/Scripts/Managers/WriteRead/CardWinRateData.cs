using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
    /// <summary>
    /// Single card statistics data
    /// </summary>
    [System.Serializable]
    public class CardStats
    {
        public string cardTypeID;
        public int totalCombats;
        public int wins;
        public int losses;
        
        // Calculate win rate (0-1)
        public float WinRate => totalCombats > 0 ? (float)wins / totalCombats : 0f;
        
        // Formatted output
        public override string ToString()
        {
            return $"[{cardTypeID}] : Win Rate {WinRate:P1} ({wins}W/{losses}L/{totalCombats}G)";
        }
    }

    /// <summary>
    /// Card win rate data container (for JSON serialization)
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
