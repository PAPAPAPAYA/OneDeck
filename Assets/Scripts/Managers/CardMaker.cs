using DefaultNamespace;
using UnityEditor;
using UnityEngine;

public class CardMaker : EditorWindow
{
	private string _cardName = "NewCard";
	private string _cardDescription = "Enter card description here";
	private bool _takeUpSpaceInDeck = true;
	private int _cardPrice = 2;
	private int _numberOfEventListeners = 1;
	private GameEvent _gameEvent;

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
		_numberOfEventListeners = EditorGUILayout.IntField("Number of EventListeners", _numberOfEventListeners);
		_gameEvent =  EditorGUILayout.ObjectField("Game Event", _gameEvent, typeof(GameEvent), false) as GameEvent;
		
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
		newCard.AddComponent<GameEventListener>();
		var newCardScript = newCard.GetComponent<CardScript>();
		var newCardListener = newCard.GetComponent<GameEventListener>();
		
		// apply settings
		newCardScript.cardDesc =  _cardDescription;
		newCardScript.price = _cardPrice;
		newCardScript.takeUpSpace = _takeUpSpaceInDeck;
		newCardListener.@event =  _gameEvent;
		
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