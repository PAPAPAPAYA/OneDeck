using UnityEngine;
using UnityEngine.Events;

// this script is used to assign CostNEffectContainers to various trigger timings and conditions
public class CardEventTrigger : MonoBehaviour
{
    public UnityEvent ownerDealtDmgToEnemyEvent;
    public UnityEvent afterShuffleEvent;
    public UnityEvent cardActivateEvent;
    public UnityEvent cardBoughtEvent;

    public void InvokeOwnerDealtDmgToEnemyEvent() // When player deals damage to enemy
    {
        ownerDealtDmgToEnemyEvent?.Invoke();
    }
    public void InvokeAfterShuffleEvent() // After shuffle (effects like move self to top only activate after shuffle)
    {
        afterShuffleEvent?.Invoke();
    }

    public void InvokeActivateEvent() // Activate
    {
        cardActivateEvent?.Invoke();
    }

    public void InvokeCardBoughtEvent() // Buy
    {
        cardBoughtEvent?.Invoke();
    }
}