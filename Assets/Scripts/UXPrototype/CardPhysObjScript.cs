using TMPro;
using UnityEngine;

public class CardPhysObjScript : MonoBehaviour
{
    public CardScript cardImRepresenting;
    private CombatUXManager _combatUXManager;
    
    [Header("MOTION CONTROL")]
    [SerializeField] private CoroutineSequencer sequencer;
    
    [Header("LOOK")]
    public SpriteRenderer cardFace;
    public SpriteRenderer cardEdge;
    public TextMeshPro cardNamePrint;
    public TextMeshPro cardDescPrint;
	//public TextMeshPro cardStatusEffectPrint;

    [Header("COLOR")]
    public Color ownerCardColor;
    public Color ownerCardEdgeColor;
    public Color opponentCardColor;
    public Color opponentCardEdgeColor;
    
    // ========== 新增：动画目标位置（由 CombatUXManager 设置） ==========
    [Header("ANIMATION")]
    [SerializeField] private float lerpSpeed = 10f;
    public Vector3 TargetPosition { get; private set; }
    public Vector3 TargetScale { get; private set; }

    void OnEnable()
    {
        _combatUXManager = CombatUXManager.me;
        // 初始化目标位置为当前位置
        TargetPosition = transform.position;
        TargetScale = transform.localScale;
    }

    void Update()
    {
        ApplyColor();
        UpdateMotion(); // 在 Update 中处理动画
        UpdateStatusEffectDisplay();
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
        
        if (cardImRepresenting.myStatusRef != CombatManager.Me?.ownerPlayerStatusRef)
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
}
