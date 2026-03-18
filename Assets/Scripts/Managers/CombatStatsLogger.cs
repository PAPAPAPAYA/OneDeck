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
		
		// 卡牌 reveal 统计: cardTypeID -> list of reveal indices
		private Dictionary<string, List<int>> _cardRevealStats = new Dictionary<string, List<int>>();

		[Header("Output Settings")]
		[Tooltip("是否导出 CSV 文件")]
		public bool exportToCSV = true;
		[Tooltip("导出文件目录（相对于项目根目录）")]
		public string outputDirectory = "CombatLogs";
		[Tooltip("是否同时输出到控制台")]
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

		// 在 CombatManager 的 RevealNextCard 之后调用此方法
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
			
			// 记录卡牌 reveal 统计
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
			
			// 打印卡牌 reveal 统计 (总reveal次数 / 该卡被reveal次数)
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
				// 构建导出路径
				string projectRoot = Directory.GetParent(Application.dataPath).FullName;
				string exportDir = Path.Combine(projectRoot, outputDirectory);
				
				// 确保目录存在
				if (!Directory.Exists(exportDir))
				{
					Directory.CreateDirectory(exportDir);
				}

				// 生成文件名：CombatLog_20260101_143052.csv
				string fileName = $"CombatLog_{_currentSessionId}.csv";
				string filePath = Path.Combine(exportDir, fileName);

				// 构建 CSV 内容
				StringBuilder csv = new StringBuilder();
				
				// 写入表头
				csv.AppendLine("战斗日志导出");
				csv.AppendLine($"导出时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
				csv.AppendLine($"总翻牌次数,{records.Count}");
				csv.AppendLine($"最终己方HP,{CombatManager.Me.ownerPlayerStatusRef?.hp ?? 0}");
				csv.AppendLine($"最终敌方HP,{CombatManager.Me.enemyPlayerStatusRef?.hp ?? 0}");
				csv.AppendLine($"总回合数,{GetMaxRoundNum()}");
				csv.AppendLine();
				
				// 写入详细数据表头
				csv.AppendLine("翻牌序号,回合数,己方HP,敌方HP,己方牌组数,敌方牌组数");
				
				// 写入详细数据
				foreach (var record in records)
				{
					csv.AppendLine($"{record.revealIndex},{record.roundNum},{record.ownerHP},{record.enemyHP},{record.ownerDeckSize},{record.enemyDeckSize}");
				}
				
				// 写入卡牌 reveal 统计 (总reveal次数 / 该卡被reveal次数)
				csv.AppendLine();
				csv.AppendLine("卡牌类型,被Reveal次数,出现间隔(总次数/卡次数)");
				int totalRevealsForCSV = records.Count;
				foreach (var kvp in _cardRevealStats)
				{
					string cardTypeID = kvp.Key;
					List<int> revealIndices = kvp.Value;
					int cardRevealCount = revealIndices.Count;
					float interval = totalRevealsForCSV / (float)cardRevealCount;
					csv.AppendLine($"{cardTypeID},{cardRevealCount},{interval:F2}");
				}

				// 写入文件
				File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

				Debug.Log($"[CombatStatsLogger] 战斗日志已导出到: {filePath}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[CombatStatsLogger] 导出 CSV 失败: {ex.Message}");
			}
		}

		/// <summary>
		/// 获取有效的卡组大小（排除 Start Card 等中立卡）
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
				// 跳过中立卡，只统计属于指定玩家的卡
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
