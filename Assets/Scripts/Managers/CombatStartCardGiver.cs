using UnityEngine;
using Random = UnityEngine.Random;

namespace DefaultNamespace.Managers
{
    /// <summary>
    /// Randomly gives cards to player from specified pool before combat starts
    /// </summary>
    public class CombatStartCardGiver : MonoBehaviour
    {
        [Header("Pool Config")]
        [Tooltip("Source pool to randomly select cards from")]
        public DeckSO rewardPoolDeck;
        
        [Tooltip("Player deck (target)")]
        public DeckSO playerDeck;
        
        [Header("Optional Limits")]
        [Tooltip("Whether to check deck size limit")]
        public bool checkDeckSizeLimit = true;
        
        [Tooltip("Deck size limit reference (if checkDeckSizeLimit is true)")]
        public IntSO deckSizeLimit;
        
        [Tooltip("How many cards to add per trigger")]
        public int cardsToGive = 1;
        
        [Header("Trigger Settings")]
        [Tooltip("Only trigger on first shop enter")]
        public bool onlyFirstTime = true;
        
        [Header("Debug")]
        public bool logAddedCard = true;

        // Internal state: whether cards have already been given
        private bool _hasGivenCard = false;

        private void OnEnable()
        {
            // Reset state to ensure it can trigger every time the game runs
            _hasGivenCard = false;
        }

        /// <summary>
        /// Randomly select cards from reward pool and add to player deck.
        /// Can be bound to onEnterShopPhase event.
        /// </summary>
        public void GiveRandomCardFromPool()
        {
            // Check if only first time
            if (onlyFirstTime && _hasGivenCard)
            {
                return;
            }
            
            // Validate source pool
            if (rewardPoolDeck == null || rewardPoolDeck.deck.Count == 0)
            {
                Debug.LogWarning("[CombatStartCardGiver] Reward pool is empty or not configured");
                return;
            }
            
            // Validate target deck
            if (playerDeck == null)
            {
                Debug.LogError("[CombatStartCardGiver] Player deck is not configured");
                return;
            }
            
            // Mark as triggered
            _hasGivenCard = true;
            
            for (int i = 0; i < cardsToGive; i++)
            {
                // Check capacity limit
                if (checkDeckSizeLimit && deckSizeLimit != null)
                {
                    int currentSize = GetActualDeckSize();
                    if (currentSize >= deckSizeLimit.value)
                    {
                        Debug.Log("[CombatStartCardGiver] Deck is full, stop adding");
                        return;
                    }
                }
                
                // Randomly select card
                int randomIndex = Random.Range(0, rewardPoolDeck.deck.Count);
                GameObject cardToAdd = rewardPoolDeck.deck[randomIndex];
                
                // Add to player deck
                playerDeck.deck.Add(cardToAdd);
                
                if (logAddedCard)
                {
                    Debug.Log($"[CombatStartCardGiver] Added card: {cardToAdd.name}");
                }
            }
        }
        
        /// <summary>
        /// Calculate actual deck size counting only cards that take up space
        /// </summary>
        private int GetActualDeckSize()
        {
            int count = 0;
            foreach (var card in playerDeck.deck)
            {
                var cardScript = card.GetComponent<CardScript>();
                if (cardScript != null && cardScript.takeUpSpace)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// Reset trigger state (for testing or restarting game)
        /// </summary>
        public void ResetTriggerState()
        {
            _hasGivenCard = false;
        }
    }
}
