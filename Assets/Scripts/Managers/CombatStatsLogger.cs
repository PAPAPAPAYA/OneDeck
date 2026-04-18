using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	public class CombatStatsLogger : MonoBehaviour
	{
		[Serializable]
		public class CombatStatsRecord
		{
			public int revealIndex;
			public int ownerHP;
			public int enemyHP;
			public int ownerDeckSize;
			public int enemyDeckSize;
			public int roundNum;

			public override string ToString()
			{
				return $"[Reveal {revealIndex}] Round {roundNum}: " +
				       $"Owner HP={ownerHP}, Enemy HP={enemyHP}, " +
				       $"Owner Deck={ownerDeckSize}, Enemy Deck={enemyDeckSize}";
			}
		}

		[Header("References")]
		public GameEvent onAnyCardRevealed;
		public BoolSO combatFinished;

		[Header("Data")]
		public List<CombatStatsRecord> records = new List<CombatStatsRecord>();
		
		// Card reveal stats: cardTypeID -> list of reveal indices
		private Dictionary<string, List<int>> _cardRevealStats = new Dictionary<string, List<int>>();

		[Header("Output Settings")]
		[Tooltip("Whether to export CSV file")]
		public bool exportToCSV = true;
		[Tooltip("Export file directory (relative to project root)")]
		public string outputDirectory = "CombatLogs";
		[Tooltip("Whether to also output to console")]
		public bool printToConsole = true;

		private int _revealIndex = 0;
		private bool _hasPrintedResults = false;
		private string _currentSessionId;

		private void OnEnable()
		{
			_revealIndex = 0;
			_hasPrintedResults = false;
			records.Clear();
			_cardRevealStats.Clear();
			_currentSessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		}

		private void Update()
		{
			if (combatFinished != null && combatFinished.value && !_hasPrintedResults)
			{
				if (printToConsole)
				{
					PrintCombatResults();
				}
				if (exportToCSV)
				{
					ExportToCSV();
				}
				_hasPrintedResults = true;
			}
		}

		// Call this method after CombatManager's RevealNextCard
		public void OnCardRevealed(CardScript cardRevealed)
		{
			if (CombatManager.Me == null) return;

			_revealIndex++;

			var record = new CombatStatsRecord
			{
				revealIndex = _revealIndex,
				ownerHP = CombatManager.Me.ownerPlayerStatusRef?.hp ?? 0,
				enemyHP = CombatManager.Me.enemyPlayerStatusRef?.hp ?? 0,
				ownerDeckSize = GetEffectiveDeckSize(true),
				enemyDeckSize = GetEffectiveDeckSize(false),
				roundNum = CombatManager.Me.roundNumRef?.value ?? 0
			};

			records.Add(record);
			
			// Record card reveal stats
			if (cardRevealed != null && !string.IsNullOrEmpty(cardRevealed.cardTypeID))
			{
				string cardTypeID = cardRevealed.cardTypeID;
				if (!_cardRevealStats.ContainsKey(cardTypeID))
				{
					_cardRevealStats[cardTypeID] = new List<int>();
				}
				_cardRevealStats[cardTypeID].Add(_revealIndex);
			}
			
			Debug.Log($"[CombatStatsLogger] {record}");
		}

		private void PrintCombatResults()
		{
			Debug.Log("========== COMBAT STATS REPORT ==========");
			Debug.Log($"Total reveals: {records.Count}");
			Debug.Log($"Final Owner HP: {CombatManager.Me.ownerPlayerStatusRef?.hp ?? 0}");
			Debug.Log($"Final Enemy HP: {CombatManager.Me.enemyPlayerStatusRef?.hp ?? 0}");
			Debug.Log("------------------------------------------");
			
			foreach (var record in records)
			{
				Debug.Log(record.ToString());
			}
			
			Debug.Log("========== END OF COMBAT STATS ==========");
			
			// Print card reveal stats (total reveals / card reveal count)
			Debug.Log("========== CARD REVEAL STATS ==========");
			int totalReveals = records.Count;
			foreach (var kvp in _cardRevealStats)
			{
				string cardTypeID = kvp.Key;
				List<int> revealIndices = kvp.Value;
				int cardRevealCount = revealIndices.Count;
				float interval = totalReveals / (float)cardRevealCount;
				Debug.Log($"Card {cardTypeID}: revealed {cardRevealCount} times, interval (total/card): {interval:F2}");
			}
			Debug.Log("========== END OF CARD REVEAL STATS ==========");
		}

		private void ExportToCSV()
		{
			if (records.Count == 0)
			{
				Debug.LogWarning("[CombatStatsLogger] No records to export.");
				return;
			}

			try
			{
				// Build export path
				string projectRoot = Directory.GetParent(Application.dataPath).FullName;
				string exportDir = Path.Combine(projectRoot, outputDirectory);
				
				// Ensure directory exists
				if (!Directory.Exists(exportDir))
				{
					Directory.CreateDirectory(exportDir);
				}

				// Generate file name: CombatLog_20260101_143052.csv
				string fileName = $"CombatLog_{_currentSessionId}.csv";
				string filePath = Path.Combine(exportDir, fileName);

				// Build CSV content
				StringBuilder csv = new StringBuilder();
				
				// Write header
				csv.AppendLine("Combat Log Export");
				csv.AppendLine($"Export Time,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
				csv.AppendLine($"Total Reveals,{records.Count}");
				csv.AppendLine($"Final Owner HP,{CombatManager.Me.ownerPlayerStatusRef?.hp ?? 0}");
				csv.AppendLine($"Final Enemy HP,{CombatManager.Me.enemyPlayerStatusRef?.hp ?? 0}");
				csv.AppendLine($"Total Rounds,{GetMaxRoundNum()}");
				csv.AppendLine();
				
				// Write detailed data header
				csv.AppendLine("Reveal Index,Round,Owner HP,Enemy HP,Owner Deck,Enemy Deck");
				
				// Write detailed data
				foreach (var record in records)
				{
					csv.AppendLine($"{record.revealIndex},{record.roundNum},{record.ownerHP},{record.enemyHP},{record.ownerDeckSize},{record.enemyDeckSize}");
				}
				
				// Write card reveal stats (total reveals / card reveal count)
				csv.AppendLine();
				csv.AppendLine("Card Type,Reveal Count,Interval(Total/Card)");
				int totalRevealsForCSV = records.Count;
				foreach (var kvp in _cardRevealStats)
				{
					string cardTypeID = kvp.Key;
					List<int> revealIndices = kvp.Value;
					int cardRevealCount = revealIndices.Count;
					float interval = totalRevealsForCSV / (float)cardRevealCount;
					csv.AppendLine($"{cardTypeID},{cardRevealCount},{interval:F2}");
				}

				// Write to file
				File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

				Debug.Log($"[CombatStatsLogger] Combat log exported to: {filePath}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[CombatStatsLogger] CSV export failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get effective deck size (excluding neutral cards like Start Card)
		/// </summary>
		private int GetEffectiveDeckSize(bool isOwner)
		{
			if (CombatManager.Me?.combinedDeckZone == null) return 0;
			
			var targetStatusRef = isOwner ? CombatManager.Me.ownerPlayerStatusRef : CombatManager.Me.enemyPlayerStatusRef;
			int count = 0;
			
			foreach (var card in CombatManager.Me.combinedDeckZone)
			{
				if (card == null) continue;
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				// Skip neutral cards, only count cards belonging to the specified player
				if (!CombatManager.ShouldSkipEffectProcessing(cardScript) && cardScript.myStatusRef == targetStatusRef)
				{
					count++;
				}
			}
			return count;
		}

		private int GetMaxRoundNum()
		{
			if (records.Count == 0) return 0;
			int maxRound = 0;
			foreach (var record in records)
			{
				if (record.roundNum > maxRound)
				{
					maxRound = record.roundNum;
				}
			}
			return maxRound;
		}
	}
}
