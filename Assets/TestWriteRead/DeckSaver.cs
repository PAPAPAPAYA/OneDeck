using System;
using System.IO;
using System.Net;
using UnityEngine;

namespace TestWriteRead
{
	public class DeckSaver : MonoBehaviour
	{
		public DeckSO playerDeck;
		private string _savePath;

		private void Awake()
		{
			_savePath =  Application.persistentDataPath + "/deckdata.json";
		}

		public void SaveDeck(DeckData data)
		{
			var json = JsonUtility.ToJson(data, true);
			File.WriteAllText(_savePath, json);
		}

		public DeckData LoadDeck()
		{
			if (File.Exists(_savePath))
			{
				var json = File.ReadAllText(_savePath);
				return JsonUtility.FromJson<DeckData>(json);
			}
			return null;
		}

		public DeckData MakeDeckData(DeckSO deckToSave)
		{
			var deckDataToSave = new DeckData
			{
				deckSaved = deckToSave
			};
			return deckDataToSave;
		}

		private void Update()
		{
			// testing only
			if (Input.GetKeyDown(KeyCode.S))
			{
				SaveDeck(MakeDeckData(playerDeck));
			}

			if (Input.GetKeyDown(KeyCode.L))
			{
				DeckData loadedDeck = LoadDeck();
				if (loadedDeck != null)
				{
					foreach (var card in loadedDeck.deckSaved.deck)
					{
						print(card.name+"\n");
					}
				}
			}
		}
	}
}