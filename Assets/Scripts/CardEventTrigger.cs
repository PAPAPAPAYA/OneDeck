using UnityEngine;
using UnityEngine.Events;

// this script is used to assign CostNEffectContainers to various trigger timings and conditions
public class CardEventTrigger : MonoBehaviour
{
    public UnityEvent afterShuffleEvent;

    public void InvokeAfterShuffleEvent() // 洗牌后（类似置顶自身的效果只在洗牌后发动）
    {
        afterShuffleEvent?.Invoke();
    }

    public UnityEvent cardActivateEvent;

    public void InvokeActivateEvent() // 发动
    {
        cardActivateEvent?.Invoke();
    }

    public UnityEvent cardBoughtEvent;

    public void InvokeCardBoughtEvent() // 购买
    {
        cardBoughtEvent?.Invoke();
    }
}