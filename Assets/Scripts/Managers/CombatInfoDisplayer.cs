using System;
using UnityEngine;
using TMPro;

public class CombatInfoDisplayer : MonoBehaviour
{
    public TextMeshProUGUI playerStatusDisplay;
    public TextMeshProUGUI enemyStatusDisplay;
    public TextMeshProUGUI combatInfoDisplay;
    public TextMeshProUGUI combatTipsDisplay;

    private void Update()
    {
        DisplayStatusInfo();
    }

    //todo not clearing
    public void ClearInfo()
    {
        playerStatusDisplay.text = "";
        enemyStatusDisplay.text = "";
        combatInfoDisplay.text = "";
        combatTipsDisplay.text = "";
    }

    public void ShowCardInfo(CardScript cardRevealed, int deckSize, int cardNum, bool ownersCard)
    {
        combatInfoDisplay.text = "#" + (deckSize - cardNum) + // card num
                                 (ownersCard?" your card: \n\n":" their card: \n\n") + // card owner
                                 ProcessTagInfo(cardRevealed) + // tags
                                 cardRevealed.cardName + // card name
                                 "\n" + cardRevealed.cardDesc; // card description
        combatInfoDisplay.color = Color.blue;
    }
    private string ProcessTagInfo(CardScript card)
    {
        var tagInfo = "";
        if (card.myTags.Contains(EnumStorage.Tag.Infected))
        {
            tagInfo += "[Infected] ";
        }

        return tagInfo;
    }
    private void DisplayStatusInfo()
    {
        playerStatusDisplay.text =
            "Your HP: " + CombatManager.Me.ownerPlayerStatusRef.hp + "\n" +
            "Your Mana: " + CombatManager.Me.ownerPlayerStatusRef.mana;
        enemyStatusDisplay.text =
            "Their HP: " + CombatManager.Me.enemyPlayerStatusRef.hp + "\n" +
            "Their Mana: " + CombatManager.Me.enemyPlayerStatusRef.mana;
    }
}
