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

		public void LoadJsonToEnemyDeckSo()
		{
			if (!switchOnSaveLoad) return; // if not switched on, return
			var loadedDeck = ReadJsonDeckData(); // save json data to local
			var randomDeckPool = new List<List<GameObject>>(); // 2d list
			if (loadedDeck != null) // null check
			{
				foreach (var deck in loadedDeck.savedDecks) // get each decks in data
				{
					// save lists of cards that match desired win amount and heart amount to randomDeckPool
					if (deck.sessionNum == sessionNumber.value)
					{
						randomDeckPool.Add(deck.theDeck);
					}
				}
				
				// populate enemy deck SO that we are about to use
				var winnerDeckList = randomDeckPool[Random.Range(0, randomDeckPool.Count)]; // get a random deck list from the 2d list randomDeckPool
				UtilityFuncManagerScript.CopyGameObjectList(winnerDeckList, enemyDeckToPopulate.deck, true);
			}
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