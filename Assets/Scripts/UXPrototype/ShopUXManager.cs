using System.Collections.Generic;
using DefaultNamespace.Managers;
using TMPro;
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
	[Tooltip("Derive the downward scroll limit from the actual deck row count instead of the fixed cameraMinY")]
	public bool useDynamicScrollBounds = true;
	[Tooltip("World-space gap kept below the bottom deck row when the dynamic bound is computed")]
	public float scrollBottomPadding = 1f;
	
	[Header("Shop Chrome (world)")]
	[Tooltip("Sliced sprite reused by the world chrome band / chips / buttons (the card face sprite keeps them consistent with price buttons)")]
	public Sprite chromeSprite;
	[Tooltip("Font asset for chrome labels (world-space TMP)")]
	public TMP_FontAsset chromeFont;

	// Store instantiated physical cards for cleanup
	private List<GameObject> _spawnedShopCards = new List<GameObject>();
	private List<GameObject> _spawnedPlayerCards = new List<GameObject>();
	// Persistent empty slots, one per deckSize grid slot, rendered behind the cards.
	// Never consumed by buy/sell; only created on shop entry and deckSize increase.
	private List<GameObject> _spawnedEmptySlots = new List<GameObject>();
	
	private Camera _mainCamera;
	private float _cameraInitialY;
	// Scroll writes go on the camera's parent rig: the camera's localPosition is owned
	// per-frame by MilkShake Shaker, which would clobber any offset written directly.
	private Transform _scrollTarget;

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
			
			Vector3 spawnPosition = GetShopItemSlotPosition(i);
			
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

		// DIAG-LOG(2026-09-06): physical shelf contents (pairs with [ShopBoard] board list)
		LogSpawnedShopCardIds();

		// Wrapped shelf (4+ cards) pushes the deck band down via CurrentDeckPos; a single-row shelf
		// restores it. Deck-only relayout keeps the shelf entry animation intact (RelayoutAll would
		// snap the shelf cards to their slots).
		RelayoutDeckBand();
	}

	/// <summary>
	/// DIAG-LOG(2026-09-06): logs the typeIDs of the cards physically on the shop shelf,
	/// so console output pairs 1:1 with the [ShopBoard] board-generation list.
	/// </summary>
	private void LogSpawnedShopCardIds()
	{
		var ids = new List<string>();
		foreach (var card in _spawnedShopCards)
		{
			var phys = card != null ? card.GetComponent<CardPhysObjScript>() : null;
			var represented = phys != null ? phys.cardImRepresenting : null;
			ids.Add(represented != null ? represented.cardTypeID : "null");
		}
		TestManager.Log("[ShopBoard] physical shelf=[" + string.Join(", ", ids) + "]");
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
		return CurrentDeckPos() + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
	}

	/// <summary>
	/// First grid index of the slot-free (utility) track. Utility passives do not consume deck
	/// slots, so their display zone always starts right after the last persistent empty slot
	/// (grid index = deckSize). Defensive fallback when deckSize is unavailable: the end of
	/// the occupying track assigned so far.
	/// </summary>
	private int GetUtilityZoneBaseSlot(int occupyingTrackEndSlot)
	{
		return ShopManager.me != null && ShopManager.me.deckSize != null
			? ShopManager.me.deckSize.value
			: occupyingTrackEndSlot;
	}

	// VISUAL-FIX(2026-09-10): The 4th shelf card (初魇 RaritySlotU first-board guarantee) fell off the
	//   right screen edge
	//   Cause:    GetShopItemSlotPosition was a single un-wrapping row (x = (index-1) * xOffset), so a
	//             board count above objPerRow ran past the visible width; the player-deck grid had
	//             row/col wrap all along and the shelf never did.
	//   Affects:  ShopUXManager shelf layout (spawn, RelayoutAll, hover restore), deck band position
	//             (CurrentDeckPos), scroll lower bound (ComputeDynamicMinY).
	//   Regress:  Own 初魇, enter Shop (first board = 3+1): the guaranteed ✦✦ card sits on shelf row 2
	//             fully on screen and the deck band slides down one pitch; reroll (3 cards) slides the
	//             band back up; buying a shelf card reflows the shelf with no holes.
	//   Related:  UTILITY_SLOT_U_1 (初魇), UTILITY_OPTION_1 (魇市新摊 extraShopOptions)
	/// <summary>
	/// Shelf position for a shop item index (single source for the shelf layout formula).
	/// Rows wrap on objPerRow with the deck grid's rhythm: indices 0..objPerRow-1 form row 0,
	/// the next objPerRow cards land on row 2 one pitch below.
	/// </summary>
	private Vector3 GetShopItemSlotPosition(int shopIndex)
	{
		int row = shopIndex / objPerRow;
		int col = shopIndex % objPerRow;
		return shopItemPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
	}

	/// <summary>
	/// Deck band origin. A single-row shelf keeps the deck at playerDeckPos (today's layout);
	/// a wrapped shelf shifts the whole band down by one pitch per extra shelf row, keeping a
	/// constant gap between the lowest shelf row and the top deck row.
	/// </summary>
	private Vector3 CurrentDeckPos()
	{
		int perRow = Mathf.Max(1, objPerRow);
		int shelfRows = Mathf.CeilToInt((float)_spawnedShopCards.Count / perRow);
		return playerDeckPos + new Vector3(0f, -Mathf.Max(0, shelfRows - 1) * yOffset, 0f);
	}

	/// <summary>
	/// Assigns player-deck grid slots in two tracks. Slot-occupying cards take the main grid
	/// (0..deckSize-1, where the persistent empty-slot frames live); slot-free cards
	/// (occupiesDeckSlot = false, utility passives) take a trailing track that always starts
	/// after the last empty slot, so they render in a fixed zone behind the empty slots and
	/// never interleave with occupying cards. With DuplicateStackingEnabled, the first card of
	/// each cardTypeID takes the next slot of its own track and further copies stack toward
	/// the upper-left of that slot.
	/// </summary>
	private class StackSlotAssigner
	{
		private class Track
		{
			public int NextSlot;
			public readonly Dictionary<string, int> SlotByType = new Dictionary<string, int>();
			public readonly Dictionary<string, int> CopyCountByType = new Dictionary<string, int>();
		}

		private readonly ShopUXManager _owner;
		private readonly Track _occupyingTrack = new Track();
		private readonly Track _utilityTrack = new Track();

		public StackSlotAssigner(ShopUXManager owner)
		{
			_owner = owner;
		}

		public Vector3 Assign(string cardTypeID, bool occupiesDeckSlot, out bool isStackedCopy)
		{
			Track track = occupiesDeckSlot ? _occupyingTrack : _utilityTrack;
			int baseSlot = occupiesDeckSlot ? 0 : _owner.GetUtilityZoneBaseSlot(_occupyingTrack.NextSlot);
			isStackedCopy = false;
			if (!_owner.DuplicateStackingEnabled || string.IsNullOrEmpty(cardTypeID))
			{
				return _owner.GetPlayerDeckSlotPosition(baseSlot + track.NextSlot++);
			}
			int slot;
			if (!track.SlotByType.TryGetValue(cardTypeID, out slot))
			{
				slot = baseSlot + track.NextSlot++;
				track.SlotByType[cardTypeID] = slot;
				track.CopyCountByType[cardTypeID] = 0;
				return _owner.GetPlayerDeckSlotPosition(slot);
			}
			int copyIndex = Mathf.Min(track.CopyCountByType[cardTypeID] + 1, _owner.duplicateStackMaxOffsetCount);
			track.CopyCountByType[cardTypeID] = copyIndex;
			isStackedCopy = true;
			return _owner.GetPlayerDeckSlotPosition(slot) + _owner.duplicateStackOffset * copyIndex;
		}
	}

	/// <summary>
	/// Recompute target positions for every player-deck card:
	/// unique cardTypeIDs take grid slots, duplicates stack upper-left.
	/// Slot-free cards (occupiesDeckSlot = false) land on the trailing utility track behind
	/// the empty slots (see StackSlotAssigner).
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
			physObj.SetTargetPosition(assigner.Assign(physObj.cardImRepresenting.cardTypeID, physObj.cardImRepresenting.occupiesDeckSlot, out bool isStackedCopy));
			SetPriceSuppressed(card, isStackedCopy);
		}
	}

	/// <summary>
	/// Live Inspector tuning: recompute every position driven by xOffset / yOffset / shopItemPos /
	/// playerDeckPos — shop shelf cards, persistent empty slots and player-deck cards.
	/// Applied immediately (tweens killed) so Inspector edits track in real time; hover-enlarged
	/// shop cards are skipped so the enlarge state is not fought (RestoreCard owns their position).
	/// </summary>
	public void RelayoutAll()
	{
		foreach (var card in _spawnedShopCards)
		{
			if (card == null) continue;
			var physObj = card.GetComponent<CardPhysObjScript>();
			if (physObj == null || physObj.shopItemIndex < 0) continue;
			var view = card.GetComponent<ShopCardView>();
			if (view != null && view.IsEnlarged) continue;
			physObj.SetPositionImmediate(GetShopItemSlotPosition(physObj.shopItemIndex));
		}

		RelayoutDeckBand();
	}

	/// <summary>
	/// Recompute the deck band: empty slots snap to their grid slots and deck cards tween to
	/// theirs. Also fires when the shelf row count crosses the objPerRow threshold — a wrapped
	/// shelf (4+ cards) pushes the band down via CurrentDeckPos, a single-row shelf restores it.
	/// </summary>
	private void RelayoutDeckBand()
	{
		// List order equals slot index (slots are appended sequentially by SpawnEmptySlots).
		for (int i = 0; i < _spawnedEmptySlots.Count; i++)
		{
			GameObject slot = _spawnedEmptySlots[i];
			if (slot == null) continue;
			Vector3 slotPosition = GetPlayerDeckSlotPosition(i) + new Vector3(0f, 0f, emptySlotZOffset);
			var physObj = slot.GetComponent<CardPhysObjScript>();
			if (physObj != null)
			{
				// VISUAL-FIX(2026-09-11): tween so the band glides when a purchase crosses the
				// objPerRow threshold; on entry/reroll slots already sit on their slot, so the
				// zero-distance tween is a no-op. Snapping put an empty-slot frame under the
				// shelf card still tweening up to row 0.
				physObj.SetTargetPosition(slotPosition);
			}
			else
			{
				slot.transform.position = slotPosition;
			}
		}

		RelayoutPlayerDeckCards();
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

	// VISUAL-FIX(2026-09-11): Buying a first-row shelf card made the deck band's first row overlap it
	//   Cause:    RemoveFromShopCards re-indexed remaining shelf cards' shopItemIndex but never
	//             retargeted their physical positions; the formerly wrapped row-2 card stayed at its
	//             old slot (y = 2.2 - 4.5 = -2.3) while RelayoutDeckBand saw a single-row shelf
	//             (count <= objPerRow) and slid the deck band back up, landing deck row 0 at -2.5.
	//   Affects:  ShopUXManager.RemoveFromShopCards (shelf reflow), RelayoutDeckBand (empty slots
	//             tween with the band instead of snapping), ShopCardView.NotifySlotMoved (hover
	//             enlarged card keeps its captured restore position on its NEW slot)
	//   Regress:  objPerRow=3 shelf with 4 cards, buy a row-0 card: the row-1 card tweens up to
	//             row 0 (no holes), the deck band glides up and never overlaps a shelf card; buy a
	//             mid-row card on a 5+ card shelf: cards shift left/up with no holes; a hover
	//             enlarged card during a purchase restores to its new slot on release.
	//   Related:  VISUAL-FIX(2026-09-10) shelf wrap + band push-down (GetShopItemSlotPosition /
	//             CurrentDeckPos) — wrapping was added but the purchase reflow was never wired.
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

		// Reflow the shelf: remaining cards tween to their re-indexed slots (also closes the hole
		// left by any mid-row purchase). Enlarged cards keep hover ownership of their target;
		// sync their captured restore position instead so RestoreCard lands on the new slot.
		foreach (var card in _spawnedShopCards)
		{
			if (card == null) continue;
			CardPhysObjScript physObj = card.GetComponent<CardPhysObjScript>();
			if (physObj == null || physObj.shopItemIndex < 0) continue;
			var view = card.GetComponent<ShopCardView>();
			Vector3 slotPosition = GetShopItemSlotPosition(physObj.shopItemIndex);
			if (view != null && view.IsEnlarged)
			{
				view.NotifySlotMoved(slotPosition);
				continue;
			}
			physObj.SetTargetPosition(slotPosition);
		}

		// A purchase that crosses the objPerRow threshold (4->3) slides the deck band back up.
		RelayoutDeckBand();
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
			
			// Do not instantiate cards that are not physical deck cards
			if (!cardScript.physicalDeckCard)
			{
				continue;
			}
			
			// Calculate position: grid slot per unique cardTypeID (slot-free cards take the
			// trailing utility track), duplicates stack upper-left when the toggle is on
			Vector3 spawnPosition = slotAssigner.Assign(cardScript.cardTypeID, cardScript.occupiesDeckSlot, out bool isStackedCopy);
			
			// Instantiate physical card
			Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : CurrentDeckPos();
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
		ShopChrome.Bootstrap(chromeSprite, chromeFont);
		_mainCamera = Camera.main;
		if (_mainCamera == null) return;

		// VISUAL-FIX(2026-09-08): Camera scroll snapped back instantly after each wheel tick
		//   Cause:    MilkShake Shaker sits on Main Camera and overwrites its localPosition every
		//             Update (Shaker.cs Update), clobbering the scroll offset written on the camera.
		//   Affects:  ShopUXManager (scroll target is now the camera's parent rig, "Camera Man")
		//   Regress:  Enter Shop, wheel-scroll down: camera holds position while scrolling;
		//             combat hit shakes still work (Shaker keeps owning camera localPosition)
		//   Related:  Assets/MilkShake/Scripts/Shaker.cs Update(), dynamic scroll bounds (ComputeDynamicMinY)
		_scrollTarget = _mainCamera.transform.parent != null ? _mainCamera.transform.parent : _mainCamera.transform;
		_cameraInitialY = _scrollTarget.position.y;
	}

	// Live Inspector tuning: OnValidate (editor-only) flags a relayout and Update applies it on
	// the next frame, so Inspector edits to xOffset / yOffset / shopItemPos / playerDeckPos take
	// effect at once instead of waiting for the next buy / sell / reroll.
	private bool _layoutDirty = false;

	private void Update()
	{
		if (_layoutDirty)
		{
			_layoutDirty = false;
			RelayoutAll();
		}
		HandleCameraScroll();
	}

	private void OnValidate()
	{
		_layoutDirty = true;
	}
	
	/// <summary>
	/// Dynamic downward scroll limit: derived from the lowest actually-laid-out content
	/// (player-deck cards + persistent empty slots, logical TargetPosition so in-flight
	/// tweens do not skew it). Scanning real positions keeps the bound correct through
	/// shelf wrapping, deck-band shifts and card counts exceeding deckSize slots.
	/// Falls back to the deckSize row formula when nothing is laid out yet, then to
	/// the fixed cameraMinY.
	/// </summary>
	private float ComputeDynamicMinY()
	{
		float lowestContentY = Mathf.Min(
			GetLaidOutLowestY(_spawnedPlayerCards),
			GetLaidOutLowestY(_spawnedEmptySlots));

		if (lowestContentY == float.MaxValue)
		{
			int slotCount = ShopManager.me != null && ShopManager.me.deckSize != null ? ShopManager.me.deckSize.value : 0;
			if (slotCount <= 0 || objPerRow <= 0)
			{
				return cameraMinY;
			}
			int rows = (slotCount + objPerRow - 1) / objPerRow;
			lowestContentY = CurrentDeckPos().y - (rows - 1) * yOffset;
		}

		if (_mainCamera == null)
		{
			return cameraMinY;
		}
		float viewHalfHeight = _mainCamera.orthographic
			? _mainCamera.orthographicSize
			: Mathf.Tan(_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(_mainCamera.transform.position.z);
		// Camera center rests ABOVE the lowest content by (viewHalfHeight - padding), so at the
		// floor the bottom row sits scrollBottomPadding above the view's bottom edge.
		float dynamicBound = (lowestContentY + viewHalfHeight - scrollBottomPadding) - _cameraInitialY;
		// A shallow board could push the bound above the upward limit and invert the clamp range.
		return Mathf.Min(dynamicBound, cameraMaxY);
	}

	/// <summary>
	/// Lowest logical target Y among spawned card objects (skips nulls and objects without
	/// CardPhysObjScript). Returns float.MaxValue when the list carries no measurable content.
	/// </summary>
	private static float GetLaidOutLowestY(List<GameObject> cards)
	{
		float lowestY = float.MaxValue;
		foreach (var card in cards)
		{
			if (card == null) continue;
			var physObj = card.GetComponent<CardPhysObjScript>();
			if (physObj == null) continue;
			lowestY = Mathf.Min(lowestY, physObj.TargetPosition.y);
		}
		return lowestY;
	}

	/// <summary>
	/// Handle mouse wheel control of camera up/down movement
	/// </summary>
	private void HandleCameraScroll()
	{
		if (!enableCameraScroll || _mainCamera == null || _scrollTarget == null)
			return;
		
		float scrollInput = Input.GetAxis("Mouse ScrollWheel");
		if (Mathf.Abs(scrollInput) < 0.001f)
			return;
		
		// Calculate new Y position
		float minYOffset = useDynamicScrollBounds ? ComputeDynamicMinY() : cameraMinY;
		Vector3 cameraPos = _scrollTarget.position;
		// Wheel up (positive input) moves the camera up toward the shop row; wheel down dives into the deck
		cameraPos.y += scrollInput * cameraScrollSpeed;
		cameraPos.y = Mathf.Clamp(cameraPos.y, _cameraInitialY + minYOffset, _cameraInitialY + cameraMaxY);
		
		_scrollTarget.position = cameraPos;
	}
	
	/// <summary>
	/// Reset camera position to initial Y
	/// </summary>
	public void ResetCameraPosition()
	{
		if (_scrollTarget == null) return;

		Vector3 cameraPos = _scrollTarget.position;
		cameraPos.y = _cameraInitialY;
		_scrollTarget.position = cameraPos;
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
		
		// 2. Check if card is a physical deck card
		if (cardScript != null && !cardScript.physicalDeckCard)
		{
			// If not physical, remove directly from _spawnedShopCards and destroy
			RemoveFromShopCards(purchasedCardIndex);
			Destroy(purchasedCard);
			// Debug.Log($"[ShopUXManager] Card purchased (non-physical), destroyed immediately");
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
		// The utility track base (= deckSize) moved with the new slots: re-slot the deck cards
		// so slot-free cards stay behind the empty slots instead of overlapping the new row.
		RelayoutPlayerDeckCards();
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
		TestManager.Log("[ShopButton] ShopUXManager.OnReroll() called. existingCards=" + _spawnedShopCards.Count + " startPos=" + (shopItemStartPos != null ? shopItemStartPos.name : "null"));
		// Roll guard (plan-world-entity-shop-chrome): gate the whole shop and deny the
		// reroll button until the new board has spawned — a queued second click can't roll twice.
		ShopInputGate.Block();
		if (ShopChrome.Instance != null) ShopChrome.Instance.SetRerollRolling(true);
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
		TestManager.Log("[ShopButton] Coroutine entered. waitTime=" + (_spawnedShopCards.Count > 0 && _spawnedShopCards[0] != null ? _spawnedShopCards[0].GetComponent<CardPhysObjScript>() != null ? _spawnedShopCards[0].GetComponent<CardPhysObjScript>().moveDuration + 0.05f : 0.35f : 0.35f) + " timeScale=" + Time.timeScale);
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
		TestManager.Log("[ShopButton] Coroutine past WaitForSeconds. elapsed=" + Time.time);
		
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
		TestManager.Log("[ShopButton] Reroll visual refresh done. newCards=" + _spawnedShopCards.Count);
		ShopInputGate.Unblock();
		if (ShopChrome.Instance != null) ShopChrome.Instance.SetRerollRolling(false);
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
			
			Vector3 spawnPosition = GetShopItemSlotPosition(i);
			
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
		
		// DIAG-LOG(2026-09-06): physical shelf contents after reroll respawn (pairs with [ShopBoard] board list)
		LogSpawnedShopCardIds();

		// Reroll can cross the objPerRow threshold (4->3): slide the deck band back to the single-row position.
		RelayoutDeckBand();
		// Layout contract (plan-world-entity-shop-chrome): the shelf must stay below the chrome band.
		ShopChrome.CheckShelfClearance(_spawnedShopCards);
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

	/// <summary>
	/// Plan step 5: emphasize pulse (combat-style 1.2x OutBack) on the player-deck physical
	/// instance of a card, fired when a utility passive's effect (re)applies via the shop
	/// recompute (buy). Payday-time application happens before deck instances exist, so there
	/// is intentionally no pulse on shop entry.
	/// </summary>
	public void PulsePlayerCard(CardScript cardScript)
	{
		if (cardScript == null) return;
		foreach (var card in _spawnedPlayerCards)
		{
			if (card == null) continue;
			var phys = card.GetComponent<CardPhysObjScript>();
			if (phys == null || phys.cardImRepresenting != cardScript) continue;

			Vector3 baseScale = phys.TargetScale;
			phys.SetTargetScale(baseScale * 1.2f, DG.Tweening.Ease.OutBack, 0.12f);
			phys.SetTargetScale(baseScale, DG.Tweening.Ease.OutQuad, 0.13f, 0.13f);
			return;
		}
	}
}
