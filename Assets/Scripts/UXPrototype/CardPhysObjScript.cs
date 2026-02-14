using System;
using MilkShake;
using TMPro;
using UnityEngine;

public class CardPhysObjScript : MonoBehaviour
{
    public CardScript cardImRepresenting;
    private CombatUXManager _combatUXManager;
    
    [Header("Phase Ref")]
    [SerializeField] private GamePhaseSO currentGamePhaseRef;
    
    [Header("Shop Settings")]
    [Tooltip("商店物品索引，-1表示不是商店物品")]
    public int shopItemIndex = -1;
    [Tooltip("长按购买所需时间（秒）")]
    public float holdTimeRequired = 0.5f;
    
    [Header("LOOK")]
    public SpriteRenderer cardFace;
    public SpriteRenderer cardEdge;
    public TextMeshPro cardNamePrint;
    public TextMeshPro cardDescPrint;
    public TextMeshPro cardPricePrint;

    [Header("COLOR")]
    public Color ownerCardColor;
    public Color ownerCardEdgeColor;
    public Color opponentCardColor;
    public Color opponentCardEdgeColor;
    
    // ========== 动画目标位置 ==========
    [Header("ANIMATION")]
    [SerializeField] private float lerpSpeed = 10f;
    public Vector3 TargetPosition { get; private set; }
    public Vector3 TargetScale { get; private set; }
    
    // ========== 长按购买相关 ==========
    private bool _isHolding = false;
    private float _holdTimer = 0f;
    
    // ========== 卡片放大相关 ==========
    private Vector3 _originalPosition;
    private Vector3 _originalScale;
    private bool _isEnlarged = false;
    private bool _hasClickProcessed = false; // 防止单击和长按冲突
    private float _enlargeCooldown = 0f; // 放大冷却时间
    private const float ENLARGE_COOLDOWN_TIME = 0.5f; // 冷却时间（秒）

    void OnEnable()
    {
        _combatUXManager = CombatUXManager.me;
    }

    void Update()
    {
        ApplyColor();
        UpdateMotion(); // 在 Update 中处理动画
        UpdateStatusEffectDisplay();
        UpdatePriceDisplay();
        
        // 长按检测
        HandleHoldToBuy();
        
        // 检测再次点击恢复卡片
        HandleClickToRestore();
        
        // 更新冷却时间
        if (_enlargeCooldown > 0)
        {
            _enlargeCooldown -= Time.deltaTime;
        }
    }
    
    /// <summary>
    /// 检测再次点击恢复卡片
    /// </summary>
    private void HandleClickToRestore()
    {
        if (!_isEnlarged) return;
        
        // 如果点击了鼠标左键，恢复卡片
        if (Input.GetMouseButtonDown(0))
        {
            RestoreCard();
            // 设置冷却时间，防止立即再次放大
            _enlargeCooldown = ENLARGE_COOLDOWN_TIME;
        }
    }
    
    /// <summary>
    /// 更新价格显示，仅在 Shop Phase 显示
    /// </summary>
    private void UpdatePriceDisplay()
    {
        // 如果没有价格文本组件，直接返回
        if (cardPricePrint == null) return;
        
        // 如果不是 Shop Phase，隐藏价格显示
        if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
        {
            cardPricePrint.gameObject.SetActive(false);
            return;
        }
        
        // 如果卡牌数据为空，隐藏价格显示
        if (cardImRepresenting == null)
        {
            cardPricePrint.gameObject.SetActive(false);
            return;
        }
        
        // 显示价格
        cardPricePrint.gameObject.SetActive(true);
        
        // 商店卡片显示原价，玩家卡组中的卡片价格除以2
        int displayPrice = shopItemIndex >= 0 ? cardImRepresenting.price.value : cardImRepresenting.price.value / 2;
        cardPricePrint.text = $"<color=yellow>${displayPrice}</color>";
    }
    
    /// <summary>
    /// 处理长按购买/卖出逻辑
    /// </summary>
    private void HandleHoldToBuy()
    {
        // 只有在 Shop Phase 才检测
        if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
            return;
        
        if (_isHolding)
        {
            _holdTimer += Time.deltaTime;
            
            // 达到长按时间，触发购买或卖出
            if (_holdTimer >= holdTimeRequired)
            {
                if (shopItemIndex >= 0)
                {
                    // 商店物品：购买
                    TryPurchase();
                }
                else if (shopItemIndex == -1)
                {
                    // 玩家卡组中的卡片：卖出
                    TrySell();
                }
                _isHolding = false;
                _holdTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// 尝试购买此卡片
    /// </summary>
    private void TryPurchase()
    {
        if (ShopManager.me != null)
        {
            ShopManager.me.BuyFunc(shopItemIndex);
        }
    }
    
    /// <summary>
    /// 尝试卖出此卡片
    /// </summary>
    private void TrySell()
    {
        if (ShopManager.me == null || cardImRepresenting == null) return;
        
        // 获取此卡片在玩家卡组中的索引
        int cardIndex = GetPlayerCardIndex();
        if (cardIndex >= 0)
        {
            ShopManager.me.SellFunc(cardIndex, this.gameObject);
        }
    }
    
    /// <summary>
    /// 获取此卡片在玩家卡组中的索引
    /// </summary>
    private int GetPlayerCardIndex()
    {
        if (ShopManager.me == null || cardImRepresenting == null) return -1;
        
        var playerDeck = ShopManager.me.playerDeckRef;
        if (playerDeck == null || playerDeck.deck == null) return -1;
        
        for (int i = 0; i < playerDeck.deck.Count; i++)
        {
            if (playerDeck.deck[i] == cardImRepresenting.gameObject)
            {
                return i;
            }
        }
        return -1;
    }
    
    private void UpdateStatusEffectDisplay()
    {
        if (cardImRepresenting == null || cardNamePrint == null) return;
        
        var statusEffectText = CombatInfoDisplayer.me?.ProcessStatusEffectInfo(cardImRepresenting);
        if (!string.IsNullOrEmpty(statusEffectText))
        {
            cardNamePrint.text = $"<size=12>{statusEffectText}\n</size><b>{cardImRepresenting.gameObject.name}</b>";
        }
        else
        {
            cardNamePrint.text = cardImRepresenting.gameObject.name;
        }
    }
    
    /// <summary>
    /// 设置目标位置（由 CombatUXManager 调用）
    /// </summary>
    public void SetTargetPosition(Vector3 target)
    {
        TargetPosition = target;
    }
    
    /// <summary>
    /// 设置目标缩放（由 CombatUXManager 调用）
    /// </summary>
    public void SetTargetScale(Vector3 target)
    {
        TargetScale = target;
    }
    
    /// <summary>
    /// 立即设置位置（无动画）
    /// </summary>
    public void SetPositionImmediate(Vector3 position)
    {
        TargetPosition = position;
        transform.position = position;
    }
    
    /// <summary>
    /// 立即设置缩放（无动画）
    /// </summary>
    public void SetScaleImmediate(Vector3 scale)
    {
        TargetScale = scale;
        transform.localScale = scale;
    }
    
    /// <summary>
    /// 在 Update 中平滑移动到目标位置
    /// </summary>
    private void UpdateMotion()
    {        
        // 使用 Lerp 平滑移动
        transform.position = Vector3.Lerp(transform.position, TargetPosition, Time.deltaTime * lerpSpeed);
        transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Time.deltaTime * lerpSpeed);
    }
    
    private void ApplyColor()
    {
        // Start Card 没有 cardImRepresenting，保持默认颜色或特殊处理
        if (cardImRepresenting == null)
        {
            // Start Card 可以设置一个特殊颜色，或者保持原样
            return;
        }
        
        // myStatusRef 为空时，使用 ownerCardColor
        if (cardImRepresenting.myStatusRef == null)
        {
            cardEdge.color = ownerCardEdgeColor;
            cardFace.color = ownerCardColor;
        }
        else if (cardImRepresenting.myStatusRef != CombatManager.Me?.ownerPlayerStatusRef)
        {
            cardEdge.color = opponentCardEdgeColor;
            cardFace.color = opponentCardColor;
        }
        else
        {
            cardEdge.color = ownerCardEdgeColor;
            cardFace.color = ownerCardColor;
        }
    }
    
    private void OnMouseDown()
    {
        // 检查是否在 Shop Phase
        if (currentGamePhaseRef != null && currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop)
        {
            // 开始长按检测（商店物品和玩家卡组中的卡片都可以）
            _isHolding = true;
            _holdTimer = 0f;
            _hasClickProcessed = false;
        }
    }
    
    private void OnMouseUp()
    {
        // 如果正在长按且未达到购买时间，视为单击，触发放大
        if (_isHolding && _holdTimer < holdTimeRequired && !_hasClickProcessed)
        {
            //if (shopItemIndex >= 0)
            {
                EnlargeCard();
                _hasClickProcessed = true;
            }
        }
        
        // 取消长按
        _isHolding = false;
        _holdTimer = 0f;
    }
    
    /// <summary>
    /// 放大卡片
    /// </summary>
    private void EnlargeCard()
    {
        // 检查冷却时间 - 防止 restore 后立即 enlarge
        if (_enlargeCooldown > 0) return;
        
        // 保存原始位置和缩放
        _originalPosition = TargetPosition;
        _originalScale = TargetScale;
        
        // 获取 ShopUXManager 中的放大设置
        if (ShopUXManager.Instance != null)
        {
            float enlargeSize = ShopUXManager.Instance.physCardEnlargeSize;
            SetTargetScale(new Vector3(enlargeSize, enlargeSize, enlargeSize));
            SetTargetPosition(ShopUXManager.Instance.enlargedPosition);
        }
        else
        {
            SetTargetScale(new Vector3(2f, 2f, 2f)); // 默认放大倍数
            SetTargetPosition(Vector3.zero); // 默认位置
        }
        
        _isEnlarged = true;
        Debug.Log($"[CardPhysObjScript] Card enlarged: {cardImRepresenting?.gameObject.name}");
    }
    
    /// <summary>
    /// 恢复卡片到原始状态
    /// </summary>
    public void RestoreCard()
    {
        if (!_isEnlarged) return;
        
        // 恢复到原始位置和缩放
        SetTargetPosition(_originalPosition);
        SetTargetScale(_originalScale);
        
        _isEnlarged = false;
        Debug.Log($"[CardPhysObjScript] Card restored: {cardImRepresenting?.gameObject.name}");
    }
    
    /// <summary>
    /// 获取卡片是否处于放大状态
    /// </summary>
    public bool IsEnlarged()
    {
        return _isEnlarged;
    }
    
    private void OnMouseExit()
    {
        // 鼠标移出，取消长按
        _isHolding = false;
        _holdTimer = 0f;
    }
}
