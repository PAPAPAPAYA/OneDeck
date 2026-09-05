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
		public int utilityBoardAppearCount; // Appear count on utility boards
		public int utilityBoardBoughtCount; // Bought count on utility boards


		// Calculate purchase rate (0-1)
		public float PurchaseRate => appearCount > 0 ? (float)boughtCount / appearCount : 0f;
		
		// Formatted output
		public override string ToString()
		{
			return $"[{cardTypeID}] {cardName}: Purchase Rate {PurchaseRate:P1} ({boughtCount} bought/{appearCount} appeared)";
		}
	}

	/// <summary>
	/// Per-(card, session) shop bucket used for the stats snapshot upload (plan §2.7).
	/// The flat cardStats totals stay the display/CSV source of truth.
	/// </summary>
	[Serializable]
	public class ShopSessionStats
	{
		public string cardTypeID;
		public int sessionNum;
		public int appear;
		public int bought;
		public int utilAppear;
		public int utilBought;
	}

	/// <summary>
	/// Shop stats data container (for JSON serialization)
	/// </summary>
	[Serializable]
	public class ShopStatsData
	{
		public List<CardShopStats> cardStats = new List<CardShopStats>();
		public List<ShopSessionStats> sessionCardStats = new List<ShopSessionStats>();
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

		/// <summary>Test seam: when set, overrides the persistentDataPath directory for the stats JSON/CSV files.</summary>
		public static string OverrideDirectoryForTests;

		// Per-visit staging buffer (2026-09-05): shop stats only reach the lifetime counters
		// via CommitStagedVisit(), which PhaseManager calls on shop exit - the only path into
		// combat. A run that never enters combat (quit mid-shop) therefore contributes nothing
		// to shop_stats.json or the stats snapshot upload.
		[Serializable]
		private class StagedCardStats
		{
			public string cardTypeID;
			public string cardName;
			public int appear;
			public int bought;
			public int utilAppear;
			public int utilBought;
		}
		private readonly List<StagedCardStats> _stagedCards = new List<StagedCardStats>();
		private int _stagedRerolls;
		private bool _stagedVisit;

		private void Awake()
		{
			Me = this;
			EnsureInitialized();

			if (resetOnStart)
			{
				ResetStats();
			}
		}

		// Lazy init: Awake does not run for AddComponent in EditMode tests, so every public
		// entry point initializes on demand; idempotent in play mode where Awake pre-inits.
		private bool _initialized;

		private void EnsureInitialized()
		{
			if (_initialized) return;
			string dataDir = OverrideDirectoryForTests ?? Application.persistentDataPath;
			_jsonPath = Path.Combine(dataDir, "shop_stats.json");
			_csvPath = Path.Combine(dataDir, "shop_stats.csv");
			LoadStats();
			_initialized = true;
		}

		/// <summary>
		/// Record card appeared in shop (staged; committed on shop exit)
		/// </summary>
		public void RecordCardAppeared(string cardTypeID, string cardName = "", bool onUtilityBoard = false)
		{
			if (!enableStats) return;
			EnsureInitialized();

			var stat = GetOrCreateStagedStat(cardTypeID, cardName);
			stat.appear++;
			if (onUtilityBoard) stat.utilAppear++;
		}

		/// <summary>
		/// Record card bought (staged; committed on shop exit)
		/// </summary>
		public void RecordCardBought(string cardTypeID, string cardName = "", bool onUtilityBoard = false)
		{
			if (!enableStats) return;
			EnsureInitialized();

			var stat = GetOrCreateStagedStat(cardTypeID, cardName);
			stat.bought++;
			if (onUtilityBoard) stat.utilBought++;
		}

		/// <summary>
		/// Record shop visit count (staged; committed on shop exit)
		/// </summary>
		public void RecordShopVisit()
		{
			if (!enableStats) return;
			EnsureInitialized();

			_stagedVisit = true;
		}

		/// <summary>
		/// Record reroll count (staged; committed on shop exit)
		/// </summary>
		public void RecordReroll()
		{
			if (!enableStats) return;
			EnsureInitialized();

			_stagedRerolls++;
		}

		/// <summary>
		/// Merge the staged visit into the lifetime counters. Called by PhaseManager on
		/// shop exit, which only fires on the path into combat, so every committed visit
		/// is followed by a fight and a run that never enters combat contributes nothing.
		/// </summary>
		public void CommitStagedVisit()
		{
			EnsureInitialized();
			if (_statsData == null) return;
			if (_stagedCards.Count == 0 && _stagedRerolls == 0 && !_stagedVisit) return;

			int sessionNum = StatsSnapshotUploader.CurrentSessionNum();
			foreach (StagedCardStats staged in _stagedCards)
			{
				// flat card totals (display / CSV source of truth)
				CardShopStats stat = GetOrCreateCardStat(staged.cardTypeID, staged.cardName);
				stat.appearCount += staged.appear;
				stat.boughtCount += staged.bought;
				stat.utilityBoardAppearCount += staged.utilAppear;
				stat.utilityBoardBoughtCount += staged.utilBought;

				// per-(card, session) bucket for the stats snapshot upload (plan §2.7)
				ShopSessionStats bucket = _statsData.sessionCardStats.Find(
					s => s.cardTypeID == staged.cardTypeID && s.sessionNum == sessionNum);
				if (bucket == null)
				{
					bucket = new ShopSessionStats
					{
						cardTypeID = staged.cardTypeID,
						sessionNum = sessionNum,
						appear = 0,
						bought = 0,
						utilAppear = 0,
						utilBought = 0
					};
					_statsData.sessionCardStats.Add(bucket);
				}
				bucket.appear += staged.appear;
				bucket.bought += staged.bought;
				bucket.utilAppear += staged.utilAppear;
				bucket.utilBought += staged.utilBought;
			}
			if (_stagedVisit) _statsData.totalShopVisits++;
			_statsData.totalRerolls += _stagedRerolls;

			_stagedCards.Clear();
			_stagedRerolls = 0;
			_stagedVisit = false;

			_pendingSave = true;
			StatsSnapshotUploader.MarkDirty();
		}

		/// <summary>
		/// Save immediately (call at appropriate times, such as when leaving shop)
		/// </summary>
		public void Flush()
		{
			EnsureInitialized();
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
		/// Get or create a card entry in the per-visit staging buffer.
		/// </summary>
		private StagedCardStats GetOrCreateStagedStat(string cardTypeID, string cardName = "")
		{
			var stat = _stagedCards.Find(s => s.cardTypeID == cardTypeID);
			if (stat == null)
			{
				stat = new StagedCardStats
				{
					cardTypeID = cardTypeID,
					cardName = cardName
				};
				_stagedCards.Add(stat);
			}
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
			EnsureInitialized();
			return _statsData.cardStats.Find(s => s.cardTypeID == cardTypeID);
		}

		/// <summary>
		/// Per-session buckets for the stats snapshot upload (plan §2.7).
		/// </summary>
		public List<ShopSessionStats> GetSessionStatsForUpload()
		{
			EnsureInitialized();
			return _statsData.sessionCardStats;
		}

		/// <summary>
		/// Lifetime meta counters for the stats snapshot upload (plan §2.7).
		/// </summary>
		public int GetTotalShopVisitsForUpload()
		{
			EnsureInitialized();
			return _statsData.totalShopVisits;
		}

		public int GetTotalRerollsForUpload()
		{
			EnsureInitialized();
			return _statsData.totalRerolls;
		}

		/// <summary>
		/// Calculate purchase rate
		/// </summary>
		public float GetPurchaseRate(string cardTypeID)
		{
			EnsureInitialized();
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
			EnsureInitialized();
			if (_statsData == null) return;

			_statsData.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

			try
			{
				string json = JsonUtility.ToJson(_statsData, true);
				File.WriteAllText(_jsonPath, json);
				
				if (printOnSave)
				{
					// Debug.Log($"[ShopStatsManager] Stats saved: {_jsonPath}");
				}
			}
			catch (Exception e)
			{
				// Debug.LogError($"[ShopStatsManager] Failed to save stats: {e.Message}");
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
						// Ensure lists are not null (legacy JSON may predate the session buckets)
						if (_statsData.cardStats == null)
							_statsData.cardStats = new List<CardShopStats>();
						if (_statsData.sessionCardStats == null)
							_statsData.sessionCardStats = new List<ShopSessionStats>();
					}
				}
				catch (Exception e)
				{
					// Debug.LogError($"[ShopStatsManager] Failed to load stats: {e.Message}");
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
			EnsureInitialized();
			if (_statsData.cardStats.Count == 0)
			{
				// Debug.LogWarning("[ShopStatsManager] No data to export");
				return;
			}

			var sb = new StringBuilder();
			
			// CSV header
			sb.AppendLine("CardTypeID,CardName,AppearCount,BoughtCount,PurchaseRate,UtilityBoardAppear,UtilityBoardBought,LastUpdated");
			
			// Sort by purchase rate
			var sortedStats = _statsData.cardStats
				.OrderByDescending(s => s.PurchaseRate)
				.ThenByDescending(s => s.appearCount)
				.ToList();
			
			foreach (var stat in sortedStats)
			{
				sb.AppendLine($"{stat.cardTypeID},{stat.cardName},{stat.appearCount},{stat.boughtCount},{stat.PurchaseRate:F4},{stat.utilityBoardAppearCount},{stat.utilityBoardBoughtCount},{_statsData.lastUpdated}");
			}

			try
			{
				File.WriteAllText(_csvPath, sb.ToString(), Encoding.UTF8);
				// Debug.Log($"[ShopStatsManager] CSV exported: {_csvPath}");
			}
			catch (Exception e)
			{
				// Debug.LogError($"[ShopStatsManager] CSV export failed: {e.Message}");
			}
		}

		#endregion

		#region Query Interface

		/// <summary>
		/// Print all stats report to console
		/// </summary>
		public void PrintReport()
		{
			EnsureInitialized();
			if (_statsData.cardStats.Count == 0)
			{
				// Debug.Log("[ShopStatsManager] No data yet");
				return;
			}

			// Debug.Log("========== Shop Card Statistics Report ==========");
			// Debug.Log($"Total shop visits: {_statsData.totalShopVisits}");
			// Debug.Log($"Total rerolls: {_statsData.totalRerolls}");
			// Debug.Log($"Card types tracked: {_statsData.cardStats.Count}");
			// Debug.Log("");
			
			var sortedStats = _statsData.cardStats
				.OrderByDescending(s => s.PurchaseRate)
				.ThenByDescending(s => s.appearCount)
				.ToList();

			foreach (var stat in sortedStats)
			{
				// Debug.Log(stat.ToString());
			}
			
			// Debug.Log($"Last updated: {_statsData.lastUpdated}");
			// Debug.Log("======================================");
		}

		/// <summary>
		/// Reset all stats
		/// </summary>
		public void ResetStats()
		{
			EnsureInitialized();
			_statsData = new ShopStatsData();
			_pendingSave = false;
			_stagedCards.Clear();
			_stagedRerolls = 0;
			_stagedVisit = false;
			
			if (File.Exists(_jsonPath))
			{
				File.Delete(_jsonPath);
			}
			if (File.Exists(_csvPath))
			{
				File.Delete(_csvPath);
			}
			
			// Debug.Log("[ShopStatsManager] Stats reset");
		}

		/// <summary>
		/// Build a human-readable report of all tracked shop card stats.
		/// </summary>
		public string GetAllStatsReportString()
		{
			if (_statsData == null || _statsData.cardStats.Count == 0)
			{
				return "No shop stats data yet.";
			}

			var sb = new StringBuilder();
			sb.AppendLine("=== SHOP STATS ===");
			sb.AppendLine($"Total shop visits: {_statsData.totalShopVisits}");
			sb.AppendLine($"Total rerolls: {_statsData.totalRerolls}");

		int totalAppears = _statsData.cardStats.Sum(s => s.appearCount);
		int utilityAppears = _statsData.cardStats.Sum(s => s.utilityBoardAppearCount);
		int utilityBought = _statsData.cardStats.Sum(s => s.utilityBoardBoughtCount);
		sb.AppendLine($"Utility board share: {(totalAppears > 0 ? (float)utilityAppears / totalAppears : 0f):P1} offers ({utilityAppears}/{totalAppears}), {utilityBought} bought on utility boards");

			var sortedStats = _statsData.cardStats
				.OrderByDescending(s => s.PurchaseRate)
				.ThenByDescending(s => s.appearCount)
				.ToList();

			foreach (var stat in sortedStats)
			{
				sb.AppendLine(stat.ToString());
			}

			sb.AppendLine($"Total cards tracked: {_statsData.cardStats.Count}");
			return sb.ToString();
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
