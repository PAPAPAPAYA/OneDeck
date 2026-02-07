using System.Collections.Generic;
using System.IO;
using UnityEngine;

// script responsible for: 
// 1. save current player deck to json
// 2. load matching round number deck randomly to enemy deck
// 3. wipe saved decks
// 4. enable/disable save/load
// * beware of changing instance id, occured once, still don't know why
namespace TestWriteRead
{
	public class DeckSaver : MonoBehaviour
	{
		[Header("system switch")]
		public bool switchOnSaveLoad = false;
		[Header("deck info refs")]
		public DeckSO playerDeck; // the deckSO to add to struct to save
		public IntSO winAmount; // win amount to add to struct to save / player current win amount (currently not used)
		public IntSO heartLeft; // heart left to add to struct to save / player current heart amount (currently not used)
		public IntSO sessionNumber; // round num to add to struct to save / player current round number
		public DeckSO enemyDeckToPopulate; // enemy deck SO ref to populate
		[Header("default enemy decks")]
		[Tooltip("当JSON中没有对应session的卡组时，从此列表中随机选择一个")]
		public List<DeckSO> defaultEnemyDecks; // 默认敌人卡组配置列表
		// local variables
		private readonly List<DeckDataStruct> _decksToSave = new(); // need to new so we can read its count; tracks already saved decks, so we don't override them when saving
		private string _savePath;

		private void Awake()
		{
			_savePath =  Application.persistentDataPath + "/deckdata.json";
		}

		private void SaveDeckToLocalList()
		{
			var deckStruct = new DeckDataStruct(playerDeck.deck, winAmount.value, heartLeft.value, sessionNumber.value);
			_decksToSave.Add(deckStruct);
		}
		
		// use _deckToSave to make a deck data
		private DeckData MakeDeckData()
		{
			var deckDataToSave = new DeckData
			{
				savedDecks = _decksToSave
			};
			return deckDataToSave;
		}

		private void SaveDeckDataToJson(DeckData data)
		{
			var json = JsonUtility.ToJson(data, true);
			File.WriteAllText(_savePath, json);
			print("Saved to " + _savePath);
		}

		// delete deck files
		private void WipeDeckSaves()
		{
			File.Delete(_savePath);
			print("Deleted " + _savePath);
		}

		private DeckData ReadJsonDeckData()
		{
			if (File.Exists(_savePath))
			{
				var json = File.ReadAllText(_savePath);
				return JsonUtility.FromJson<DeckData>(json);
			}
			return null;
		}

		public void SavePlayerDeckToJson()
		{
			if (!switchOnSaveLoad) return; // if not switched on, return
			if (File.Exists(_savePath) && // if save file already exists
			    _decksToSave.Count == 0) // if local list is empty
			{
				// then copy save file decks to local list (so that when saving we are not overriding original save file
				UtilityFuncManagerScript.CopyList(ReadJsonDeckData().savedDecks, _decksToSave, false);
			}
			SaveDeckToLocalList(); // first save to local list _decksToSave
			SaveDeckDataToJson(MakeDeckData()); // then write to json
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
			
			// JSON没有匹配时，从默认列表随机选择
			PopulateFromDefaultDecks();
		}

		/// <summary>
		/// 尝试从JSON文件加载匹配当前session number的卡组
		/// </summary>
		/// <returns>是否成功加载</returns>
		private bool TryLoadFromJson()
		{
			if (!switchOnSaveLoad) return false;
			
			var loadedDeck = ReadJsonDeckData();
			if (loadedDeck == null) return false;
			
			var randomDeckPool = new List<List<GameObject>>();
			foreach (var deck in loadedDeck.savedDecks)
			{
				if (deck.sessionNum == sessionNumber.value)
				{
					randomDeckPool.Add(deck.theDeck);
				}
			}
			
			// 如果没有匹配到任何卡组，返回false
			if (randomDeckPool.Count == 0) return false;
			
			// 随机选择一个匹配卡组填充到enemyDeck
			var winnerDeckList = randomDeckPool[Random.Range(0, randomDeckPool.Count)];
			UtilityFuncManagerScript.CopyGameObjectList(winnerDeckList, enemyDeckToPopulate.deck, true);
			Debug.Log($"[DeckSaver] 从JSON加载了session {sessionNumber.value}的敌人卡组");
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
			
			// 根据session number选择卡组（session 1对应索引0）
			int deckIndex = sessionNumber.value - 1;
			// 如果超出范围，使用最后一项
			if (deckIndex >= defaultEnemyDecks.Count)
			{
				deckIndex = defaultEnemyDecks.Count - 1;
			}
			var selectedDeck = defaultEnemyDecks[deckIndex];
			UtilityFuncManagerScript.CopyGameObjectList(selectedDeck.deck, enemyDeckToPopulate.deck, true);
			Debug.Log($"[DeckSaver] Session {sessionNumber.value}: 从默认列表加载了敌人卡组: {selectedDeck.name}");
		}

		// 旧方法保留，但调用新方法以保持向后兼容
		public void LoadJsonToEnemyDeckSo()
		{
			PopulateEnemyDeckBySessionNumber();
		}

		private void Update()
		{
			// test save
			if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl))
			{
				SavePlayerDeckToJson();
			}

			// test load
			if (Input.GetKeyDown(KeyCode.L) && Input.GetKey(KeyCode.LeftControl))
			{
				LoadJsonToEnemyDeckSo();
			}

			// test wipe
			if (Input.GetKeyDown(KeyCode.W) && Input.GetKey(KeyCode.LeftControl))
			{
				WipeDeckSaves();
			}
		}
	}
}