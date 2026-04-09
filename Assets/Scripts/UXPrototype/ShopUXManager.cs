using System.Collections.Generic;
using UnityEngine;

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
            Debug.LogWarning("[ShopUXManager] shopItems is empty or null!");
            return;
        }
        
        // Check if physicalCardPrefab is set
        if (physicalCardPrefab == null)
        {
            Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
            return;
        }
        
        // Iterate through shopItems.deck to instantiate physical cards
        for (int i = 0; i < shopItems.deck.Count; i++)
        {
            GameObject cardPrefab = shopItems.deck[i];
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[ShopUXManager] Shop item at index {i} is null, skipping.");
                continue;
            }
            
            // Get CardScript component
            CardScript cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null)
            {
                Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
                continue;
            }
            
            // Calculate position (start from shopItemPos - xOffset, use xOffset for horizontal arrangement)
            Vector3 spawnPosition = shopItemPos + new Vector3((i - 1) * xOffset, 0f, 0f);
            
            // Instantiate physical card (from start position, trigger DOTween entry animation)
            Vector3 initialPosition = shopItemStartPos.position;
            GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
            
            // Get CardPhysObjScript and set cardImRepresenting and target position/scale
            CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
            if (physObjScript != null)
            {
                physObjScript.cardImRepresenting = cardScript;
                physObjScript.shopItemIndex = i; // Set shop item index
                physObjScript.SetPositionImmediate(initialPosition);
                physObjScript.SetTargetPosition(spawnPosition);
                physObjScript.SetScaleImmediate(Vector3.zero);
                physObjScript.SetTargetScale(physCardSize);
                
                // Set card description
                if (physObjScript.cardDescPrint != null)
                {
                    physObjScript.cardDescPrint.text = cardScript.cardDesc;
                }
            }
            else
            {
                Debug.LogWarning($"[ShopUXManager] Physical card prefab does not have CardPhysObjScript component!");
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
    /// Instantiate physical cards in player deck
    /// Auto-wrap based on objPerRow, use yOffset for vertical offset per row
    /// </summary>
    public void InstantiatePlayerDeckPhysCards()
    {
        // Cleanup之前实例化的玩家牌组卡牌
        ClearSpawnedPlayerCards();
        
        // Check if playerDeck is empty
        if (playerDeck == null || playerDeck.deck == null || playerDeck.deck.Count == 0)
        {
            Debug.LogWarning("[ShopUXManager] playerDeck is empty or null!");
            return;
        }
        
        // Check if physicalCardPrefab is set
        if (physicalCardPrefab == null)
        {
            Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
            return;
        }
        
        // 遍历 playerDeck.deck Instantiate physical card
        int cardCount = 0;
        for (int i = 0; i < playerDeck.deck.Count; i++)
        {
            GameObject cardPrefab = playerDeck.deck[i];
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[ShopUXManager] Player deck card at index {i} is null, skipping.");
                continue;
            }
            
            // Get CardScript component
            CardScript cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null)
            {
                Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
                continue;
            }
            
            // Calculate row/column position
            int row = cardCount / objPerRow;      // Current row
            int col = cardCount % objPerRow;      // Current column
            
            // Calculate final position: start from playerDeckPos - xOffset (consistent with shop cards), xOffset horizontal, yOffset vertical wrap
            Vector3 spawnPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
            
            // Instantiate physical card
            Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
            GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
            
            // 获取 CardPhysObjScript 并设置
            CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
            if (physObjScript != null)
            {
                physObjScript.cardImRepresenting = cardScript;
                physObjScript.SetPositionImmediate(initialPosition);
                physObjScript.SetTargetPosition(spawnPosition);
                physObjScript.SetScaleImmediate(Vector3.zero);
                physObjScript.SetTargetScale(physCardSize);
                
                // Set card description
                if (physObjScript.cardDescPrint != null)
                {
                    physObjScript.cardDescPrint.text = cardScript.cardDesc;
                }
            }
            else
            {
                Debug.LogWarning($"[ShopUXManager] Physical card prefab does not have CardPhysObjScript component!");
                physicalCard.transform.localScale = physCardSize;
            }
            
            // Record instantiated card
            _spawnedPlayerCards.Add(physicalCard);
            cardCount++;
        }
        
        // Instantiate empty slot placeholders based on deckSize and current deck count
        if (ShopManager.me != null && ShopManager.me.deckSize != null && emptyCardSpacePrefab != null)
        {
            int emptySlots = ShopManager.me.deckSize.value - cardCount;
            for (int i = 0; i < emptySlots; i++)
            {
                // Calculate row/column position（接续在卡牌后面）
                int row = cardCount / objPerRow;
                int col = cardCount % objPerRow;
                
                Vector3 spawnPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
                
                // Instantiate empty slot占位符
                Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
                GameObject emptySpace = Instantiate(emptyCardSpacePrefab, initialPosition, Quaternion.identity, spawnParent);
                
                // Set position and scale（与卡牌一致）
                CardPhysObjScript physObjScript = emptySpace.GetComponent<CardPhysObjScript>();
                if (physObjScript != null)
                {
                    physObjScript.SetPositionImmediate(initialPosition);
                    physObjScript.SetTargetPosition(spawnPosition);
                    physObjScript.SetScaleImmediate(Vector3.zero);
                    physObjScript.SetTargetScale(physCardSize);
                }
                else
                {
                    emptySpace.transform.localScale = physCardSize;
                }
                
                // 记录已实例化的空位
                _spawnedPlayerCards.Add(emptySpace);
                cardCount++;
            }
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
    /// 1. Remove an emptyCardSpace placeholder
    /// 2. Set purchased card's target position to corresponding player deck position
    /// 3. Update _spawnedShopCards and _spawnedPlayerCards
    /// </summary>
    /// <param name="purchasedCardIndex">Index of purchased shop card in _spawnedShopCards</param>
    public void OnCardPurchased(int purchasedCardIndex)
    {
        // 1. Get purchased card
        if (purchasedCardIndex < 0 || purchasedCardIndex >= _spawnedShopCards.Count)
        {
            Debug.LogWarning($"[ShopUXManager] Invalid purchased card index: {purchasedCardIndex}");
            return;
        }
        
        GameObject purchasedCard = _spawnedShopCards[purchasedCardIndex];
        CardPhysObjScript purchasedCardPhys = purchasedCard.GetComponent<CardPhysObjScript>();
        CardScript cardScript = purchasedCardPhys != null ? purchasedCardPhys.cardImRepresenting : null;
        
        // 2. Check if card occupies deck space
        if (cardScript != null && !cardScript.takeUpSpace)
        {
            // If doesn't occupy space, remove directly from _spawnedShopCards and destroy
            _spawnedShopCards.RemoveAt(purchasedCardIndex);
            
            // Update shopItemIndex for remaining shop cards
            for (int i = 0; i < _spawnedShopCards.Count; i++)
            {
                CardPhysObjScript physObj = _spawnedShopCards[i].GetComponent<CardPhysObjScript>();
                if (physObj != null)
                {
                    physObj.shopItemIndex = i;
                }
            }
            
            Destroy(purchasedCard);
            Debug.Log($"[ShopUXManager] Card purchased (no space), destroyed immediately");
            return;
        }
        
        // 3. Cards occupying space: find and remove an emptyCardSpace
        GameObject emptySpaceToRemove = null;
        int emptySpaceIndex = -1;
        for (int i = 0; i < _spawnedPlayerCards.Count; i++)
        {
            // Determine emptyCardSpace by checking if has CardPhysObjScript and cardImRepresenting is null
            var physObj = _spawnedPlayerCards[i].GetComponent<CardPhysObjScript>();
            if (physObj != null && physObj.cardImRepresenting == null)
            {
                emptySpaceToRemove = _spawnedPlayerCards[i];
                emptySpaceIndex = i;
                break;
            }
        }
        
        // Remove found emptyCardSpace
        if (emptySpaceToRemove != null)
        {
            _spawnedPlayerCards.RemoveAt(emptySpaceIndex);
            Destroy(emptySpaceToRemove);
        }
        
        // 4. Remove from _spawnedShopCards
        _spawnedShopCards.RemoveAt(purchasedCardIndex);
        
        // Update shopItemIndex for remaining shop cards
        for (int i = 0; i < _spawnedShopCards.Count; i++)
        {
            CardPhysObjScript physObj = _spawnedShopCards[i].GetComponent<CardPhysObjScript>();
            if (physObj != null)
            {
                physObj.shopItemIndex = i;
            }
        }
        
        // 5. Insert at removed emptyCardSpace position (if found)
        if (emptySpaceIndex >= 0)
        {
            _spawnedPlayerCards.Insert(emptySpaceIndex, purchasedCard);
        }
        else
        {
            // If emptyCardSpace not found (deck full), add to end
            _spawnedPlayerCards.Add(purchasedCard);
            emptySpaceIndex = _spawnedPlayerCards.Count - 1;
        }
        
        // 6. Calculate new position in player deck (fill empty slot position)
        int row = emptySpaceIndex / objPerRow;
        int col = emptySpaceIndex % objPerRow;
        
        Vector3 targetPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
        
        // Update purchased card's target position
        if (purchasedCardPhys != null)
        {
            purchasedCardPhys.SetTargetPosition(targetPosition);
            // Clear shopItemIndex, mark as no longer a shop item
            purchasedCardPhys.shopItemIndex = -1;
        }
        
        Debug.Log($"[ShopUXManager] Card purchased, moved to player deck position ({row}, {col})");
    }
    
    /// <summary>
    /// Call this method after player sells a card
    /// 1. Move sold card to shop start position
    /// 2. Destroy after card reaches target position
    /// 3. Insert emptyCardSpace at sold card's position
    /// 4. 更新 _spawnedPlayerCards
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
            Debug.LogWarning($"[ShopUXManager] Sold card not found in _spawnedPlayerCards");
            // 直接销毁
            Destroy(soldCardInstance);
            return;
        }
        
        // 2. Remove from _spawnedPlayerCards
        _spawnedPlayerCards.RemoveAt(spawnedIndex);
        
        // 3. Calculate empty slot position
        int row = spawnedIndex / objPerRow;
        int col = spawnedIndex % objPerRow;
        Vector3 emptySpacePosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
        
        // 4. Set sold card's target position to shop start position (play sell animation)
        CardPhysObjScript soldCardPhys = soldCardInstance.GetComponent<CardPhysObjScript>();
        if (soldCardPhys != null)
        {
            // Set target position为商店起始位置
            Vector3 shopStartPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
            soldCardPhys.SetTargetPosition(shopStartPosition);
            soldCardPhys.SetTargetScale(Vector3.zero); // Scale down simultaneously
            
            // Start coroutine to destroy card and spawn empty slot after animation
            StartCoroutine(DestroySoldCardAndSpawnEmpty(soldCardInstance, emptySpacePosition, spawnedIndex));
        }
        else
        {
            // If no CardPhysObjScript, destroy directly and spawn empty slot
            Destroy(soldCardInstance);
            SpawnEmptySpaceAt(emptySpacePosition, spawnedIndex);
        }
    }
    
    /// <summary>
    /// Coroutine: Wait for sell animation to complete, then destroy card and spawn empty slot
    /// </summary>
    private System.Collections.IEnumerator DestroySoldCardAndSpawnEmpty(GameObject soldCard, Vector3 position, int insertIndex)
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
        
        // Spawn empty slot
        SpawnEmptySpaceAt(position, insertIndex);
    }
    
    /// <summary>
    /// Spawn empty slot at specified position
    /// </summary>
    private void SpawnEmptySpaceAt(Vector3 position, int insertIndex)
    {
        if (emptyCardSpacePrefab == null) return;
        
        // Instantiate empty slot
        Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
        GameObject emptySpace = Instantiate(emptyCardSpacePrefab, initialPosition, Quaternion.identity, spawnParent);
        
        // Set position and scale
        CardPhysObjScript physObjScript = emptySpace.GetComponent<CardPhysObjScript>();
        if (physObjScript != null)
        {
            physObjScript.SetPositionImmediate(initialPosition);
            physObjScript.SetTargetPosition(position);
            physObjScript.SetScaleImmediate(Vector3.zero);
            physObjScript.SetTargetScale(physCardSize);
        }
        else
        {
            emptySpace.transform.localScale = physCardSize;
        }
        
        // Insert at specified position
        if (insertIndex >= 0 && insertIndex <= _spawnedPlayerCards.Count)
        {
            _spawnedPlayerCards.Insert(insertIndex, emptySpace);
        }
        else
        {
            _spawnedPlayerCards.Add(emptySpace);
        }
        
        Debug.Log($"[ShopUXManager] Card sold, spawned empty space at index {insertIndex}");
    }

    /// <summary>
    /// Generate additional placeholder cards based on new deckSize
    /// Call this method when deckSize increases
    /// </summary>
    public void SpawnAdditionalEmptySpaces()
    {
        if (emptyCardSpacePrefab == null || ShopManager.me == null || ShopManager.me.deckSize == null)
            return;
        
        // Calculate current card count (non-empty slots)
        int cardCount = 0;
        foreach (var card in _spawnedPlayerCards)
        {
            if (card != null)
            {
                var physObj = card.GetComponent<CardPhysObjScript>();
                if (physObj != null && physObj.cardImRepresenting != null)
                {
                    cardCount++;
                }
            }
        }
        
        // Calculate how many empty slots there should be
        int targetEmptySlots = ShopManager.me.deckSize.value - cardCount;
        
        // Calculate current empty slot count
        int currentEmptySlots = 0;
        foreach (var card in _spawnedPlayerCards)
        {
            if (card != null)
            {
                var physObj = card.GetComponent<CardPhysObjScript>();
                if (physObj != null && physObj.cardImRepresenting == null)
                {
                    currentEmptySlots++;
                }
            }
        }
        
        // Number of new empty slots needed
        int newEmptySlots = targetEmptySlots - currentEmptySlots;
        
        // Generate new empty slots
        int currentTotalCount = _spawnedPlayerCards.Count;
        for (int i = 0; i < newEmptySlots; i++)
        {
            // Calculate row/column position
            int row = (currentTotalCount + i) / objPerRow;
            int col = (currentTotalCount + i) % objPerRow;
            
            Vector3 spawnPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
            
            // Instantiate empty slot占位符
            Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
            GameObject emptySpace = Instantiate(emptyCardSpacePrefab, initialPosition, Quaternion.identity, spawnParent);
            
            // Set position and scale
            CardPhysObjScript physObjScript = emptySpace.GetComponent<CardPhysObjScript>();
            if (physObjScript != null)
            {
                physObjScript.SetPositionImmediate(initialPosition);
                physObjScript.SetTargetPosition(spawnPosition);
                physObjScript.SetScaleImmediate(Vector3.zero);
                physObjScript.SetTargetScale(physCardSize);
            }
            else
            {
                emptySpace.transform.localScale = physCardSize;
            }
            
            // 记录已实例化的空位
            _spawnedPlayerCards.Add(emptySpace);
        }
        
        if (newEmptySlots > 0)
        {
            Debug.Log($"[ShopUXManager] Spawned {newEmptySlots} additional empty spaces. Total player cards: {_spawnedPlayerCards.Count}");
        }
    }

    /// <summary>
    /// Call this method when shop rerolls
    /// 1. Existing shop cards fly to shop start position and shrink to destroy
    /// 2. Generate new physical cards after animation completes
    /// </summary>
    public void OnReroll()
    {
        // 1. Make existing shop cards fly to shop start position and shrink
        AnimateShopCardsExit();
        
        // 2. 启动协程，等待Animation complete后生成新卡片
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
                    // Set target position为商店起始位置，并缩小
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
            Debug.LogWarning("[ShopUXManager] shopItems is empty or null, cannot spawn new cards!");
            return;
        }
        
        // Check if physicalCardPrefab is set
        if (physicalCardPrefab == null)
        {
            Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
            return;
        }
        
        // Iterate through shopItems.deck to instantiate physical cards
        for (int i = 0; i < shopItems.deck.Count; i++)
        {
            GameObject cardPrefab = shopItems.deck[i];
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[ShopUXManager] Shop item at index {i} is null, skipping.");
                continue;
            }
            
            // Get CardScript component
            CardScript cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null)
            {
                Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
                continue;
            }
            
            // 计算位置
            Vector3 spawnPosition = shopItemPos + new Vector3((i - 1) * xOffset, 0f, 0f);
            
            // Instantiate physical card（从商店起始位置开始，触发 DOTween 入场动画）
            Vector3 initialPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
            GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
            
            // 获取 CardPhysObjScript 并设置
            CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
            if (physObjScript != null)
            {
                physObjScript.cardImRepresenting = cardScript;
                physObjScript.shopItemIndex = i;
                physObjScript.SetPositionImmediate(initialPosition);
                physObjScript.SetTargetPosition(spawnPosition);
                physObjScript.SetScaleImmediate(Vector3.zero);
                physObjScript.SetTargetScale(physCardSize);
                
                // Set card description
                if (physObjScript.cardDescPrint != null)
                {
                    physObjScript.cardDescPrint.text = cardScript.cardDesc;
                }
            }
            else
            {
                Debug.LogWarning($"[ShopUXManager] Physical card prefab does not have CardPhysObjScript component!");
                physicalCard.transform.localScale = physCardSize;
            }
            
            // Record instantiated card
            _spawnedShopCards.Add(physicalCard);
        }
        
        Debug.Log($"[ShopUXManager] Reroll complete, spawned {_spawnedShopCards.Count} new shop cards.");
    }
}
