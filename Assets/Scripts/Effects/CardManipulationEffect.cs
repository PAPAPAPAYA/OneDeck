using UnityEngine;

public class CardManipulationEffect : MonoBehaviour
{
    public DeckSO playerDeck;
    public void TakeSelfOut() // remove self from deck list
    {
        if (playerDeck.deck.Contains(gameObject))
        {
            playerDeck.deck.Remove(gameObject);
        }
    }
}
