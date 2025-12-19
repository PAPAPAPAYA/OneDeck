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
                MixAndShuffleDeckLists,
                Reveal,
                Resolve,
                End
        }
}
