using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	[Serializable]
	public class CardShopStats
	{
		public string cardName;
		public int appearCount;
		public int boughtCount;
	}

	[Serializable]
	public class ShopStatsData
	{
		public List<CardShopStats> cardStats = new List<CardShopStats>();
		public int totalShopVisits;
		public int totalRerolls;
		public DateTime lastUpdateTime;
	}

	public class ShopStatsManager : MonoBehaviour
	{
		#region singleton
		public static ShopStatsManager me;
		private void Awake()
		{
			me = this;
			LoadStats();
		}
		#endregion

		[Header("Settings")]
		public string statsFileName = "shop_stats.json";
		public bool enableStats = true;

		private ShopStatsData _statsData;
		private string _filePath;

		private void Start()
		{
			_filePath = Path.Combine(Application.persistentDataPath, statsFileName);
		}

		/// <summary>
		/// 记录卡牌出现在商店中
		/// </summary>
		public void RecordCardAppeared(string cardName)
		{
			if (!enableStats) return;

			var stat = GetOrCreateCardStat(cardName);
			stat.appearCount++;
			SaveStats();
		}

		/// <summary>
		/// 记录卡牌被购买
		/// </summary>
		public void RecordCardBought(string cardName)
		{
			if (!enableStats) return;

			var stat = GetOrCreateCardStat(cardName);
			stat.boughtCount++;
			SaveStats();
		}

		/// <summary>
		/// 记录商店访问次数
		/// </summary>
		public void RecordShopVisit()
		{
			if (!enableStats) return;

			_statsData.totalShopVisits++;
			SaveStats();
		}

		/// <summary>
		/// 记录刷新次数
		/// </summary>
		public void RecordReroll()
		{
			if (!enableStats) return;

			_statsData.totalRerolls++;
			SaveStats();
		}

		/// <summary>
		/// 获取或创建卡牌统计数据
		/// </summary>
		private CardShopStats GetOrCreateCardStat(string cardName)
		{
			var stat = _statsData.cardStats.Find(s => s.cardName == cardName);
			if (stat == null)
			{
				stat = new CardShopStats
				{
					cardName = cardName,
					appearCount = 0,
					boughtCount = 0
				};
				_statsData.cardStats.Add(stat);
			}
			return stat;
		}

		/// <summary>
		/// 获取所有统计数据
		/// </summary>
		public ShopStatsData GetStats()
		{
			return _statsData;
		}

		/// <summary>
		/// 获取指定卡牌的统计数据
		/// </summary>
		public CardShopStats GetCardStats(string cardName)
		{
			return _statsData.cardStats.Find(s => s.cardName == cardName);
		}

		/// <summary>
		/// 计算购买率
		/// </summary>
		public float GetPurchaseRate(string cardName)
		{
			var stat = GetCardStats(cardName);
			if (stat == null || stat.appearCount == 0) return 0f;
			return (float)stat.boughtCount / stat.appearCount;
		}

		/// <summary>
		/// 保存统计数据到JSON
		/// </summary>
		public void SaveStats()
		{
			if (_statsData == null) return;

			_statsData.lastUpdateTime = DateTime.Now;
			_filePath = Path.Combine(Application.persistentDataPath, statsFileName);

			try
			{
				string json = JsonUtility.ToJson(_statsData, true);
				File.WriteAllText(_filePath, json);
				Debug.Log($"[ShopStatsManager] 统计已保存到: {_filePath}");
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
			_filePath = Path.Combine(Application.persistentDataPath, statsFileName);

			if (File.Exists(_filePath))
			{
				try
				{
					string json = File.ReadAllText(_filePath);
					_statsData = JsonUtility.FromJson<ShopStatsData>(json);
					if (_statsData == null)
					{
						_statsData = new ShopStatsData();
					}
					if (_statsData.cardStats == null)
					{
						_statsData.cardStats = new List<CardShopStats>();
					}
					Debug.Log($"[ShopStatsManager] 统计已加载: {_filePath}");
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
				Debug.Log($"[ShopStatsManager] 未找到现有统计文件，创建新数据");
			}
		}

		/// <summary>
		/// 重置所有统计数据
		/// </summary>
		public void ResetStats()
		{
			_statsData = new ShopStatsData();
			SaveStats();
			Debug.Log("[ShopStatsManager] 统计数据已重置");
		}

		/// <summary>
		/// 导出统计报告到可读文本
		/// </summary>
		public void ExportReport()
		{
			string reportPath = Path.Combine(Application.persistentDataPath, "shop_stats_report.txt");
			try
			{
				using (StreamWriter writer = new StreamWriter(reportPath))
				{
					writer.WriteLine("=== 商店卡牌统计报告 ===");
					writer.WriteLine($"生成时间: {DateTime.Now}");
					writer.WriteLine($"总商店访问次数: {_statsData.totalShopVisits}");
					writer.WriteLine($"总刷新次数: {_statsData.totalRerolls}");
					writer.WriteLine($"统计卡牌种类数: {_statsData.cardStats.Count}");
					writer.WriteLine();

					// 按出现次数排序
					_statsData.cardStats.Sort((a, b) => b.appearCount.CompareTo(a.appearCount));

					writer.WriteLine("--- 卡牌详细统计 ---");
					foreach (var stat in _statsData.cardStats)
					{
						float purchaseRate = stat.appearCount > 0
							? (float)stat.boughtCount / stat.appearCount * 100
							: 0;
						writer.WriteLine($"\n卡牌: {stat.cardName}");
						writer.WriteLine($"  出现次数: {stat.appearCount}");
						writer.WriteLine($"  购买次数: {stat.boughtCount}");
						writer.WriteLine($"  购买率: {purchaseRate:F2}%");
					}
				}
				Debug.Log($"[ShopStatsManager] 报告已导出: {reportPath}");
			}
			catch (Exception e)
			{
				Debug.LogError($"[ShopStatsManager] 导出报告失败: {e.Message}");
			}
		}

		private void OnApplicationQuit()
		{
			SaveStats();
		}

		private void Update()
		{
			// test reset
			if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
			{
				ResetStats();
			}
		}
	}
}