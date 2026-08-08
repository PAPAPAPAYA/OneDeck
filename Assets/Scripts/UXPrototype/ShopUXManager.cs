using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ShopUXManager : MonoBehaviour
{
	#region Singleton
	public static ShopUXManager Instance;
	
	private void Awake()
	{
		Instance = this;
	}
	#endregion
	
	public float xOffset;
	public float yOffset;
	public int objPerRow;
	public float physCardEnlargeSize;
	
	[Header("Enlarge Settings")]
	[Tooltip("Target position after enlargement")]
	public Vector3 enlargedPosition = Vector3.zero;
	
	[Header("Spawn Settings")]
	public GameObject physicalCardPrefab;
	public GameObject emptyCardSpacePrefab;
	public Vector3 physCardSize = Vector3.one;
	public Transform spawnParent;
	
	[Header("shop item")]
	public DeckSO shopItems;
	public Vector3 shopItemPos = Vector3.zero;
	public Transform shopItemStartPos;
	
	[Header("player deck")]
	public DeckSO playerDeck;
	public Vector3 playerDeckPos;
	public Transform playerDeckStartPos;

	[Header("Duplicate Stacking")]
	[Tooltip("Per-copy offset for stacked duplicate cards (negative x = left, positive y = up)")]
	public Vector3 duplicateStackOffset = new Vector3(-0.12f, 0.12f, -0.02f);
	[Tooltip("Max copy index used for the stack offset; extra copies clamp to this offset")]
	public int duplicateStackMaxOffsetCount = 5;
	
	[Header("Empty Slot Settings")]
	[Tooltip("Z offset placing persistent empty slots behind the cards (positive z = further from camera)")]
	public float emptySlotZOffset = 0.1f;
	[Tooltip("Duration of the empty-slot spawn pop (scale 0 -> overshoot -> normal)")]
	public float emptySlotSpawnDuration = 0.35f;
	[Tooltip("Per-slot delay for the spawn pop; 0 = all slots pop simultaneously")]
	public float emptySlotSpawnStagger = 0.04f;

	[Header("Camera Scroll Settings")]
	[Tooltip("Whether to enable mouse wheel to control camera up/down movement")]
	public bool enableCameraScroll = true;
	[Tooltip("Camera scroll speed")]
	public float cameraScrollSpeed = 5f;
	[Tooltip("Camera minimum Y position (downward scroll limit)")]
	public float cameraMinY = -5f;
	[Tooltip("Camera maximum Y position (upward scroll limit)")]
	public float cameraMaxY = 5f;
	
	// Store instantiated physical cards for cleanup
	private List<GameObject> _spawnedShopCards = new List<GameObject>();
	private List<GameObject> _spawnedPlayerCards = new List<GameObject>();
	// Persistent empty slots, one per deckSize grid slot, rendered behind the cards.
	// Never consumed by buy/sell; only created on shop entry and deckSize increase.
	private List<GameObject> _spawnedEmptySlots = new List<GameObject>();
	
	private Camera _mainCamera;
	private float _cameraInitialY;

	/// <summary>
	/// Called when PhaseManager enters Shop Phase
	/// Instantiate physical card prefab based on shopItems DeckSO
	/// </summary>
	public void InstantiateShopPhysCards()
	{
		// Clean up previously instantiated shop cards
		ClearSpawnedShopCards();
		
		// Check if shopItems is empty
		if (shopItems == null || shopItems.deck == null || shopItems.deck.Count == 0)
		{
			// Debug.LogWarning("[ShopUXManager] shopItems is empty or null!");
			return;
		}
		
		// Check if physicalCardPrefab is set
		if (physicalCardPrefab == null)
		{
			// Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
			return;
		}
		
		// Iterate through shopItems.deck to instantiate physical cards
		for (int i = 0; i < shopItems.deck.Count; i++)
		{
			GameObject cardPrefab = shopItems.deck[i];
			if (cardPrefab == null)
			{
				// Debug.LogWarning($"[ShopUXManager] Shop item at index {i} is null, skipping.");
				continue;
			}
			
			// Get CardScript component
			CardScript cardScript = cardPrefab.GetComponent<CardScript>();
			if (cardScript == null)
			{
				// Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
				continue;
			}
			
			// Calculate position (start from shopItemPos - xOffset, use xOffset for horizontal arrangement)
			Vector3 spawnPosition = shopItemPos + new Vector3((i - 1) * xOffset, 0f, 0f);
			
			// Instantiate physical card (from start position, trigger DOTween entry animation)
			Vector3 initialPosition = shopItemStartPos.position;
			GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
			
			// Get CardPhysObjScript and set cardImRepresenting and target position/scale
			CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
			physicalCard.AddComponent<ShopCardView>();
			if (physObjScript != null)
			{
				physObjScript.cardImRepresenting = cardScript;
				physObjScript.shopItemIndex = i; // Set shop item index
				physObjScript.SetPositionImmediate(initialPosition);
				physObjScript.SetTargetPosition(spawnPosition);
				physObjScript.SetScaleImmediate(Vector3.zero);
				physObjScript.SetTargetScale(physCardSize);
				
				// VISUAL-FIX(2026-06-30): Shop cards show raw <dmg> placeholders instead of damage numbers
				//   Cause:    ShopUXManager initialized cardDescPrint.text with raw cardDesc,
				//             bypassing CardScript.GetCardDescForDisplay() used in combat
				//   Affects:  ShopUXManager, CardPhysObjScript, CardScript
				//   Regress:  Enter Shop phase and check that cards with <dmg> show numeric damage
				//   Related:  GOBLIN_CHARGE_TEAM, any card with <dmg> or <dmg:key>
				SetShopCardDescription(physObjScript, cardScript);
			}
			else
			{
				// Debug.LogWarning($"[ShopUXManager] Physical card prefab does not have CardPhysObjScript component!");
				physicalCard.transform.localScale = physCardSize;
			}
			
			// Record instantiated card
			_spawnedShopCards.Add(physicalCard);
		}
	}
	
	/// <summary>
	/// Clean up all instantiated shop cards
	/// </summary>
	public void ClearSpawnedShopCards()
	{
		foreach (var card in _spawnedShopCards)
		{
			if (card != null)
			{
				Destroy(card);
			}
		}
		_spawnedShopCards.Clear();
	}
	
	/// <summary>
	/// Clean up all instantiated player deck cards
	/// </summary>
	public void ClearSpawnedPlayerCards()
	{
		foreach (var card in _spawnedPlayerCards)
		{
			if (card != null)
			{
				Destroy(card);
			}
		}
		_spawnedPlayerCards.Clear();
		foreach (var slot in _spawnedEmptySlots)
		{
			if (slot != null)
			{
				Destroy(slot);
			}
		}
		_spawnedEmptySlots.Clear();
	}
	
	/// <summary>
	/// Clean up all instantiated physical cards (shop + player deck)
	/// </summary>
	public void ClearSpawnedCards()
	{
		ClearSpawnedShopCards();
		ClearSpawnedPlayerCards();
	}

	/// <summary>
	/// True when the duplicate-copies-share-slot rule is enabled (null-safe).
	/// </summary>
	private bool DuplicateStackingEnabled
	{
		get { return ShopManager.me != null && ShopManager.me.DuplicateCopiesShareSlot; }
	}

	/// <summary>
	/// Grid position for a player-deck slot index (shared row/column math for cards and empty slots).
	/// </summary>
	private Vector3 GetPlayerDeckSlotPosition(int slotIndex)
	{
		int row = slotIndex / objPerRow;
		int col = slotIndex % objPerRow;
		return playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
	}

	/// <summary>
	/// Assigns player-deck grid slots. With DuplicateStackingEnabled, the first card of each
	/// cardTypeID takes the next slot and further copies stack toward the upper-left of that slot.
	/// </summary>
	private class StackSlotAssigner
	{
		private readonly ShopUXManager _owner;
		private readonly Dictionary<string, int> _slotByType = new Dictionary<string, int>();
		private readonly Dictionary<string, int> _copyCountByType = new Dictionary<string, int>();

		public int NextSlot { get; private set; }

		public StackSlotAssigner(ShopUXManager owner)
		{
			_owner = owner;
		}

		public Vector3 Assign(string cardTypeID, out bool isStackedCopy)
		{
			isStackedCopy = false;
			if (!_owner.DuplicateStackingEnabled || string.IsNullOrEmpty(cardTypeID))
			{
				return _owner.GetPlayerDeckSlotPosition(NextSlot++);
			}
			int slot;
			if (!_slotByType.TryGetValue(cardTypeID, out slot))
			{
				_slotByType[cardTypeID] = NextSlot;
				_copyCountByType[cardTypeID] = 0;
				return _owner.GetPlayerDeckSlotPosition(NextSlot++);
			}
			int copyIndex = Mathf.Min(_copyCountByType[cardTypeID] + 1, _owner.duplicateStackMaxOffsetCount);
			_copyCountByType[cardTypeID] = copyIndex;
			isStackedCopy = true;
			return _owner.GetPlayerDeckSlotPosition(slot) + _owner.duplicateStackOffset * copyIndex;
		}
	}

	/// <summary>
	/// Recompute target positions for every player-deck card:
	/// unique cardTypeIDs take grid slots, duplicates stack upper-left.
	/// Empty slots are persistent background objects and never move.
	/// </summary>
	private void RelayoutPlayerDeckCards()
	{
		var assigner = new StackSlotAssigner(this);
		foreach (var card in _spawnedPlayerCards)
		{
			if (card == null) continue;
			var physObj = card.GetComponent<CardPhysObjScript>();
			if (physObj == null || physObj.cardImRepresenting == null) continue;
			physObj.SetTargetPosition(assigner.Assign(physObj.cardImRepresenting.cardTypeID, out bool isStackedCopy));
			SetPriceSuppressed(card, isStackedCopy);
		}
	}

	/// <summary>
	/// Show the price print only on the base card of a duplicate stack; stacked copies hide it.
	/// </summary>
	private static void SetPriceSuppressed(GameObject card, bool suppressed)
	{
		var view = card.GetComponent<ShopCardView>();
		if (view != null)
		{
			view.suppressPriceDisplay = suppressed;
		}
	}

	/// <summary>
	/// Find the last player-deck card whose CardScript has the given cardTypeID, or -1.
	/// </summary>
	private int FindLastPlayerCardIndexOfType(string cardTypeID)
	{
		int index = -1;
		for (int i = 0; i < _spawnedPlayerCards.Count; i++)
		{
			var physObj = _spawnedPlayerCards[i].GetComponent<CardPhysObjScript>();
			if (physObj != null && physObj.cardImRepresenting != null
				&& physObj.cardImRepresenting.cardTypeID == cardTypeID)
			{
				index = i;
			}
		}
		return index;
	}

	/// <summary>
	/// Remove a purchased card from the shop list and re-index the remaining shop cards.
	/// </summary>
	private void RemoveFromShopCards(int purchasedCardIndex)
	{
		_spawnedShopCards.RemoveAt(purchasedCardIndex);
		for (int i = 0; i < _spawnedShopCards.Count; i++)
		{
			CardPhysObjScript physObj = _spawnedShopCards[i].GetComponent<CardPhysObjScript>();
			if (physObj != null)
			{
				physObj.shopItemIndex = i;
			}
		}
	}

	/// <summary>
	/// Instantiate physical cards in player deck
	/// Auto-wrap based on objPerRow, use yOffset for vertical offset per row
	/// </summary>
	public void InstantiatePlayerDeckPhysCards()
	{
		// Cleanup previously instantiated player deck cards
		ClearSpawnedPlayerCards();
		
		// Check if playerDeck is null
		if (playerDeck == null || playerDeck.deck == null)
		{
			// Debug.LogWarning("[ShopUXManager] playerDeck is null!");
			return;
		}
		
		// Check if physicalCardPrefab is set
		if (physicalCardPrefab == null)
		{
			// Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
			return;
		}
		
		// Iterate through playerDeck.deck to instantiate physical cards
		var slotAssigner = new StackSlotAssigner(this);
		for (int i = 0; i < playerDeck.deck.Count; i++)
		{
			GameObject cardPrefab = playerDeck.deck[i];
			if (cardPrefab == null)
			{
				// Debug.LogWarning($"[ShopUXManager] Player deck card at index {i} is null, skipping.");
				continue;
			}
			
			// Get CardScript component
			CardScript cardScript = cardPrefab.GetComponent<CardScript>();
			if (cardScript == null)
			{
				// Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
				continue;
			}
			
			// Do not instantiate cards that do not take up deck space
			if (!cardScript.takeUpSpace)
			{
				continue;
			}
			
			// Calculate position: grid slot per unique cardTypeID, duplicates stack upper-left when the toggle is on
			Vector3 spawnPosition = slotAssigner.Assign(cardScript.cardTypeID, out bool isStackedCopy);
			
			// Instantiate physical card
			Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
			GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
			
			// Get CardPhysObjScript and setup
			CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
			physicalCard.AddComponent<ShopCardView>();
			// Stacked duplicate copies hide their price; only the base card of a stack shows it
			SetPriceSuppressed(physicalCard, isStackedCopy);
			if (physObjScript != null)
			{
				physObjScript.cardImRepresenting = cardScript;
				physObjScript.SetPositionImmediate(initialPosition);
				physObjScript.SetTargetPosition(spawnPosition);
				physObjScript.SetScaleImmediate(Vector3.zero);
				physObjScript.SetTargetScale(physCardSize);
				
				// VISUAL-FIX(2026-06-30): Shop cards show raw <dmg> placeholders instead of damage numbers
				//   Cause:    ShopUXManager initialized cardDescPrint.text with raw cardDesc,
				//             bypassing CardScript.GetCardDescForDisplay() used in combat
				//   Affects:  ShopUXManager, CardPhysObjScript, CardScript
				//   Regress:  Enter Shop phase and check that cards with <dmg> show numeric damage
				//   Related:  GOBLIN_CHARGE_TEAM, any card with <dmg> or <dmg:key>
				SetShopCardDescription(physObjScript, cardScript);
			}
			else
			{
				// Debug.LogWarning($"[ShopUXManager] Physical card prefab does not have CardPhysObjScript component!");
				physicalCard.transform.localScale = physCardSize;
			}
			
			// Record instantiated card
			_spawnedPlayerCards.Add(physicalCard);
		}
		
		// Spawn persistent empty slots behind every grid slot (cards sit on top of them)
		if (ShopManager.me != null && ShopManager.me.deckSize != null && emptyCardSpacePrefab != null)
		{
			SpawnEmptySlots(_spawnedEmptySlots.Count, ShopManager.me.deckSize.value - _spawnedEmptySlots.Count, true);
		}
	}
	
	private void Start()
	{
		_mainCamera = Camera.main;
		if (_mainCamera != null)
		{
			_cameraInitialY = _mainCamera.transform.position.y;
		}
	}
	
	private void Update()
	{
		HandleCameraScroll();
	}
	
	/// <summary>
	/// Handle mouse wheel control of camera up/down movement
	/// </summary>
	private void HandleCameraScroll()
	{
		if (!enableCameraScroll || _mainCamera == null)
			return;
		
		float scrollInput = Input.GetAxis("Mouse ScrollWheel");
		if (Mathf.Abs(scrollInput) < 0.001f)
			return;
		
		// Calculate new Y position
		Vector3 cameraPos = _mainCamera.transform.position;
		cameraPos.y -= scrollInput * cameraScrollSpeed;
		cameraPos.y = Mathf.Clamp(cameraPos.y, _cameraInitialY + cameraMinY, _cameraInitialY + cameraMaxY);
		
		_mainCamera.transform.position = cameraPos;
	}
	
	/// <summary>
	/// Reset camera position to initial Y
	/// </summary>
	public void ResetCameraPosition()
	{
		if (_mainCamera == null) return;
		
		Vector3 cameraPos = _mainCamera.transform.position;
		cameraPos.y = _cameraInitialY;
		_mainCamera.transform.position = cameraPos;
	}
	
	/// <summary>
	/// Call this method after player purchases a card
	/// 1. Move the purchased card to the next free player-deck grid slot
	/// 2. Update _spawnedShopCards and _spawnedPlayerCards
	/// (empty slots are persistent background objects and are not consumed)
	/// </summary>
	/// <param name="purchasedCardIndex">Index of purchased shop card in _spawnedShopCards</param>
	public void OnCardPurchased(int purchasedCardIndex)
	{
		// 1. Get purchased card
		if (purchasedCardIndex < 0 || purchasedCardIndex >= _spawnedShopCards.Count)
		{
			// Debug.LogWarning($"[ShopUXManager] Invalid purchased card index: {purchasedCardIndex}");
			return;
		}
		
		GameObject purchasedCard = _spawnedShopCards[purchasedCardIndex];
		CardPhysObjScript purchasedCardPhys = purchasedCard.GetComponent<CardPhysObjScript>();
		CardScript cardScript = purchasedCardPhys != null ? purchasedCardPhys.cardImRepresenting : null;
		
		// 2. Check if card occupies deck space
		if (cardScript != null && !cardScript.takeUpSpace)
		{
			// If doesn't occupy space, remove directly from _spawnedShopCards and destroy
			RemoveFromShopCards(purchasedCardIndex);
			Destroy(purchasedCard);
			// Debug.Log($"[ShopUXManager] Card purchased (no space), destroyed immediately");
			return;
		}
		
		// 3. Duplicate-slot rule: copies of an already-owned cardTypeID stack onto it.
		// Grid slots no longer match list indices, so positions come from RelayoutPlayerDeckCards.
		if (DuplicateStackingEnabled && cardScript != null && !string.IsNullOrEmpty(cardScript.cardTypeID))
		{
			RemoveFromShopCards(purchasedCardIndex);
			if (purchasedCardPhys != null)
			{
				// Clear shopItemIndex, mark as no longer a shop item
				purchasedCardPhys.shopItemIndex = -1;
			}
			
			int lastCopyIndex = FindLastPlayerCardIndexOfType(cardScript.cardTypeID);
			if (lastCopyIndex >= 0)
			{
				// Stack onto the existing copies
				_spawnedPlayerCards.Insert(lastCopyIndex + 1, purchasedCard);
			}
			else
			{
				// First copy of its type: takes the next free grid slot via RelayoutPlayerDeckCards
				_spawnedPlayerCards.Add(purchasedCard);
			}
			RelayoutPlayerDeckCards();
			return;
		}
		
		// 4. Remove from _spawnedShopCards
		RemoveFromShopCards(purchasedCardIndex);

		// 5. Add to player deck; the next free grid slot comes from RelayoutPlayerDeckCards
		// (empty slots are persistent background objects, nothing to consume)
		_spawnedPlayerCards.Add(purchasedCard);

		// Clear shopItemIndex, mark as no longer a shop item
		if (purchasedCardPhys != null)
		{
			purchasedCardPhys.shopItemIndex = -1;
		}

		RelayoutPlayerDeckCards();
	}
	
	/// <summary>
	/// Call this method after player sells a card
	/// 1. Move sold card to shop start position
	/// 2. Destroy after card reaches target position
	/// 3. Compact remaining cards toward the first grid slots
	/// (empty slots are persistent background objects, nothing to respawn)
	/// </summary>
	/// <param name="soldCardInstance">Sold physical card instance</param>
	/// <param name="cardIndex">Original index of sold card in player deck</param>
	public void OnCardSold(GameObject soldCardInstance, int cardIndex)
	{
		if (soldCardInstance == null) return;
		
		// 1. Find index of sold card in _spawnedPlayerCards
		int spawnedIndex = _spawnedPlayerCards.IndexOf(soldCardInstance);
		if (spawnedIndex < 0)
		{
			// Debug.LogWarning($"[ShopUXManager] Sold card not found in _spawnedPlayerCards");
			// Destroy directly
			Destroy(soldCardInstance);
			return;
		}
		
		// 2. Duplicate-slot rule: empty slots are persistent, so nothing is respawned
		CardPhysObjScript soldCardPhys = soldCardInstance.GetComponent<CardPhysObjScript>();
		if (DuplicateStackingEnabled)
		{
			_spawnedPlayerCards.RemoveAt(spawnedIndex);

			if (soldCardPhys != null)
			{
				// Set target position to shop start position (play sell animation)
				Vector3 shopStartPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
				soldCardPhys.SetTargetPosition(shopStartPosition);
				soldCardPhys.SetTargetScale(Vector3.zero); // Scale down simultaneously

				// Start coroutine to destroy card after animation
				StartCoroutine(DestroySoldCardAfterAnimation(soldCardInstance));
			}
			else
			{
				// If no CardPhysObjScript, destroy directly
				Destroy(soldCardInstance);
			}
			RelayoutPlayerDeckCards();
			return;
		}

		// 3. Remove from _spawnedPlayerCards
		_spawnedPlayerCards.RemoveAt(spawnedIndex);

		// 4. Set sold card's target position to shop start position (play sell animation)
		if (soldCardPhys != null)
		{
			// Set target position to shop start position
			Vector3 shopStartPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
			soldCardPhys.SetTargetPosition(shopStartPosition);
			soldCardPhys.SetTargetScale(Vector3.zero); // Scale down simultaneously

			// Start coroutine to destroy card after animation
			StartCoroutine(DestroySoldCardAfterAnimation(soldCardInstance));
		}
		else
		{
			// If no CardPhysObjScript, destroy directly
			Destroy(soldCardInstance);
		}

		// 5. Compact remaining cards toward the first grid slots
		RelayoutPlayerDeckCards();
	}
	
	/// <summary>
	/// Coroutine: Wait for sell animation to complete, then destroy the card.
	/// Empty slots are persistent background objects, so nothing is respawned.
	/// </summary>
	private System.Collections.IEnumerator DestroySoldCardAfterAnimation(GameObject soldCard)
	{
		// Wait for animation to complete (using CardPhysObjScript's moveDuration, default 0.3s, add a buffer)
		float waitTime = 0.35f;
		if (soldCard != null)
		{
			var physObj = soldCard.GetComponent<CardPhysObjScript>();
			if (physObj != null)
			{
				waitTime = physObj.moveDuration + 0.05f;
			}
		}
		yield return new WaitForSeconds(waitTime);

		// Destroy sold card
		if (soldCard != null)
		{
			Destroy(soldCard);
		}
	}

	/// <summary>
	/// Called when deckSize increases: spawn persistent empty slots for the new grid slots.
	/// </summary>
	public void SpawnAdditionalEmptySpaces()
	{
		if (emptyCardSpacePrefab == null || ShopManager.me == null || ShopManager.me.deckSize == null)
			return;

		SpawnEmptySlots(_spawnedEmptySlots.Count, ShopManager.me.deckSize.value - _spawnedEmptySlots.Count, true);
	}

	/// <summary>
	/// Spawn persistent empty slots at grid slots [fromSlot, fromSlot + count), placed behind
	/// the cards (emptySlotZOffset). With animate = true, each slot pops in place:
	/// scale 0 -> overshoot -> normal size (Ease.OutBack), staggered per slot.
	/// Slots never move and are never consumed by buy/sell.
	/// </summary>
	private void SpawnEmptySlots(int fromSlot, int count, bool animate)
	{
		if (emptyCardSpacePrefab == null || count <= 0) return;

		for (int i = 0; i < count; i++)
		{
			Vector3 slotPosition = GetPlayerDeckSlotPosition(fromSlot + i) + new Vector3(0f, 0f, emptySlotZOffset);

			GameObject emptySpace = Instantiate(emptyCardSpacePrefab, slotPosition, Quaternion.identity, spawnParent);

			CardPhysObjScript physObjScript = emptySpace.GetComponent<CardPhysObjScript>();
			if (physObjScript != null)
			{
				physObjScript.SetPositionImmediate(slotPosition);
				if (animate)
				{
					physObjScript.SetScaleImmediate(Vector3.zero);
					physObjScript.SetTargetScale(physCardSize, Ease.OutBack, emptySlotSpawnDuration, i * emptySlotSpawnStagger);
				}
				else
				{
					physObjScript.SetScaleImmediate(physCardSize);
				}
			}
			else
			{
				emptySpace.transform.localScale = physCardSize;
			}

			_spawnedEmptySlots.Add(emptySpace);
		}
	}

	/// <summary>
	/// Call this method when shop rerolls
	/// 1. Existing shop cards fly to shop start position and shrink to destroy
	/// 2. Generate new physical cards after animation completes
	/// </summary>
	public void OnReroll()
	{
		// DIAG-LOG(2026-08-08): tracing why the shop Reroll button may appear dead
		Debug.Log("[ShopButton] ShopUXManager.OnReroll() called. existingCards=" + _spawnedShopCards.Count + " startPos=" + (shopItemStartPos != null ? shopItemStartPos.name : "null"));
		// 1. Make existing shop cards fly to shop start position and shrink
		AnimateShopCardsExit();
		
		// 2. Start coroutine, wait for animation complete then spawn new cards
		StartCoroutine(SpawnNewShopCardsAfterDelay());
	}
	
	/// <summary>
	/// Make existing shop cards fly to shop start position and shrink
	/// </summary>
	private void AnimateShopCardsExit()
	{
		Vector3 exitPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
		
		foreach (var card in _spawnedShopCards)
		{
			if (card != null)
			{
				CardPhysObjScript physObj = card.GetComponent<CardPhysObjScript>();
				if (physObj != null)
				{
					// Set target position to shop start position and shrink
					physObj.SetTargetPosition(exitPosition);
					physObj.SetTargetScale(Vector3.zero);
				}
			}
		}
	}
	
	/// <summary>
	/// Coroutine: Wait for exit animation to complete, destroy old cards and generate new ones
	/// </summary>
	private System.Collections.IEnumerator SpawnNewShopCardsAfterDelay()
	{
		// DIAG-LOG(2026-08-08): probe 1 - coroutine entered
		Debug.Log("[ShopButton] Coroutine entered. waitTime=" + (_spawnedShopCards.Count > 0 && _spawnedShopCards[0] != null ? _spawnedShopCards[0].GetComponent<CardPhysObjScript>() != null ? _spawnedShopCards[0].GetComponent<CardPhysObjScript>().moveDuration + 0.05f : 0.35f : 0.35f) + " timeScale=" + Time.timeScale);
		// Wait for animation to complete (using CardPhysObjScript's moveDuration, default 0.3s, add a buffer)
		float waitTime = 0.35f;
		if (_spawnedShopCards.Count > 0 && _spawnedShopCards[0] != null)
		{
			var physObj = _spawnedShopCards[0].GetComponent<CardPhysObjScript>();
			if (physObj != null)
			{
				waitTime = physObj.moveDuration + 0.05f;
			}
		}
		yield return new WaitForSeconds(waitTime);
		// DIAG-LOG(2026-08-08): probe 2 - past the wait
		Debug.Log("[ShopButton] Coroutine past WaitForSeconds. elapsed=" + Time.time);
		
		// Destroy old shop cards
		foreach (var card in _spawnedShopCards)
		{
			if (card != null)
			{
				Destroy(card);
			}
		}
		_spawnedShopCards.Clear();
		
		// Generate new shop physical cards
		SpawnShopCardsInternal();
		// DIAG-LOG(2026-08-08): tracing whether the reroll visual refresh completed
		Debug.Log("[ShopButton] Reroll visual refresh done. newCards=" + _spawnedShopCards.Count);
	}
	
	/// <summary>
	/// Internal method: Generate shop physical cards based on current shopItems
	/// (Don't clean list because it was cleaned before calling)
	/// </summary>
	private void SpawnShopCardsInternal()
	{
		// Check if shopItems is empty
		if (shopItems == null || shopItems.deck == null || shopItems.deck.Count == 0)
		{
			// Debug.LogWarning("[ShopUXManager] shopItems is empty or null, cannot spawn new cards!");
			return;
		}
		
		// Check if physicalCardPrefab is set
		if (physicalCardPrefab == null)
		{
			// Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
			return;
		}
		
		// Iterate through shopItems.deck to instantiate physical cards
		for (int i = 0; i < shopItems.deck.Count; i++)
		{
			GameObject cardPrefab = shopItems.deck[i];
			if (cardPrefab == null)
			{
				// Debug.LogWarning($"[ShopUXManager] Shop item at index {i} is null, skipping.");
				continue;
			}
			
			// Get CardScript component
			CardScript cardScript = cardPrefab.GetComponent<CardScript>();
			if (cardScript == null)
			{
				// Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
				continue;
			}
			
			// Calculate position
			Vector3 spawnPosition = shopItemPos + new Vector3((i - 1) * xOffset, 0f, 0f);
			
			// Instantiate physical card (from shop start position, trigger DOTween entry animation)
			Vector3 initialPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
			GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
			
			// Get CardPhysObjScript and setup
			CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
			physicalCard.AddComponent<ShopCardView>();
			if (physObjScript != null)
			{
				physObjScript.cardImRepresenting = cardScript;
				physObjScript.shopItemIndex = i;
				physObjScript.SetPositionImmediate(initialPosition);
				physObjScript.SetTargetPosition(spawnPosition);
				physObjScript.SetScaleImmediate(Vector3.zero);
				physObjScript.SetTargetScale(physCardSize);
				
				// VISUAL-FIX(2026-06-30): Shop cards show raw <dmg> placeholders instead of damage numbers
				//   Cause:    ShopUXManager initialized cardDescPrint.text with raw cardDesc,
				//             bypassing CardScript.GetCardDescForDisplay() used in combat
				//   Affects:  ShopUXManager, CardPhysObjScript, CardScript
				//   Regress:  Enter Shop phase and check that cards with <dmg> show numeric damage
				//   Related:  GOBLIN_CHARGE_TEAM, any card with <dmg> or <dmg:key>
				SetShopCardDescription(physObjScript, cardScript);
			}
			else
			{
				// Debug.LogWarning($"[ShopUXManager] Physical card prefab does not have CardPhysObjScript component!");
				physicalCard.transform.localScale = physCardSize;
			}
			
			// Record instantiated card
			_spawnedShopCards.Add(physicalCard);
		}
		
		// Debug.Log($"[ShopUXManager] Reroll complete, spawned {_spawnedShopCards.Count} new shop cards.");
	}

	/// <summary>
	/// Sets the card description on a shop physical card, resolving dynamic &lt;dmg&gt; placeholders.
	/// Centralizes the description initialization for all shop spawn paths.
	/// </summary>
	private void SetShopCardDescription(CardPhysObjScript physObjScript, CardScript cardScript)
	{
		if (physObjScript == null || physObjScript.cardDescPrint == null || cardScript == null) return;
		physObjScript.cardDescPrint.text = cardScript.GetCardDescForDisplay();
	}
}
