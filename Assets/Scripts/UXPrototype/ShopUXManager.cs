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
    [Tooltip("放大后的目标位置")]
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
    [Tooltip("是否启用鼠标滚轮控制相机上下移动")]
    public bool enableCameraScroll = true;
    [Tooltip("相机滚动速度")]
    public float cameraScrollSpeed = 5f;
    [Tooltip("相机最低Y位置（向下滚动限制）")]
    public float cameraMinY = -5f;
    [Tooltip("相机最高Y位置（向上滚动限制）")]
    public float cameraMaxY = 5f;
    
    // 存储已实例化的物理卡牌，用于清理
    private List<GameObject> _spawnedShopCards = new List<GameObject>();
    private List<GameObject> _spawnedPlayerCards = new List<GameObject>();
    
    private Camera _mainCamera;
    private float _cameraInitialY;

    /// <summary>
    /// 当 PhaseManager 进入 Shop Phase 时调用此方法
    /// 根据 shopItems DeckSO 实例化物理卡牌 prefab
    /// </summary>
    public void InstantiateShopPhysCards()
    {
        // 清理之前实例化的商店卡牌
        ClearSpawnedShopCards();
        
        // 检查 shopItems 是否为空
        if (shopItems == null || shopItems.deck == null || shopItems.deck.Count == 0)
        {
            Debug.LogWarning("[ShopUXManager] shopItems is empty or null!");
            return;
        }
        
        // 检查 physicalCardPrefab 是否设置
        if (physicalCardPrefab == null)
        {
            Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
            return;
        }
        
        // 遍历 shopItems.deck 实例化物理卡牌
        for (int i = 0; i < shopItems.deck.Count; i++)
        {
            GameObject cardPrefab = shopItems.deck[i];
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[ShopUXManager] Shop item at index {i} is null, skipping.");
                continue;
            }
            
            // 获取 CardScript 组件
            CardScript cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null)
            {
                Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
                continue;
            }
            
            // 计算位置（从 shopItemPos - xOffset 开始，使用 xOffset 水平排列）
            Vector3 spawnPosition = shopItemPos + new Vector3((i - 1) * xOffset, 0f, 0f);
            
            // 实例化物理卡牌（初始位置在目标位置下方，让Lerp动画生效）
            Vector3 initialPosition = shopItemStartPos.position;
            GameObject physicalCard = Instantiate(physicalCardPrefab, initialPosition, Quaternion.identity, spawnParent);
            
            // 获取 CardPhysObjScript 并设置 cardImRepresenting 和目标位置/缩放
            CardPhysObjScript physObjScript = physicalCard.GetComponent<CardPhysObjScript>();
            if (physObjScript != null)
            {
                physObjScript.cardImRepresenting = cardScript;
                physObjScript.shopItemIndex = i; // 设置商店物品索引
                physObjScript.SetPositionImmediate(initialPosition);
                physObjScript.SetTargetPosition(spawnPosition);
                physObjScript.SetScaleImmediate(Vector3.zero);
                physObjScript.SetTargetScale(physCardSize);
                
                // 设置卡牌描述
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
            
            // 记录已实例化的卡牌
            _spawnedShopCards.Add(physicalCard);
        }
    }
    
    /// <summary>
    /// 清理所有已实例化的商店卡牌
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
    /// 清理所有已实例化的玩家牌组卡牌
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
    /// 清理所有已实例化的物理卡牌（商店+玩家牌组）
    /// </summary>
    public void ClearSpawnedCards()
    {
        ClearSpawnedShopCards();
        ClearSpawnedPlayerCards();
    }
    
    /// <summary>
    /// 实例化玩家牌组中的物理卡牌
    /// 根据 objPerRow 自动换行，每行使用 yOffset 垂直偏移
    /// </summary>
    public void InstantiatePlayerDeckPhysCards()
    {
        // 清理之前实例化的玩家牌组卡牌
        ClearSpawnedPlayerCards();
        
        // 检查 playerDeck 是否为空
        if (playerDeck == null || playerDeck.deck == null || playerDeck.deck.Count == 0)
        {
            Debug.LogWarning("[ShopUXManager] playerDeck is empty or null!");
            return;
        }
        
        // 检查 physicalCardPrefab 是否设置
        if (physicalCardPrefab == null)
        {
            Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
            return;
        }
        
        // 遍历 playerDeck.deck 实例化物理卡牌
        int cardCount = 0;
        for (int i = 0; i < playerDeck.deck.Count; i++)
        {
            GameObject cardPrefab = playerDeck.deck[i];
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[ShopUXManager] Player deck card at index {i} is null, skipping.");
                continue;
            }
            
            // 获取 CardScript 组件
            CardScript cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null)
            {
                Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
                continue;
            }
            
            // 计算行列位置
            int row = cardCount / objPerRow;      // 当前行
            int col = cardCount % objPerRow;      // 当前列
            
            // 计算最终位置：从 playerDeckPos - xOffset 开始（与商店卡片一致），xOffset水平排列，yOffset垂直换行
            Vector3 spawnPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
            
            // 实例化物理卡牌
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
                
                // 设置卡牌描述
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
            
            // 记录已实例化的卡牌
            _spawnedPlayerCards.Add(physicalCard);
            cardCount++;
        }
        
        // 根据 deckSize 和当前牌组数量，实例化空位占位符
        if (ShopManager.me != null && ShopManager.me.deckSize != null && emptyCardSpacePrefab != null)
        {
            int emptySlots = ShopManager.me.deckSize.value - cardCount;
            for (int i = 0; i < emptySlots; i++)
            {
                // 计算行列位置（接续在卡牌后面）
                int row = cardCount / objPerRow;
                int col = cardCount % objPerRow;
                
                Vector3 spawnPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
                
                // 实例化空位占位符
                Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
                GameObject emptySpace = Instantiate(emptyCardSpacePrefab, initialPosition, Quaternion.identity, spawnParent);
                
                // 设置位置和缩放（与卡牌一致）
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
    /// 处理鼠标滚轮控制相机上下移动
    /// </summary>
    private void HandleCameraScroll()
    {
        if (!enableCameraScroll || _mainCamera == null)
            return;
        
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) < 0.001f)
            return;
        
        // 计算新的Y位置
        Vector3 cameraPos = _mainCamera.transform.position;
        cameraPos.y -= scrollInput * cameraScrollSpeed;
        cameraPos.y = Mathf.Clamp(cameraPos.y, _cameraInitialY + cameraMinY, _cameraInitialY + cameraMaxY);
        
        _mainCamera.transform.position = cameraPos;
    }
    
    /// <summary>
    /// 重置相机位置到初始Y
    /// </summary>
    public void ResetCameraPosition()
    {
        if (_mainCamera == null) return;
        
        Vector3 cameraPos = _mainCamera.transform.position;
        cameraPos.y = _cameraInitialY;
        _mainCamera.transform.position = cameraPos;
    }
    
    /// <summary>
    /// 玩家购买卡片后调用此方法
    /// 1. 删除一个占位 emptyCardSpace
    /// 2. 将被购买的卡片的目标位置设置为玩家卡组对应位置
    /// 3. 更新 _spawnedShopCards 和 _spawnedPlayerCards
    /// </summary>
    /// <param name="purchasedCardIndex">被购买的商店卡片在 _spawnedShopCards 中的索引</param>
    public void OnCardPurchased(int purchasedCardIndex)
    {
        // 1. 获取被购买的卡片
        if (purchasedCardIndex < 0 || purchasedCardIndex >= _spawnedShopCards.Count)
        {
            Debug.LogWarning($"[ShopUXManager] Invalid purchased card index: {purchasedCardIndex}");
            return;
        }
        
        GameObject purchasedCard = _spawnedShopCards[purchasedCardIndex];
        CardPhysObjScript purchasedCardPhys = purchasedCard.GetComponent<CardPhysObjScript>();
        CardScript cardScript = purchasedCardPhys != null ? purchasedCardPhys.cardImRepresenting : null;
        
        // 2. 检查卡片是否占用牌组空间
        if (cardScript != null && !cardScript.takeUpSpace)
        {
            // 如果不占用空间，直接从 _spawnedShopCards 中移除并销毁
            _spawnedShopCards.RemoveAt(purchasedCardIndex);
            
            // 更新剩余商店卡片的 shopItemIndex
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
        
        // 3. 占用空间的卡片处理：找到并删除一个 emptyCardSpace
        GameObject emptySpaceToRemove = null;
        int emptySpaceIndex = -1;
        for (int i = 0; i < _spawnedPlayerCards.Count; i++)
        {
            // 通过检查是否有 CardPhysObjScript 且 cardImRepresenting 为 null 来判断是 emptyCardSpace
            var physObj = _spawnedPlayerCards[i].GetComponent<CardPhysObjScript>();
            if (physObj != null && physObj.cardImRepresenting == null)
            {
                emptySpaceToRemove = _spawnedPlayerCards[i];
                emptySpaceIndex = i;
                break;
            }
        }
        
        // 删除找到的 emptyCardSpace
        if (emptySpaceToRemove != null)
        {
            _spawnedPlayerCards.RemoveAt(emptySpaceIndex);
            Destroy(emptySpaceToRemove);
        }
        
        // 4. 从 _spawnedShopCards 中移除
        _spawnedShopCards.RemoveAt(purchasedCardIndex);
        
        // 更新剩余商店卡片的 shopItemIndex
        for (int i = 0; i < _spawnedShopCards.Count; i++)
        {
            CardPhysObjScript physObj = _spawnedShopCards[i].GetComponent<CardPhysObjScript>();
            if (physObj != null)
            {
                physObj.shopItemIndex = i;
            }
        }
        
        // 5. 插入到被删除的 emptyCardSpace 位置（如果找到了）
        if (emptySpaceIndex >= 0)
        {
            _spawnedPlayerCards.Insert(emptySpaceIndex, purchasedCard);
        }
        else
        {
            // 如果没有找到 emptyCardSpace（牌组已满），添加到末尾
            _spawnedPlayerCards.Add(purchasedCard);
            emptySpaceIndex = _spawnedPlayerCards.Count - 1;
        }
        
        // 6. 计算玩家卡组中的新位置（填补空位的位置）
        int row = emptySpaceIndex / objPerRow;
        int col = emptySpaceIndex % objPerRow;
        
        Vector3 targetPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
        
        // 更新被购买卡片的目标位置
        if (purchasedCardPhys != null)
        {
            purchasedCardPhys.SetTargetPosition(targetPosition);
            // 清除 shopItemIndex，标记为不再是商店物品
            purchasedCardPhys.shopItemIndex = -1;
        }
        
        Debug.Log($"[ShopUXManager] Card purchased, moved to player deck position ({row}, {col})");
    }
    
    /// <summary>
    /// 玩家卖出卡片后调用此方法
    /// 1. 将被卖出的卡片移动到商店起始位置
    /// 2. 在卡片到达目标位置后销毁
    /// 3. 在被卖出的卡片位置插入 emptyCardSpace
    /// 4. 更新 _spawnedPlayerCards
    /// </summary>
    /// <param name="soldCardInstance">被卖出的物理卡片实例</param>
    /// <param name="cardIndex">被卖出卡片在玩家卡组中的原始索引</param>
    public void OnCardSold(GameObject soldCardInstance, int cardIndex)
    {
        if (soldCardInstance == null) return;
        
        // 1. 找到被卖出卡片在 _spawnedPlayerCards 中的索引
        int spawnedIndex = _spawnedPlayerCards.IndexOf(soldCardInstance);
        if (spawnedIndex < 0)
        {
            Debug.LogWarning($"[ShopUXManager] Sold card not found in _spawnedPlayerCards");
            // 直接销毁
            Destroy(soldCardInstance);
            return;
        }
        
        // 2. 从 _spawnedPlayerCards 中移除
        _spawnedPlayerCards.RemoveAt(spawnedIndex);
        
        // 3. 计算空位位置
        int row = spawnedIndex / objPerRow;
        int col = spawnedIndex % objPerRow;
        Vector3 emptySpacePosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
        
        // 4. 将被卖出的卡片设置目标位置为商店起始位置（播放卖出动画）
        CardPhysObjScript soldCardPhys = soldCardInstance.GetComponent<CardPhysObjScript>();
        if (soldCardPhys != null)
        {
            // 设置目标位置为商店起始位置
            Vector3 shopStartPosition = shopItemStartPos != null ? shopItemStartPos.position : shopItemPos;
            soldCardPhys.SetTargetPosition(shopStartPosition);
            soldCardPhys.SetTargetScale(Vector3.zero); // 同时缩小
            
            // 启动协程，在动画结束后销毁卡片并生成空位
            StartCoroutine(DestroySoldCardAndSpawnEmpty(soldCardInstance, emptySpacePosition, spawnedIndex));
        }
        else
        {
            // 如果没有 CardPhysObjScript，直接销毁并生成空位
            Destroy(soldCardInstance);
            SpawnEmptySpaceAt(emptySpacePosition, spawnedIndex);
        }
    }
    
    /// <summary>
    /// 协程：等待卖出动画完成后销毁卡片并生成空位
    /// </summary>
    private System.Collections.IEnumerator DestroySoldCardAndSpawnEmpty(GameObject soldCard, Vector3 position, int insertIndex)
    {
        // 等待动画完成（根据 lerpSpeed 和距离估算，大约 0.5 秒足够）
        yield return new WaitForSeconds(0.5f);
        
        // 销毁卖出的卡片
        if (soldCard != null)
        {
            Destroy(soldCard);
        }
        
        // 生成空位
        SpawnEmptySpaceAt(position, insertIndex);
    }
    
    /// <summary>
    /// 在指定位置生成空位
    /// </summary>
    private void SpawnEmptySpaceAt(Vector3 position, int insertIndex)
    {
        if (emptyCardSpacePrefab == null) return;
        
        // 实例化空位
        Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
        GameObject emptySpace = Instantiate(emptyCardSpacePrefab, initialPosition, Quaternion.identity, spawnParent);
        
        // 设置位置和缩放
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
        
        // 插入到指定位置
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
    /// 根据新的 deckSize 生成额外的占位卡片
    /// 当 deckSize 增加时调用此方法
    /// </summary>
    public void SpawnAdditionalEmptySpaces()
    {
        if (emptyCardSpacePrefab == null || ShopManager.me == null || ShopManager.me.deckSize == null)
            return;
        
        // 计算当前已有的卡牌数量（非空位）
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
        
        // 计算应该有多少个空位
        int targetEmptySlots = ShopManager.me.deckSize.value - cardCount;
        
        // 计算当前已有的空位数量
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
        
        // 需要生成的新空位数量
        int newEmptySlots = targetEmptySlots - currentEmptySlots;
        
        // 生成新的空位
        int currentTotalCount = _spawnedPlayerCards.Count;
        for (int i = 0; i < newEmptySlots; i++)
        {
            // 计算行列位置
            int row = (currentTotalCount + i) / objPerRow;
            int col = (currentTotalCount + i) % objPerRow;
            
            Vector3 spawnPosition = playerDeckPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
            
            // 实例化空位占位符
            Vector3 initialPosition = playerDeckStartPos != null ? playerDeckStartPos.position : playerDeckPos;
            GameObject emptySpace = Instantiate(emptyCardSpacePrefab, initialPosition, Quaternion.identity, spawnParent);
            
            // 设置位置和缩放
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
    /// 商店 reroll 时调用此方法
    /// 1. 现有商店卡片飞向商店起始位置并缩小销毁
    /// 2. 等待动画完成后生成新的物理卡片
    /// </summary>
    public void OnReroll()
    {
        // 1. 让现有商店卡片飞向商店起始位置并缩小
        AnimateShopCardsExit();
        
        // 2. 启动协程，等待动画完成后生成新卡片
        StartCoroutine(SpawnNewShopCardsAfterDelay());
    }
    
    /// <summary>
    /// 让现有商店卡片飞向商店起始位置并缩小
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
                    // 设置目标位置为商店起始位置，并缩小
                    physObj.SetTargetPosition(exitPosition);
                    physObj.SetTargetScale(Vector3.zero);
                }
            }
        }
    }
    
    /// <summary>
    /// 协程：等待退出动画完成后销毁旧卡片并生成新卡片
    /// </summary>
    private System.Collections.IEnumerator SpawnNewShopCardsAfterDelay()
    {
        // 等待动画完成（约 0.5 秒）
        yield return new WaitForSeconds(0.5f);
        
        // 销毁旧的商店卡片
        foreach (var card in _spawnedShopCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }
        _spawnedShopCards.Clear();
        
        // 生成新的商店物理卡片
        SpawnShopCardsInternal();
    }
    
    /// <summary>
    /// 内部方法：根据当前的 shopItems 生成商店物理卡片
    /// （不清理列表，因为已在调用前清理）
    /// </summary>
    private void SpawnShopCardsInternal()
    {
        // 检查 shopItems 是否为空
        if (shopItems == null || shopItems.deck == null || shopItems.deck.Count == 0)
        {
            Debug.LogWarning("[ShopUXManager] shopItems is empty or null, cannot spawn new cards!");
            return;
        }
        
        // 检查 physicalCardPrefab 是否设置
        if (physicalCardPrefab == null)
        {
            Debug.LogError("[ShopUXManager] physicalCardPrefab is not assigned!");
            return;
        }
        
        // 遍历 shopItems.deck 实例化物理卡牌
        for (int i = 0; i < shopItems.deck.Count; i++)
        {
            GameObject cardPrefab = shopItems.deck[i];
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[ShopUXManager] Shop item at index {i} is null, skipping.");
                continue;
            }
            
            // 获取 CardScript 组件
            CardScript cardScript = cardPrefab.GetComponent<CardScript>();
            if (cardScript == null)
            {
                Debug.LogWarning($"[ShopUXManager] Card prefab at index {i} does not have CardScript component!");
                continue;
            }
            
            // 计算位置
            Vector3 spawnPosition = shopItemPos + new Vector3((i - 1) * xOffset, 0f, 0f);
            
            // 实例化物理卡牌（从商店起始位置开始，让Lerp动画生效）
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
                
                // 设置卡牌描述
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
            
            // 记录已实例化的卡牌
            _spawnedShopCards.Add(physicalCard);
        }
        
        Debug.Log($"[ShopUXManager] Reroll complete, spawned {_spawnedShopCards.Count} new shop cards.");
    }
}
