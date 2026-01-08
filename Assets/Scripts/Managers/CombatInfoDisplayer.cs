using System;
using DefaultNamespace.SOScripts;
using UnityEngine;
using TMPro;

// a required component of combat manager, responsible for temporarily show combat info
public class CombatInfoDisplayer : MonoBehaviour
{
	public StringSO effectResultString;
	public GamePhaseSO gamePhase;
	public TextMeshProUGUI playerStatusDisplay;
	public TextMeshProUGUI enemyStatusDisplay;
	public TextMeshProUGUI revealZoneDisplay;
	public TextMeshProUGUI combatTipsDisplay;
	public TextMeshProUGUI effectResultDisplay;

	private void Update()
	{
		if (gamePhase.Value() != EnumStorage.GamePhase.Combat) return;
		DisplayStatusInfo();
		effectResultDisplay.text = effectResultString.value;
	}

	public void ClearInfo()
	{
		playerStatusDisplay.text = "";
		enemyStatusDisplay.text = "";
		revealZoneDisplay.text = "";
		combatTipsDisplay.text = "";
		effectResultString.value = "";
		effectResultDisplay.text = "";
	}

	public void ShowCardInfo(CardScript cardRevealed, int deckSize, int cardNum, bool ownersCard)
	{
		revealZoneDisplay.text = "#" + (deckSize - cardNum) + // card num
		                         (ownersCard ? " your card: \n\n" : " their card: \n\n") + // card owner
		                         ProcessTagInfo(cardRevealed) + // tags
		                         cardRevealed.cardName + // card name
		                         "\n" + cardRevealed.cardDesc; // card description
		revealZoneDisplay.color = ownersCard ? Color.blue : Color.red;
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