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
		private List<DeckDataStruct> _deckToSave;
		private string _savePath;

		private void Awake()
		{
			_savePath =  Application.persistentDataPath + "/deckdata.json";
		}

		private void SaveDeck(DeckData data)
		{
			var json = JsonUtility.ToJson(data, true);
			File.WriteAllText(_savePath, json);
		}

		// delete deck files
		private void WipeDeckSaves()
		{
			File.Delete(_savePath);
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
				SaveDeck(MakeDeckData());
			}

			if (Input.GetKeyDown(KeyCode.L))
			{
				var loadedDeck = LoadDeck();
				if (loadedDeck != null)
				{
					// foreach (var card in loadedDeck.deckSaved.deck)
					// {
					// 	print(card.name+"\n");
					// }
				}
			}
		}
	}
}