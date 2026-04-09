using UnityEngine;
using UnityEngine.Events;

// this script is used to assign CostNEffectContainers to various trigger timings and conditions
public class CardEventTrigger : MonoBehaviour
{
    public UnityEvent ownerDealtDmgToEnemyEvent;
    public UnityEvent afterShuffleEvent;
    public UnityEvent cardActivateEvent;
    public UnityEvent cardBoughtEvent;

    public void InvokeOwnerDealtDmgToEnemyEvent() // 当玩家对敌人造成伤害
    {
        ownerDealtDmgToEnemyEvent?.Invoke();
    }
    public void InvokeAfterShuffleEvent() // After shuffle（类似置顶自身的效果只在After shuffle发动）
    {
        afterShuffleEvent?.Invoke();
    }

    public void InvokeActivateEvent() // 发动
    {
        cardActivateEvent?.Invoke();
    }

    public void InvokeCardBoughtEvent() // 购买
    {
        cardBoughtEvent?.Invoke();
    }
}