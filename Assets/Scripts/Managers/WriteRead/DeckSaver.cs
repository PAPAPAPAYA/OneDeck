using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

// script responsible for:
// 1. snapshot the current player deck to the async-PvP server (cardTypeID-based)
// 2. populate the enemy deck: debug > server ghost > default pool
namespace TestWriteRead
{
	/// <summary>
	/// Deck save system - uses cardTypeID instead of GameObject instances to store decks, avoiding instance ID change issues
	/// </summary>
	public class DeckSaver : MonoBehaviour
	{
		#region Pool Entry
		/// <summary>
		/// A pool of enemy decks available for one session.
		/// One deck is randomly selected when populating the enemy deck.
		/// </summary>
		[System.Serializable]
		public class EnemyDeckPoolEntry
		{
			[Tooltip("Decks available for this session; one will be randomly selected")]
			public List<DeckSO> decks = new List<DeckSO>();
		}
		#endregion

		#region HP Bonus Entry
		/// <summary>
		/// Defines a card type that grants bonus HP/HPMax to the enemy when present in the enemy deck.
		/// </summary>
		[System.Serializable]
		public class EnemyDeckHpBonusEntry
		{
			[Tooltip("Card type ID to detect in the enemy deck")]
			public string cardTypeID;

			[Tooltip("HP/HPMax bonus granted for each matching card")]
			public int hpBonusPerCard = 1;
		}
		#endregion

		#region SINGLETON
		public static DeckSaver Me;

		private void Awake()
		{
			Me = this;
		}
		#endregion

		[Header("Deck Info Refs")]
		public DeckSO playerDeck; // Player deck reference
		public IntSO winAmount; // Current win count
		public IntSO heartLeft; // Current hearts
		public IntSO sessionNumber; // Current session number
		public DeckSO enemyDeckToPopulate; // Enemy deck reference (for population)

		[Header("Status Refs")]
		public PlayerStatusSO playerStatusRef; // Player status reference (for hpMax)
		public PlayerStatusSO enemyStatusRef; // Enemy status reference (for setting hpMax)

		[Header("Card Database")]
		[Tooltip("Shop card pool, used to build card database (automatically reads all available cards from it)")]
		public DeckSO shopPoolRef; // Shop card pool reference

		[Tooltip("Additional card prefabs (optional, for cards not in shop pool)")]
		public List<GameObject> additionalCardPrefabs; // Additional cards (optional)

		[Header("Debug Enemy Deck")]
		[Tooltip("When enabled, enemy deck always uses Debug Enemy Deck, bypassing ghost fetch and default pool")]
		public bool useDebugEnemyDeck = false;

		[Tooltip("Fixed enemy deck used when Use Debug Enemy Deck is enabled")]
		public DeckSO debugEnemyDeck;

		[Header("Default Enemy Deck Pools")]
		[Tooltip("Each entry corresponds to a session; one DeckSO is randomly selected from that session's pool")]
		public List<EnemyDeckPoolEntry> defaultEnemyDeckPool = new List<EnemyDeckPoolEntry>(); // Default enemy deck pool configuration

		[Header("Enemy Deck HP Bonus")]
		[Tooltip("Detect specific cardTypeIDs in the enemy deck and increase enemy HP/HPMax based on count")]
		public List<EnemyDeckHpBonusEntry> enemyDeckHpBonuses = new List<EnemyDeckHpBonusEntry>();

		[Header("Debug")]
		[SerializeField] private bool printOnSave = true;

		// Card type ID to prefab mapping cache
		private Dictionary<string, GameObject> _cardTypeToPrefabCache;

		private void Start()
		{
			BuildCardDatabaseCache();
		}

		/// <summary>
		/// Build card type ID to prefab mapping cache
		/// </summary>
		private void BuildCardDatabaseCache()
		{
			_cardTypeToPrefabCache = new Dictionary<string, GameObject>();

			// Read cards from shop pool
			if (shopPoolRef != null && shopPoolRef.deck != null)
			{
				foreach (var cardPrefab in shopPoolRef.deck)
				{
					AddCardToCache(cardPrefab);
				}
			}
			else
			{
				// Debug.LogWarning("[DeckSaver] ShopPoolRef is not set or empty, card database will be empty");
			}

			// Add additional cards (if any)
			if (additionalCardPrefabs != null)
			{
				foreach (var cardPrefab in additionalCardPrefabs)
				{
					AddCardToCache(cardPrefab);
				}
			}

			if (printOnSave)
			{
				// Debug.Log($"[DeckSaver] Card database built, total {_cardTypeToPrefabCache.Count} cards");
			}
		}

		/// <summary>
		/// Add card to cache dictionary
		/// </summary>
		private void AddCardToCache(GameObject cardPrefab)
		{
			if (cardPrefab == null) return;

			var cardScript = cardPrefab.GetComponent<CardScript>();
			if (cardScript == null) return;

			string typeID = GetCardTypeID(cardScript);
			if (string.IsNullOrEmpty(typeID)) return;

			if (_cardTypeToPrefabCache.ContainsKey(typeID))
			{
				// Debug.LogWarning($"[DeckSaver] Duplicate cardTypeID: {typeID}, card: {cardPrefab.name}");
				return;
			}
			_cardTypeToPrefabCache[typeID] = cardPrefab;
		}

		/// <summary>
		/// Get stable card type ID from CardScript
		/// </summary>
		private string GetCardTypeID(CardScript cardScript)
		{
			// Prefer configured cardTypeID
			if (!string.IsNullOrEmpty(cardScript.cardTypeID))
			{
				return cardScript.cardTypeID;
			}

			// If not configured, use card name and warn
			// Debug.LogWarning($"[DeckSaver] Card {cardScript.name} has no cardTypeID configured, using card name as identifier");
			return cardScript.name;
		}

		/// <summary>
		/// Find card prefab by cardTypeID
		/// </summary>
		private GameObject FindCardPrefabByTypeID(string cardTypeID)
		{
			if (_cardTypeToPrefabCache == null || _cardTypeToPrefabCache.Count == 0)
			{
				BuildCardDatabaseCache();
			}

			if (_cardTypeToPrefabCache.TryGetValue(cardTypeID, out var prefab))
			{
				return prefab;
			}

			// Debug.LogError($"[DeckSaver] Cannot find card prefab with cardTypeID {cardTypeID}");
			return null;
		}

		/// <summary>
		/// Public getter for card prefab by cardTypeID. Used by EnemyDeckRecorder.
		/// </summary>
		public GameObject GetCardPrefabByTypeID(string cardTypeID)
		{
			return FindCardPrefabByTypeID(cardTypeID);
		}

		/// <summary>
		/// Calculate total HP bonus from a list of card prefabs.
		/// </summary>
		private int CalculateHpBonus(List<GameObject> deck)
		{
			int bonus = 0;
			if (deck == null || enemyDeckHpBonuses.Count == 0) return bonus;

			foreach (var cardPrefab in deck)
			{
				if (cardPrefab == null) continue;

				var cardScript = cardPrefab.GetComponent<CardScript>();
				if (cardScript == null) continue;

				string typeID = GetCardTypeID(cardScript);
				foreach (var entry in enemyDeckHpBonuses)
				{
					if (entry.cardTypeID == typeID)
					{
						bonus += entry.hpBonusPerCard;
					}
				}
			}
			return bonus;
		}

		/// <summary>
		/// Calculate total HP bonus from a list of card type IDs.
		/// </summary>
		private int CalculateHpBonus(List<string> cardTypeIDs)
		{
			int bonus = 0;
			if (cardTypeIDs == null || enemyDeckHpBonuses.Count == 0) return bonus;

			foreach (var typeID in cardTypeIDs)
			{
				foreach (var entry in enemyDeckHpBonuses)
				{
					if (entry.cardTypeID == typeID)
					{
						bonus += entry.hpBonusPerCard;
					}
				}
			}
			return bonus;
		}

		/// <summary>
		/// Apply calculated HP bonus to enemy status.
		/// </summary>
		private void ApplyEnemyHpBonus(int bonus)
		{
			if (enemyStatusRef == null || bonus <= 0) return;

			enemyStatusRef.hpMax += bonus;
			enemyStatusRef.hp += bonus;

			// Debug.Log($"[DeckSaver] Enemy deck bonus applied: +{bonus} HP/HPMax");
		}

		#region Deck Operations

		/// <summary>
		/// Snapshot the current player deck to the async-PvP server (plan §2.5)
		/// </summary>
		public void SavePlayerDeckSnapshot()
		{
			// Tutorial combat: never persist the tutorial deck as the player deck.
			if (TutorialManager.IsTutorialActive) return;

			// Async-PvP: ghost-deck snapshot for other players (plan §2.5); outbox-backed.
			UploadDeckSnapshot(CreateDeckSaveEntry());
		}

		/// <summary>
		/// Enqueue the just-saved player deck as a ghost-deck snapshot (plan §2.5).
		/// Silently skipped when networking is off or identity is not registered yet.
		/// </summary>
		private void UploadDeckSnapshot(DeckSaveEntry deckEntry)
		{
			if (!PlayerIdentity.HasIdentity) return;

			var request = new DeckUploadRequest
			{
				playerId = PlayerIdentity.PlayerId,
				gameVersion = DeckNetworkClient.GameVersion,
				sessionNum = deckEntry.sessionNum,
				hpMax = deckEntry.hpMax,
				winAmount = deckEntry.winAmount,
				heartLeft = deckEntry.heartLeft,
				cardTypeIDs = deckEntry.cardTypeIDs
			};
			UploadOutbox.Enqueue(NetUploadKind.DeckSnapshot, request);
		}

		/// <summary>
		/// Create save entry from current player deck
		/// </summary>
		private DeckSaveEntry CreateDeckSaveEntry()
		{
			var cardTypeIDs = new List<string>();

			foreach (var cardPrefab in playerDeck.deck)
			{
				if (cardPrefab == null) continue;

				var cardScript = cardPrefab.GetComponent<CardScript>();
				if (cardScript == null) continue;

				string typeID = GetCardTypeID(cardScript);
				if (!string.IsNullOrEmpty(typeID))
				{
					cardTypeIDs.Add(typeID);
				}
			}

			return new DeckSaveEntry
			{
				cardTypeIDs = cardTypeIDs,
				winAmount = winAmount.value,
				heartLeft = heartLeft.value,
				sessionNum = sessionNumber.value,
				hpMax = playerStatusRef != null ? playerStatusRef.hpMax : 20
			};
		}

		/// <summary>
		/// Populate enemy deck by current session number.
		/// Priority: debug > server ghost > default pool (plan §2.5).
		/// </summary>
		public void PopulateEnemyDeckBySessionNumber()
		{
			// Tutorial combat: the enemy deck is provided by TutorialManager.
			if (TutorialManager.IsTutorialActive) return;

			// No ghost is fighting until the server branch actually injects one.
			OpponentDeckCache.SetCurrentOpponent(null);

			// Debug override: use fixed enemy deck for testing (not counted in source telemetry)
			if (useDebugEnemyDeck && debugEnemyDeck != null)
			{
				UtilityFuncManagerScript.CopyGameObjectList(debugEnemyDeck.deck, enemyDeckToPopulate.deck, true);
				ApplyEnemyHpBonus(CalculateHpBonus(enemyDeckToPopulate.deck));
				return;
			}

			// Server ghost decks first: validated candidates only (plan §2.4)
			if (TryLoadFromOpponentCache())
			{
				OpponentDeckCache.RecordEnemySource(OpponentDeckCache.SourceServer);
				return;
			}

			// No ghost available: select from the default pool
			PopulateFromDefaultDecks();
			OpponentDeckCache.RecordEnemySource(OpponentDeckCache.SourcePool);
		}

		/// <summary>
		/// Try to populate from a server ghost deck candidate (plan §2.4).
		/// Any unknown cardTypeID discards the whole deck and the next candidate is tried;
		/// a dry cache returns false so the local fallback chain takes over.
		/// </summary>
		private bool TryLoadFromOpponentCache()
		{
			if (!OpponentDeckCache.FetchEnabled) return false;

			while (true)
			{
				var candidate = OpponentDeckCache.TakeCandidate(sessionNumber.value);
				if (candidate == null) return false;

				var cardPrefabs = new List<GameObject>();
				bool allKnown = candidate.cardTypeIDs != null && candidate.cardTypeIDs.Count > 0;
				if (allKnown)
				{
					foreach (var typeID in candidate.cardTypeIDs)
					{
						var prefab = FindCardPrefabByTypeID(typeID);
						if (prefab == null)
						{
							allKnown = false;
							break;
						}
						cardPrefabs.Add(prefab);
					}
				}
				if (!allKnown)
				{
					// Whole-deck discard: one unknown card means an unplayable ghost (plan §2.4).
					OpponentDeckCache.DiscardCandidate(candidate.deckId);
					continue;
				}

				// Populate enemy deck
				enemyDeckToPopulate.deck.Clear();
				enemyDeckToPopulate.deck.AddRange(cardPrefabs);

				// Apply the ghost's saved hpMax (same rule as the JSON branch)
				if (enemyStatusRef != null)
				{
					enemyStatusRef.hpMax = candidate.hpMax > 0 ? candidate.hpMax : 20;
				}

				// Apply HP bonus for specific cardTypeIDs in the ghost deck
				ApplyEnemyHpBonus(CalculateHpBonus(candidate.cardTypeIDs));

				// Stash for the VS display and the match report (plan §2.5)
				OpponentDeckCache.SetCurrentOpponent(candidate);
				return true;
			}
		}

		/// <summary>
		/// Select corresponding deck pool from default enemy deck pools by current session number,
		/// then randomly pick one DeckSO from that pool to populate the enemy deck.
		/// session 0 -> pool[0], session 1 -> pool[1], and so on.
		/// If session number exceeds pool range, use last pool.
		/// </summary>
		private void PopulateFromDefaultDecks()
		{
			if (defaultEnemyDeckPool == null || defaultEnemyDeckPool.Count == 0)
			{
				// Debug.LogWarning($"[DeckSaver] Session {sessionNumber.value}: No JSON record and default deck pool is empty, cannot populate enemy deck");
				return;
			}

			// Use session number directly as pool index (session 0 -> pool[0], session 1 -> pool[1])
			int poolIndex = sessionNumber.value;
			// If out of range, use last pool
			if (poolIndex >= defaultEnemyDeckPool.Count)
			{
				poolIndex = defaultEnemyDeckPool.Count - 1;
			}
			var selectedPool = defaultEnemyDeckPool[poolIndex];

			if (selectedPool == null || selectedPool.decks == null || selectedPool.decks.Count == 0)
			{
				// Debug.LogWarning($"[DeckSaver] Session {sessionNumber.value}: Selected default deck pool is empty, cannot populate enemy deck");
				return;
			}

			// Randomly select one deck from the pool
			var selectedDeck = selectedPool.decks[UnityEngine.Random.Range(0, selectedPool.decks.Count)];

			// Use utility function to copy deck
			UtilityFuncManagerScript.CopyGameObjectList(selectedDeck.deck, enemyDeckToPopulate.deck, true);
			// Debug.Log($"[DeckSaver] Session {sessionNumber.value}: Loaded enemy deck from default pool: {selectedDeck.name}");

			// Apply HP bonus for specific cardTypeIDs in the selected deck
			ApplyEnemyHpBonus(CalculateHpBonus(enemyDeckToPopulate.deck));
		}

		#endregion

		#region Debug Hotkeys
		// Hotkey instructions (must be active in Game view):
		// Ctrl + S: Snapshot current player deck to the server
		// Ctrl + L: Load deck to enemy deck

		private void Update()
		{
			if (!Input.GetKey(KeyCode.LeftControl)) return;

			// Ctrl + S: Save
			if (Input.GetKeyDown(KeyCode.S) && !Input.GetKey(KeyCode.LeftShift))
			{
				SavePlayerDeckSnapshot();
			}

			// Ctrl + L: Load
			if (Input.GetKeyDown(KeyCode.L))
			{
				PopulateEnemyDeckBySessionNumber();
			}
		}

		#endregion
	}
}
