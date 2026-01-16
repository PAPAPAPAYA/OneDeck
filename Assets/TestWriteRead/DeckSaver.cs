using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NUnit.Framework;
using UnityEngine;

// implement func to capture the current deck and win & heart amount, add it to the list
// read json and add to list
// so that the list contains all the decks saved before this game
// save the list to json
namespace TestWriteRead
{
	public class DeckSaver : MonoBehaviour
	{
		[Header("deck info refs")]
		public DeckSO playerDeck; // the deckSO to add to struct to save
		public IntSO winAmount; // win amount to add to struct to save
		public IntSO heartLeft; // heart left to add to struct to save
		public List<DeckDataStruct> _deckToSave =  new List<DeckDataStruct>();
		private string _savePath;

		private void Awake()
		{
			_savePath =  Application.persistentDataPath + "/deckdata.json";
		}

		private void SaveDeckToLocalList()
		{
			var deckStruct = new DeckDataStruct(playerDeck.deck, winAmount.value, heartLeft.value);
			_deckToSave.Add(deckStruct);
		}

		private void SaveDeck(DeckData data)
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

		private DeckData LoadDeck()
		{
			if (File.Exists(_savePath))
			{
				var json = File.ReadAllText(_savePath);
				return JsonUtility.FromJson<DeckData>(json);
			}
			return null;
		}

		private DeckData MakeDeckData()
		{
			var deckDataToSave = new DeckData
			{
				savedDecks = _deckToSave
			};
			return deckDataToSave;
		}

		private void Update()
		{
			// testing only
			if (Input.GetKeyDown(KeyCode.S))
			{
				if (File.Exists(_savePath) && // if save file already exists
				    _deckToSave.Count == 0) // if local list is empty
				{
					UtilityFuncManagerScript.CopyList(LoadDeck().savedDecks, _deckToSave, false); // then copy save file decks to local list (so that when saving we are not overriding original save file
				}
				SaveDeckToLocalList();
				SaveDeck(MakeDeckData());
			}

			if (Input.GetKeyDown(KeyCode.L))
			{
				var loadedDeck = LoadDeck();
				if (loadedDeck != null)
				{
					foreach (var deck in loadedDeck.savedDecks)
					{
						foreach (var card in deck.theDeck)
						{
							print(card.name+"\n");
						}
					}
				}
			}

			if (Input.GetKeyDown(KeyCode.W))
			{
				WipeDeckSaves();
			}
		}
	}
}