using UnityEngine;
using UnityEngine.Events;

// this script is used to assign CostNEffectContainers to various trigger timings and conditions
public class CardEventTrigger : MonoBehaviour
{
    public UnityEvent cardActivateEvent;

    public void InvokeActivateEvent() // 发动
    {
        cardActivateEvent?.Invoke();
    }

    public UnityEvent cardBoughtEvent;

    public void InvokeCardBoughtEvent()
    {
        cardBoughtEvent?.Invoke();
    }
}