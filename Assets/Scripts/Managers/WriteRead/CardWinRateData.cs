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
		/// <summary>Draws where both sides ended at HP &lt;= 0 (draw plan Step 2; legacy JSON loads as 0).</summary>
		public int drawsNatural;
		/// <summary>Draws forced by an L0 budget-cap conclusion (draw plan Step 2; legacy JSON loads as 0).</summary>
		public int drawsForced;

		// Calculate win rate (0-1)
		public float WinRate => totalCombats > 0 ? (float)wins / totalCombats : 0f;

		// Formatted output
		public override string ToString()
		{
			return $"[{cardTypeID}] : Win Rate {WinRate:P1} ({wins}W/{losses}L/{drawsNatural}DN/{drawsForced}DF/{totalCombats}G)";
		}
	}

	/// <summary>
	/// Per-(card, session) win rate bucket used for the stats snapshot upload (plan §2.7).
	/// The flat allCardStats totals stay the display/CSV source of truth.
	/// </summary>
	[System.Serializable]
	public class SessionCardStats
	{
		public string cardTypeID;
		public int sessionNum;
		public int combats;
		public int wins;
		public int losses;
		/// <summary>Natural-draw count (draw plan Step 2; legacy JSON loads as 0).</summary>
		public int drawsNatural;
		/// <summary>Forced-draw count (draw plan Step 2; legacy JSON loads as 0).</summary>
		public int drawsForced;
	}

	/// <summary>
	/// Card win rate data container (for JSON serialization)
	/// </summary>
	[System.Serializable]
	public class CardWinRateData
	{
		public List<CardStats> allCardStats = new();
		public List<SessionCardStats> sessionCardStats = new();
		public string lastUpdated;

		public CardWinRateData()
		{
			lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		}
	}
}
