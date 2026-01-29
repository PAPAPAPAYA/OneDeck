using System.Collections.Generic;
using DefaultNamespace;
using UnityEditor;
using UnityEngine;

public class CardMaker : EditorWindow
{
	private string _cardName = "NewCard";
	private string _cardDescription = "Enter card description here";
	private bool _takeUpSpaceInDeck = true;
	private int _cardPrice = 2;
	private List<GameEvent> _gameEvents =  new List<GameEvent>();
	private int _numberOfEventListeners = 0;
	private List<GameObject> _effectObjects = new List<GameObject>();


	[MenuItem("Tools/Card Maker")]
	public static void ShowWindow()
	{
		GetWindow<CardMaker>("Card Maker");
	}

	private void OnGUI()
	{
		GUILayout.Label("Card Maker", EditorStyles.boldLabel);

		_cardName = EditorGUILayout.TextField("Card Name", _cardName);
		_cardDescription = EditorGUILayout.TextArea(_cardDescription);
		_takeUpSpaceInDeck = EditorGUILayout.Toggle("Take Up Space", _takeUpSpaceInDeck);
		_cardPrice = EditorGUILayout.IntField("Card Price", _cardPrice);
		for (int i = 0; i < _gameEvents.Count; i++)
		{
			_gameEvents[i] = (GameEvent)EditorGUILayout.ObjectField("Game Event", _gameEvents[i], typeof(GameEvent), false);
		}
		for (int i = 0; i < _effectObjects.Count; i++)
		{
			_effectObjects[i] =  (GameObject)EditorGUILayout.ObjectField("Effect Object", _effectObjects[i], typeof(GameObject), false);
		}
		
		// button prompt to new event listener
		if (GUILayout.Button("Add event listener"))
		{
			_gameEvents.Add(null);
			_effectObjects.Add(null);
			_numberOfEventListeners =  _gameEvents.Count;
		}
		if (GUILayout.Button("Remove event listener"))
		{
			if (_numberOfEventListeners <= 1) return; // doesn't let you create a card without event listener
			_gameEvents.RemoveAt(_gameEvents.Count - 1);
			_effectObjects.RemoveAt(_effectObjects.Count - 1);
			_numberOfEventListeners =  _gameEvents.Count;
		}
		
		// button prompt to create
		if (GUILayout.Button("Create Card"))
		{
			CreateCardPrefab();
		}
	}
	
	private void CreateCardPrefab()
	{
		// new a card
		var newCard = new GameObject(_cardName)
		{
			transform =
			{
				position = Vector3.zero
			}
		};
		
		// add components
		newCard.AddComponent<CardScript>();
		var newCardScript = newCard.GetComponent<CardScript>();
		for (var i = 0; i < _numberOfEventListeners; i++)
		{
			newCard.AddComponent<GameEventListener>();
		}
		var newCardListeners = newCard.GetComponents<GameEventListener>();
		
		// apply settings
		newCardScript.cardDesc =  _cardDescription;
		newCardScript.price = _cardPrice;
		newCardScript.takeUpSpace = _takeUpSpaceInDeck;
		var newEffectObjects = new List<GameObject>();
		for (int i = 0; i < _effectObjects.Count; i++)
		{
			newEffectObjects.Add(Instantiate(_effectObjects[i], newCard.transform));
		}
		for (int i = 0; i < _numberOfEventListeners; i++)
		{
			newCardListeners[i].@event =  _gameEvents[i];
			//UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(newCardListeners[i].response, _effectObjects[i].GetComponent<CostNEffectContainer>().InvokeEffectEvent);
			UnityEditor.Events.UnityEventTools.AddPersistentListener(newCardListeners[i].response, _effectObjects[i].GetComponent<CostNEffectContainer>().InvokeEffectEvent);
		}
		
		SaveCardPrefab(newCard);
	}

	private void SaveCardPrefab(GameObject card)
	{
		string path = "Assets/Prefabs/Cards/" + _cardName + ".prefab";
		
		string directory = System.IO.Path.GetDirectoryName(path);
		if (!System.IO.Directory.Exists(directory))
		{
			if (directory != null) System.IO.Directory.CreateDirectory(directory);
		}
		
		PrefabUtility.SaveAsPrefabAsset(card, path);
		DestroyImmediate(card);
		
		AssetDatabase.Refresh();
	}


	private static void ConfigureNewCard()
	{
		var newCard = new GameObject("Card");

		newCard.AddComponent<CardScript>();
		newCard.AddComponent<GameEventListener>();
	}
}