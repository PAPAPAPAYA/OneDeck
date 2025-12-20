using UnityEngine;

public class EnumStorage : MonoBehaviour
{
        public enum GamePhase
        {
                Combat,
                Shop
        }

        public enum CombatState
        {
                GatherDeckLists,
                ShuffleDeck,
                Reveal,
                Resolve,
                End
        }
}
