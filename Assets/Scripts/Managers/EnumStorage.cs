using UnityEngine;

public class EnumStorage : MonoBehaviour
{
    public enum GamePhase
    {
        Combat,
        Shop,
        Result
    }

    public enum CombatState
    {
        GatherDeckLists,
        ShuffleDeck,
        Reveal
    }
    
    public enum Tag
    {
        None,
        Infected,
    }
}