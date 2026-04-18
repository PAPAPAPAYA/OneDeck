using UnityEngine;

namespace DefaultNamespace.Managers
{
    /// <summary>
    /// Manages starting cards for player each run.
    /// On first shop enter, randomly selects one card from starting card pool and adds to player deck.
    /// </summary>
    public class StartingCardManager : MonoBehaviour
    {
        [Header("Starting Card Pool Config")]
        [Tooltip("DeckSO containing all possible starting cards, one is randomly selected")]
        public DeckSO startingCardPool;
        
        [Header("Player Deck")]
        [Tooltip("Player's DeckSO, starting cards will be added here")]
        public DeckSO playerDeck;
        
        [Header("Game State")]
        [Tooltip("Current session number reference, used to determine if it's the first session")]
        public IntSO sessionNum;
        
        // Flag whether starting card has been given this run
        private bool _hasGivenStartingCardThisRun;

        private void OnEnable()
        {
            // Reset flag when game starts
            _hasGivenStartingCardThisRun = false;
        }

        /// <summary>
        /// Try to give player starting card.
        /// Only executes on first shop enter each run.
        /// Called via PhaseManager's onEnterShopPhase event.
        /// </summary>
        public void TryGiveStartingCard()
        {
            // Only execute on first time this run and only if it's the first session (sessionNum == 0)
            if (_hasGivenStartingCardThisRun)
            {
                return;
            }

            if (sessionNum.value != 0)
            {
                return;
            }

            if (startingCardPool == null)
            {
                Debug.LogWarning("[StartingCardManager] Starting card pool is not configured!");
                return;
            }

            if (playerDeck == null)
            {
                Debug.LogWarning("[StartingCardManager] Player deck is not configured!");
                return;
            }

            // Randomly select one card from pool
            GameObject selectedCard = GetRandomCardFromPool();
            if (selectedCard == null)
            {
                Debug.LogWarning("[StartingCardManager] Starting card pool is empty!");
                return;
            }

            // Add to player deck
            playerDeck.deck.Add(selectedCard);
            _hasGivenStartingCardThisRun = true;
            
            //Debug.Log($"[StartingCardManager] Added starting card: {selectedCard.name}");
        }

        /// <summary>
        /// Randomly select one card from starting card pool
        /// </summary>
        private GameObject GetRandomCardFromPool()
        {
            if (startingCardPool.deck == null || startingCardPool.deck.Count == 0)
            {
                return null;
            }

            int randomIndex = Random.Range(0, startingCardPool.deck.Count);
            return startingCardPool.deck[randomIndex];
        }
        
        /// <summary>
        /// Reset flag for new game
        /// </summary>
        public void ResetForNewRun()
        {
            _hasGivenStartingCardThisRun = false;
        }
    }
}
