using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
    /// <summary>
    /// Deck save entry - uses cardTypeID instead of GameObject instances
    /// Avoids issues caused by GameObject instance ID changes
    /// </summary>
    [System.Serializable]
    public class DeckSaveEntry
    {
        /// <summary>List of all card type IDs in the deck</summary>
        public List<string> cardTypeIDs = new();
        
        /// <summary>Win count when saved</summary>
        public int winAmount;
        
        /// <summary>Hearts when saved</summary>
        public int heartLeft;
        
        /// <summary>Session number when saved</summary>
        public int sessionNum;
        
        /// <summary>Max HP when saved</summary>
        public int hpMax;
        
        /// <summary>Save timestamp</summary>
        public string savedAt;

        public DeckSaveEntry()
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// Deck data container (for JSON serialization)
    /// </summary>
    [System.Serializable]
    public class DeckData
    {
        /// <summary>All saved deck entries</summary>
        public List<DeckSaveEntry> savedDecks = new();
        
        /// <summary>Data version number, for version migration</summary>
        public int version = 1;
        
        /// <summary>Last updated time</summary>
        public string lastUpdated;

        public DeckData()
        {
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
